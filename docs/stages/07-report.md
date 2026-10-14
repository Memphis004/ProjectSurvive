# Stage 07 — Battle core (EcsLite, headless) and the bot sim

- **Date:** 2026-10-15
- **Prompt:** “ทำ Stage 07 เท่านั้น: Battle Core (EcsLite แบบ headless)” — the session and its factory,
  the components, the systems in the GDD's order, the event buffer shared with Stage 02b, the bot that
  kites and shops, and a ~300-ghost + 500-projectile benchmark.
- **Result:** the battle is real. `lib/Runtime/Simulation/Battle` is an EcsLite world stepped one fixed
  30 Hz tick at a time behind the Stage 02b `IBattleSession` / `IBattleSessionFactory` contract, holding
  **every case of the shared contract suite** the fake already held, using the **same** `BattleEventRing`
  and `BattleFrameBuffer` instances the fake uses, with **no scene, no `UnityEngine`, no physics**. It
  measures **0 bytes allocated per tick** at 300 ghosts + 500 projectiles (avg 0.114 ms, p95 0.126 ms
  per tick, Release, .NET 9, machine quoted in §4). The bot harness plays 200 seeds through all five
  floors. No `data/` row changed: three numbers the prototype wants do not exist in the tables, and
  [ADR 0017](../../docs/adr/0017-ecslite-battle-simulation.md) names the column for each instead of
  inventing a constant.

## 1. What was built

**The session and the factory.** `EcsBattleSessionFactory` implements `IBattleSessionFactory`: it
validates the `BattleSetup`, derives the battle RNG stream with
`RandomStreams.DeriveStreamSeed(seed, RandomStreamKind.Battle)` (shop and visual streams untouched),
builds the def-id map once, and hands each session the config plus that stream. `EcsBattleSimulation`
is the session: `Step(inputFrame)` advances exactly one tick; `Phase`, `Tick`, `Hud`, `Entities`,
`Events` and `Result` are the contract's, and `Dispose` tears the world down. It is the **only**
`IBattleSession` implementation besides the fake, and nothing outside `lib/Runtime/Simulation` names an
EcsLite type (architecture test, §7).

**The world.** Fourteen components (`Position`, `Velocity`, `Health`, `Faction`, `PlayerTag`,
`EnemyTag`, `Mana`, `Caster`, `Projectile`, `Status`, `ContactAttack`, `Pickup`, `Lifetime`,
`Collider`) — plain structs with public fields, no references, no EcsLite types inside. Everything a
system needs about a def (move speed, contact damage, cooldown, damage, reach) is **copied onto the
entity at spawn**, so no hot path looks a def up. One caster entity per loadout entry, so weapons keep
independent cooldowns without the player holding a collection; player HP/MP live in components, never
in a mirror field, so the HUD, the floor-end rule and the result read one number.

**The systems, in the GDD's order** (`WaveSpawn → LoadTest → PlayerMove → EnemySteer → AutoCast →
ProjectileMove → SpatialHash → Collision → DamageStatus → Death → Drop → Pickup → FloorEnd →
FramePublish`). `Step` clears the per-tick queues, advances the tick, runs them, publishes the frame,
and only then—if a system asked to end the floor—produces the result. The order is load-bearing and
commented as such: steering before the grid, the grid before collision, collision before the damage that
can kill, and the frame last so it describes a finished tick. Two systems the prompt does not list are
explicit additions: `LoadTest` (benchmark-only, immediately after `WaveSpawn`, so it can top the world up
before anything reads it) and `FramePublish` (last, because `Collision`/`Death` must not race the frame).

The prompt says to *start* with one player, one chasing ghost and one projectile plus one aura. The
shipped simulation is that plus the other two cast behaviours the item table already declares
(melee, pierce) and the three enemy behaviours `enemy.csv` already declares (chase, charge,
stationary), because `data/` describes them and a smaller simulation would have refused rows the
Stage 04 content already carries.

**Bounds, and what is not balance.** `BattleArena` holds the disc radius (40), the body radii, the
spatial-hash cell size (4 units) and the caps (512 ghosts, 1024 projectiles, 256 pickups, 2048 queued
hits/tick, 2048 entity slots, 1280 dense slots per pool, 1024 recycled slots, 1536 dense slots per
filter). None of it is a balance number: they are memory bounds and geometry the simulation needs in
order to exist — the pattern Stage 02b set for the fake's orbit constants — and the one a player could
feel (the arena radius) is documented as Stage 11's to move into `data/` if the balance pass wants it.

**Targeting and the grid.** `BattleSpatialHash` is a uniform grid rebuilt once per tick and read by
`AutoCast`, `Collision` and `Pickup` (`QueryRadius` / `TryFindNearest`). It is one tick old when
`AutoCast` reads it (it is rebuilt *after* casting), so liveness is re-checked — a ghost that died last
tick can still be in the grid. `AutoCast` casts **at most once per tick**: among the casters that are
ready and whose `MpCost` the pool can pay, the shortest base cooldown fires at the nearest ghost, ties
keeping the earliest loadout entry. That is what makes MP and cooldown the two real limits on a build:
a build that costs more than its focus income casts less often than its cooldowns allow.

**Events and the frame are the shared Stage 02b objects.** The session publishes into the same
`BattleEventRing` (4096 slots, with `DroppedEvents` reported rather than hidden) and the same
`BattleFrameBuffer` double buffer the fake uses — not copies. `ArchitectureTests.Battle_frame_has_exactly_one_implementation`
still passes, so a Presentation written against the fake sees identical frame and event behaviour from
the real battle.

**Def ids.** `BattleEvent.DefId` is an `int` (no allocation per hit, no config lookup in presentation),
so the string ids `data/` uses have to become numbers somewhere. `BattleDefIds` numbers a def by its
**1-based position in its table** (enemy / skill / item, in `IGameConfig` order = `data/tables/*.csv`
file order); `0` means “no def”, and an id that is not in a table resolves to `0` rather than to a
guess. The map is built once per factory and never persisted, so inserting a row anywhere but the end of
a table renumbers ids — safe, because a def id lives only inside one battle's event stream (a save names
string ids).

**Determinism and the state hash.** Randomness is the battle stream only, consumed by spawning
positions and projectile fan angles. `ComputeStateHash` mixes the phases, the tick, the counters and
the per-wave spawn state, then **walks every live entity in entity-index order** (via
`GetAllEntities` into a preallocated buffer), mixing whichever components each entity carries.
Entity-index order is a function of the operation sequence alone, so it is stable across a replay and
does not depend on which pool a system happened to touch.

**Allocation discipline.** Everything the tick touches is allocated once, in the constructor: the world
and its pools, every filter, the per-tick queues, the scratch arrays, the event ring, both frame slots,
the grid, and the result's consumable array. The code follows three rules to keep it that way —
`foreach` over an `EcsFilter` binds the struct enumerator (no boxing) and every filter is sized with
`End(denseCapacity)` so none resizes mid-battle; anything that deletes an entity **collects first and
deletes after the enumeration**, so no pool access follows a deletion; and dead slots are recycled, so
`NewEntity` reuses an index instead of growing the world. **The one allocation a battle makes is the
`BattleResult`** (with its consumable array) when the floor ends — roughly 88 bytes, once. That is why
the benchmark measures *inside* a running floor (§4).

**The tick/backlog/pause policy — the part the prompt asked for and Stage 08 has to implement.** The
battle owns **no clock**: no `Time`, no `DateTime`, no accumulator, all forbidden by an architecture
test. Paused is therefore simply “not stepped” — an inactive tab stops calling `Step` and nothing
accumulates. Backlog is **dropped, not replayed**: the Present driver feeds real deltas to `FixedTicker`
(Stage 01), which hands out at most `MaxStepsPerFrame` (4) steps per frame and counts the surplus in
`DroppedTicks`. Replaying a ten-second stall as 300 steps would be a death sentence in a game about not
being touched, and would spiral into longer frames on a machine that cannot keep up. A dropped tick is
not simulated at all, so a replay reproduces the *battle* (seed, ticks, inputs) — not the wall-clock
history of the machine that recorded it.

**The bot sim, and its two modes.** `lib/Tests~/Sim` grew a kiting bot (`KiteBot`: danger radius, orbit
weight, retreat bias — the bot's skill, not the game's balance) and a greedy camp policy (`CampBot`:
rent what it can afford, place it, merge when the rule allows, depart), driven through `RunSession` and
reaching a battle **only through `IBattleSession`**, so it would run against any factory. Two modes
share the one binary: `bot` plays consecutive seeds and writes one row per floor to
`artifacts/balance.csv`; `benchmark` runs the acceptance load and writes per-tick timing to
`artifacts/benchmark.csv`. `tools/sim.ps1` drives both, and the `streams` mode from Stage 01 is
untouched (its `--quiet` digest line is still what the determinism check compares byte for byte).
`tools/test.ps1` gained a required `Sim bot run` check that smoke-runs two seeds and fails if no rows
land, so the bot cannot silently rot.

**What verification caught.** One real bug, and it was a silent one: `BattleContext.WaveCount` was never
assigned, so `WaveSpawn` opened its windows but iterated none of them — a floor with **no ghosts at all**
timed out as a *win*. Every rule test that expected a kill or a death failed at once, which is how it
surfaced; it is now assigned from `Waves.Length` and is the first entry in the “facts that cost time to
learn” list on the system page. Two EcsLite behaviours also had to be learned the hard way and are
written down: a filter enumerator is a **lock** (an add/remove inside the loop is deferred to a
`DelayedOp` buffer until `Dispose`, which is why deletion is always collected outside the loop), and
`GetAllEntities(ref buffer)` **resizes the buffer you pass**, so it is held in a field and sized to the
entity capacity rather than allocated per call.

## 2. Files that matter

| File | What |
| --- | --- |
| `lib/Runtime/Simulation/Battle/EcsBattleSimulation.cs` | The session: world, systems, phases, result, `ComputeStateHash`, `Dispose` |
| `lib/Runtime/Simulation/Battle/EcsBattleSessionFactory.cs` | `IBattleSessionFactory`; setup validation, battle stream derivation, config + def-id ownership |
| `lib/Runtime/Simulation/Battle/BattleContext.cs` | Per-session shared state: config, pre-resolved wave timeline, per-tick queues, counters, ring, frame, grid, spawner |
| `lib/Runtime/Simulation/Battle/BattleArena.cs` | Structural bounds only (radius, body radii, cell size, caps, ECS capacities) |
| `lib/Runtime/Simulation/Battle/BattleComponents.cs` | The component vocabulary (plain structs) |
| `lib/Runtime/Simulation/Battle/BattleSpawner.cs` | Entity creation (player, caster, ghost, projectile, pickup) |
| `lib/Runtime/Simulation/Battle/BattleSpatialHash.cs` | The uniform grid: rebuild, `QueryRadius`, `TryFindNearest` |
| `lib/Runtime/Simulation/Battle/BattleDefIds.cs` | String id → 1-based table position, built once per factory |
| `lib/Runtime/Simulation/Battle/EcsBattleOptions.cs` | The benchmark knobs (`LoadTest*`) — never balance |
| `lib/Runtime/Simulation/Battle/Systems/*` | The fourteen systems, in run order |
| `lib/Tests~/Core.Tests/Battle/EcsBattleSessionContractTests.cs` | The shared contract suite bound to the real factory (and the two premises it has to supply) |
| `lib/Tests~/Core.Tests/Battle/EcsBattleRulesTests.cs` | 10 cases: movement per tick, arena clamp, cooldown, MP, projectile hit + despawn, one reward per kill, one floor end per death, boss win, steering, the zero-allocation tick |
| `lib/Tests~/Core.Tests/ArchitectureTests.cs` | `Ecs_types_do_not_leak_outside_the_simulation_layer` (new); the frame/port tests it must keep passing |
| `lib/Tests~/Sim/{KiteBot,CampBot,BotRun,BenchmarkMode,SimOptions,SimEnvironment,StreamsMode,Program}.cs` | The bot, the benchmark and the mode dispatch |
| `tools/sim.ps1` | `--seeds 200` → `artifacts/balance.csv`; `--mode benchmark` → `artifacts/benchmark.csv` |
| `tools/test.ps1` | New required `Sim bot run` check |
| `docs/adr/0017-ecslite-battle-simulation.md` | Tick/backlog/pause, def ids, auto-cast, elemental status, drop value, magnet speed, arena bounds, the contract suite's premises |
| `llmwiki/systems/battle-simulation.md` | The system note: file map, one tick, components, bounds, auto-cast, determinism, allocation, the data gaps, how to run it, the gotchas |
| `llmwiki/index.md`, `llmwiki/stage-log.md`, `knowledge.md` | Status, links and the Stage 07 summary |

## 3. Commands actually run

```powershell
# The whole gate: build (warnings are failures) + xUnit + Sim determinism + streams + bot smoke + data gates
pwsh -NoProfile -File tools/test.ps1 -Json

# Focused, during development
dotnet build "lib/Tests~/ProjectSurvive.Core.sln"
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~Battle"
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~EcsBattle"
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~ArchitectureTests"

# The bot: 200 seeds through all five floors (the acceptance balance run)
pwsh -NoProfile -File tools/sim.ps1 --seeds 200

# The acceptance benchmark: ~300 ghosts + 500 projectiles, Release, .NET
pwsh -NoProfile -File tools/sim.ps1 --mode benchmark
```

Unity (Editor `6000.3.9f1`, through the Editor bridge): `recompile` → `recompile_status` →
`console_status` → `list_tests` → the EditMode suite, then the PlayMode suite (`--async_tests`, polled
to completion).

## 4. Results and where the artifacts are

| Gate | Result | Evidence |
| --- | --- | --- |
| `tools/test.ps1` | **`PASS 11 / FAIL 0 / WARN 0 / SKIP 0`**, `exitCode: 0`; build with **no warnings**, **413 tests passed**, Sim determinism, the three streams separated, the bot smoke run, the three data gates | console; `artifacts/stage07-test.log` (full JSON) |
| Core test count | **413** total, **28** of them Stage 07: 27 `EcsBattle*` cases (10 rules + the 17 contract-suite cases the real factory inherits) + 1 architecture test | `dotnet test --filter` runs above |
| Sim determinism | seed `20261013` → `determinismDigest=0x841b4e46`; seed `20261014` → `0x23f674fd` (two processes, byte-identical `--quiet` lines) | the gate above |
| Stream separation | `shopDigest=0x2d476321 battleDigest=0xbeb639e1 visualDigest=0xe8742eda` | the gate above |
| Bot smoke run | 2 seeds → 6 floor rows | the gate above; `artifacts/balance-smoke.csv` |
| Data | **no `data/` change this stage** — generated tables validate with no findings, regeneration reproduces the tracked output, 17 fixtures still rejected | the gate above |
| Acceptance benchmark | 300 ghosts + 500 projectiles (alive: enemies avg 299.03 / max 301, projectiles avg 482.61 / max 494), 900 warm-up + 600 measured ticks, `floorEndedAtTick=0`: **avg 0.11404 ms, p50 0.1123 ms, p95 0.1255 ms, p99 0.1455 ms, max 0.2619 ms per tick**, **`allocatedBytesPerTick=0`, `allocatedBytesTotal=0`** | `artifacts/benchmark.csv` (per-tick rows) and `artifacts/sim-benchmark.txt` |
| Benchmark machine | 16 logical cores; Windows `10.0.26300` x64; **.NET 9.0.13**; `Intel64 Family 6 Model 191 Stepping 2`; Release configuration | machine line in `artifacts/sim-benchmark.txt` and `artifacts/benchmark.csv` |
| Balance run | 200 seeds (**760 floor rows**): floor 1 **200/200**, floor 2 **198/200**, floor 3 **91/198**, floor 4 **71/91**, floor 5 **71/71** (wins / floors played) | `artifacts/balance.csv`, `artifacts/sim-bot.txt` |
| Unity EditMode | **33/33** pass (unchanged count — this stage adds no Unity test) | Editor bridge test run |
| Unity PlayMode | **4/4** pass (unchanged) | Editor bridge test run |
| Unity compile | clean: `ProjectSurvive.Core.Simulation.dll` was rebuilt by the Editor **after** the last source edit, `compilationFailed: false`, no new console errors or warnings | `recompile_status` / `console_status`, assembly timestamps |

The 200-seed balance picture is worth reading carefully, because it is a **symptom of the data gaps** as
much as of the tables. Floor 1 is free, floor 2 costs 2 runs, floor 3 is the hard filter (91 of 198),
floor 4 costs another 20, and **the boss floor killed nobody who reached it (71/71)** — the one result
a balance pass should look at first. Part of this is the bot, but the structural part is §6: **a run
earns no bia inside a battle**, so a camp can only ever spend `economy.start_bia` and a build cannot
grow after the first visit.

## 5. What was not tested, and why

1. **No WebGL build, no browser, no Playwright run.** Stage 07 adds no scene, no prefab and no UI, so
   there is nothing a player build would exercise that the EditMode suite does not. The size gate and
   the browser frame budget stay Stage 03b/15 work.
2. **The performance numbers are .NET numbers, exactly as the prompt specifies** (“บน .NET”), and they
   are **not** a claim about the Web player. The Web target is IL2CPP/WASM and single-threaded, where
   the same tick can be several times slower; nothing in this report says otherwise. The only thing the
   zero-allocation result transfers with is the *code* discipline (no boxes, no LINQ, no closures, no
   string work) — the number itself has to be re-measured in a build, which is Stage 08's job.
3. **No bitwise determinism across browsers or CPUs is claimed** (AGENTS rule 6). “Same seed and state →
   same battle” and “a recording replays to the same `ComputeStateHash`” are asserted *inside one
   runtime*; the scope gotcha stands.
4. **Elemental status, kill rewards and the magnet are unexercised as *balance*.** The status pipeline
   runs and is tested, but its magnitude is the applying skill's own damage (not a designer's number);
   a kill pays `0`, so `BattleResult.CurrencyEarned` is `0`; a pickup is collected instantaneously
   inside `constant.pickup_radius` rather than drifting in. All three are named gaps with a named column
   in §6, not untested code.
5. **The Unity composition root still binds no battle session and no run scope.** Nothing resolves
   `IBattleSessionFactory` from the container, so “the container hands a run its battle factory” is
   **NOT RUN**; the bot and the tests construct the factory directly. Still deliberate
   ([ADR 0016](../../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md)), and now the thing
   Stage 08 has to finish.
6. **`LoadTestSystem`'s knobs are exercised only by the benchmark and the contract suite.** The
   real-floor path is what the bot plays; the knobs are a test/benchmark instrument, and nothing in
   `data/` can turn them on.
7. **Two files in the working tree are deliberately *not* part of this commit**:
   `UnityProject/ProjectSettings/ProjectSettings.asset` (rewritten by `WebSizeProfilesTests` applying a
   size profile to the real `PlayerSettings` whenever the EditMode suite runs — a pre-existing Stage 03
   side effect, not a Stage 07 decision) and `UnityProject/Assets/Main/Scripts/Presentation/Thai/ThaiTextSpike.cs`
   (one trailing space in a doc comment, already dirty when this stage started).

## 6. Blockers and risks

- **No blocker.** No Unity license, browser, network or engine upgrade was needed; the whole stage is
  headless and the Editor was used only to prove the same sources compile and pass under Unity's
  compiler.
- **Risks accepted, with reasons:**
  1. **`enemy.drop_value` does not exist, so a battle pays nothing.** The drop pipeline is real end to
     end (death → queued drop → spawn → collect → `PickupCollected`) but the value is `0`. This is the
     single biggest balance distortion in the run today, and it is deliberate: a number a balance pass
     must move belongs in a column. Stage 11 adds `enemy.drop_value` (per row, so a boss can pay more
     than a filler ghost).
  2. **Elemental status has no magnitude of its own.** It re-uses the applying skill's damage on a 1 s
     cadence, refreshed rather than stacked, until a `status` table exists. Not a balance claim — the
     only data-derived number available today ([ADR 0017](../../docs/adr/0017-ecslite-battle-simulation.md)).
  3. **The magnet is instantaneous.** A drifting pull needs a speed and there is no
     `constant.magnet_speed`; collecting instantly inside `pickup_radius` is the honest reading of the
     one column that does exist.
  4. **Def ids are table positions, so inserting a row renumbers them.** Safe only because an id never
     leaves a battle's event stream. If a future stage persists a def id (a stat snapshot, a log), that
     assumption breaks and the id has to become the string or a stable enum — written down in the ADR.
  5. **The contract suite's premises are supplied by the derived class.** The shared suite drives an
     idle player and expects an event stream larger than the 4096-slot ring, neither of which a real
     floor produces on its own (real floors are tuned for a *kiting* player; a boss floor has nothing to
     kill). The derived class hands in a large HP pool and turns on the churn knob
     (`LoadTestEntityLifetimeTicks`). Neither touches a balance number, and **no assertion in the shared
     suite was weakened or skipped** — but a reader of that suite should know the two inputs exist.
  6. **EcsLite pools grow if a cap is wrong.** Every filter declares `End(denseCapacity)`, so exceeding a
     cap would resize a pool mid-battle instead of failing loudly. The caps are set well above the
     worst case the benchmark reaches (301 ghosts, 494 projectiles), and the risk is bounded by the
     arena's spawn caps — but a future content stage that raises enemy counts has to raise these too.
  7. **The bot is a measuring instrument, and its numbers are its own.** Floors 3–5 in
     `artifacts/balance.csv` say as much about `KiteBot`'s danger radius and orbit weight as about the
     floor tables. The bot's constants live in the sim (never in `lib/`), and the balance pass must
     treat the CSV as an input, not as the game's difficulty.
  8. **`BattleReplay` is the *battle* half only.** The run-level `ReplayLog` embeds one per floor; the
     battle does not know about the run, and nothing here persists a run. Save/resume remains Stage 12.

## 7. Acceptance criteria

| # | Criterion (from the Stage 07 prompt) | Verdict |
| --- | --- | --- |
| 1 | BattleSimulation takes config, seed, `CombatLoadoutSnapshot` and an input frame; fixed 30 Hz tick; a stated backlog and pause policy for an inactive tab | **PASS** — `EcsBattleSessionFactory` takes `IGameConfig` + `BattleSetup` (seed, loadout, floor); `Step(PlayerInputFrame)` is one tick; the policy is ADR 0017 §1 (paused = not stepped; surplus dropped and counted by `FixedTicker`, `MaxStepsPerFrame = 4`) |
| 2 | Components: `Position`, `Velocity`, `Health`, `Faction`, `Enemy(kind)`, `Projectile`, `Caster(cooldown, mpCost)`, `Status`, `Pickup`, `Lifetime`, `Collider` | **PASS** — all present (`Faction` + `EnemyTag` carry the kind via the def id copied at spawn), plus `Mana`, `ContactAttack`, `PlayerTag` |
| 3 | Systems in the order `WaveSpawn → PlayerMove → EnemySteer → AutoCast (MP, lowest cooldown) → ProjectileMove → SpatialHash → Collision → Damage/Element/Status → Death → Drop → Magnet/Pickup → FloorEnd` | **PASS** — exactly that order (`LoadTest` inserted after `WaveSpawn`, `FramePublish` last; both justified in §1) |
| 4 | Start small: one player, one chasing ghost kind, one projectile spell and one aura — then add | **PASS** — both are implemented; the four cast behaviours and three enemy behaviours `data/` already declares ship too, because the content is already there |
| 5 | Event buffer for Presentation; no references to GameObject, Camera, UI or Unity Physics | **PASS** — `BattleEventRing` (Stage 02b, shared); `ArchitectureTests.Core_assemblies_do_not_reference_unity` + `Runtime_sources_do_not_reference_unity` cover the layer, and the simulation has no `UnityEngine` using at all |
| 6 | Pool entities and arrays; no allocation in `Tick` after warm-up | **PASS** — measured: `allocatedBytesPerTick=0` over 600 ticks at the acceptance load (§4), and asserted by `The_acceptance_load_allocates_nothing_per_tick` |
| 7 | `Sim/`: kiting bot + greedy camp/bag policy; `tools/sim.ps1 --seeds 200 → artifacts/balance.csv` | **PASS** — `KiteBot` + `CampBot` + `tools/sim.ps1`; 200 seeds → 760 floor rows in `artifacts/balance.csv` |
| 8 | BattleSimulation (EcsLite) implements `IBattleSession` + `IBattleSessionFactory` | **PASS** — `EcsBattleSimulation` / `EcsBattleSessionFactory`; the fake implements only the factory side, so the two are not interchangeable by accident |
| 9 | Must pass the same `BattleSessionContractTests` as the fake — no editing or skipping a test to pass | **PASS** — `EcsBattleSessionContractTests : BattleSessionContractTests<EcsBattleSessionFactory>`; the shared suite is **unchanged**, and the two premises it needs are supplied as inputs (§6 risk 5) |
| 10 | Event buffer uses the Stage 02b `BattleEventRing`; EcsLite types must not escape `lib/Runtime/Simulation` | **PASS** — the same ring instance; `ArchitectureTests.Ecs_types_do_not_leak_outside_the_simulation_layer` scans both `lib/` and the Unity scripts |
| 11 | Bot sim reaches a battle only through `IBattleSession` | **PASS** — the bot holds an `IBattleSessionFactory`; no EcsLite type is named in `lib/Tests~/Sim` |
| 12 | Benchmark at ~300 ghosts + 500 projectiles: avg/p95 ms per tick, GC allocation per tick = 0 after warm-up | **PASS** — avg 0.114 ms, p95 0.126 ms, **0 bytes/tick** (§4), with the machine and configuration named |
| — | Tests: movement is per tick; cooldown and MP never over-cast | **PASS** — `The_player_advances_by_move_speed_each_tick`, `The_player_cannot_leave_the_arena`, `A_weapon_never_casts_faster_than_its_cooldown`, `Mp_never_goes_negative_and_a_cast_that_costs_too_much_never_happens` |
| — | Tests: projectile hit and despawn; a kill is rewarded once; a player death ends the floor once | **PASS** — `A_projectile_hits_a_ghost_for_the_weapon_damage_and_then_despawns`, `A_kill_is_rewarded_exactly_once`, `The_player_dying_ends_the_floor_exactly_once` (+ `Killing_the_boss_wins_the_boss_floor`, `Ghosts_walk_towards_the_player`) |
| — | Tests: the simulation disposes; a seeded replay repeats within one runtime | **PASS** — `Dispose` is covered by the contract suite's lifecycle case, and the replay round-trip (`BattleReplay` → same `ComputeStateHash`) is a shared contract case the real factory now passes |
| — | Acceptance: **runs with no scene** | **PASS** — the whole stage runs from `dotnet` with no Unity scene loaded; the Editor was used only for compile + the existing suites |
| — | Acceptance: **no promise of bitwise determinism across browser or CPU** | **PASS** — stated in ADR 0017 and §5.3, and nothing in the code or the harness implies cross-runtime equality |
| — | Acceptance: **uses `BattleEventRing` and `BattleFrameBuffer` shared with the fake; must not implement `IBattleFrame` itself — `BattleFrameBuffer` is the only implementation, enforced by an architecture test** | **PASS** — `ArchitectureTests.Battle_frame_has_exactly_one_implementation` still green; the session publishes into the shared instances |
| — | Compile + tests (DoD) | **PASS** — `tools/test.ps1` `PASS 11 / FAIL 0 / WARN 0 / SKIP 0` with **no build warnings**, 413 tests; Unity compile clean; EditMode **33/33**; PlayMode **4/4** |
| — | Documentation: ADR for the open choices and the missing data | **PASS** — [ADR 0017](../../docs/adr/0017-ecslite-battle-simulation.md) (tick/backlog/pause, def ids, auto-cast, status magnitude, drop value, magnet speed, arena bounds, the contract premises) + `llmwiki/systems/battle-simulation.md` |
| — | Commit `stage-07:` | **PASS** — see the commit carrying this report |

**Gate that mattered most:** the shared contract suite, unchanged, bound to the real factory. Writing no
new assertions and passing every one of the fake's was what forced the honest answers — the loadout
handed in a config rather than a stub, the frame and the event ring reused rather than re-implemented,
and the two suite premises supplied as inputs instead of one assertion relaxed.
