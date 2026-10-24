# Battle simulation (EcsLite) — Stage 07

The real battle: an EcsLite world of ghosts, projectiles and drops, driven one fixed
30 Hz tick at a time, with no `UnityEngine`, no scene and no physics. It implements the
Stage 02b [`IBattleSession`](battle-contract.md) contract, so everything a caller knows
about a battle it learned from that page; this page is the *inside*.

- Decision record: [ADR 0017](../../docs/adr/0017-ecslite-battle-simulation.md)
- Stage report: [`../../docs/stages/07-report.md`](../../docs/stages/07-report.md)
- Implementation: `lib/Runtime/Simulation/Battle/**` (nothing outside this folder may
  name an EcsLite type — enforced by an architecture test)

## 1. File map

| File | What it owns |
| --- | --- |
| `EcsBattleSimulation` | the session: world, systems, phases, the result, `ComputeStateHash`, `Dispose` |
| `EcsBattleSessionFactory` | `IBattleSessionFactory`; validates the setup, derives the battle RNG stream, owns the config + def-id map |
| `BattleContext` | per-session shared state: config, pre-resolved wave timeline, per-tick queues, counters, event ring, frame buffer, spatial hash, spawner |
| `BattleArena` | structural bounds only: disc radius, body radii, grid cell, entity caps, ECS capacities |
| `BattleComponents` | the component vocabulary (plain structs, no references) |
| `BattleSpawner` | creates entities (player, caster, ghost, projectile, pickup) |
| `BattleSpatialHash` | the uniform grid: rebuild + `QueryRadius` / `TryFindNearest` |
| `BattleDefIds` | string id → 1-based table position, built once per factory |
| `EcsBattleOptions` | the load-benchmark knobs (never balance) |
| `Systems/*` | the fourteen systems, in run order |
| `Systems/SweepSystem` | the end-of-floor sweep: pulls the remaining drops in and ends the floor |

## 2. One tick

`Step(input)` → clear the tick queues → `Tick++` → run the systems in exactly this order
→ publish the frame → if a system asked to end the floor, produce the result.

| # | System | Does |
| --- | --- | --- |
| 1 | `WaveSpawn` | opens each wave window, walks ghosts in from the rim, announces once |
| 2 | `LoadTest` | benchmark only: tops the world up to `LoadTestEnemies` / `LoadTestProjectiles`, with optional churn |
| 3 | `PlayerMove` | moves the player from the input frame, clamps to the disc |
| 4 | `EnemySteer` | walks ghosts at their def's speed toward the player — only while they are chasing (see §2c) |
| 4b | `EnemyAttack` | the per-kind telegraphed attack, its warning events and its damage (§2c) |
| 5 | `AutoCast` | focus regen, cooldown clocks, one cast (see §5) |
| 6 | `ProjectileMove` | advances shots, releases expired / out-of-bounds ones, recomputes the in-flight count |
| 7 | `SpatialHash` | rebuilds the grid the next three systems read |
| 8 | `Collision` | contact damage (player) and shot-vs-ghost overlap, both **queued** |
| 9 | `DamageStatus` | applies the queued damage, applies/tick/expires statuses, hurts the player |
| 10 | `Death` | zero-HP ghosts: count the kill once, announce, queue the drop, release |
| 11 | `Drop` | spawns the queued pickups (capped) |
| 12 | `Pickup` | collects drops inside the pickup radius |
| 13 | `FloorEnd` | the only place a win or a loss is chosen (plus the two timer warnings) |
| 14 | `FramePublish` | writes the HUD, the render entities and the per-caster cooldown list into `BattleFrameBuffer` |

## 2b. The end-of-floor sweep (Stage 07c)

A **win does not end the floor immediately**. `FloorEndSystem` decides the win and the reason,
then sets `BattleContext.SweepActive` instead of ending: the contract's phase stays `Running`
while `SweepSystem` pulls every drop still on the ground into the player, at
`constant.magnet_speed`, and pays it. The sweep closes when the last pickup is collected or when
`constant.sweep_timeout_seconds` runs out; at the cap whatever is left is collected at once, so a
won floor always produces its result. A **loss** (the player dying, or a boss floor timing out)
never sweeps, and `DevWinNow` still finishes immediately because it stands for "skip this floor".

## 2c. Telegraphed ghost attacks (Stage 11b)

A ghost whose `enemy` row names a pattern carries `EnemyAttack` (the row's numbers, in ticks) and
`EnemyBrain` (where it is in its cycle). `EnemyAttackSystem` runs one small machine for both kinds and
skips every stage whose tick count is zero:

```
Chase -> Telegraph -> Strike -> FollowUpTelegraph -> FollowUp -> Retreat -> Recover -> Chase
```

- **Krasue (`DashFollowUp`, called `DashDrain` before Stage 16)**: warns a **line** (`attack_length`
6, half-width 0.9) for 0.6 s, dashes along it for 0.25 s, warns a **circle** around herself for
0.4 s, drains for 1.0 s (2 damage every 0.3 s inside 2.2 units), retreats at 2.4 u/s, then waits
`attack_cooldown_seconds`.
- **กระหัง (`DashFollowUp`, Stage 16)**: the same machine with a different payload — a dive along a
5.5-unit line after a 0.7 s warning, then a single **gale** (circle, radius 4.2, one application)
at the landing point that also **throws** the player `follow_up_push` units away from the ghost, and
no retreat. The shove is the only rule in the battle that moves the player other than input; it is
clamped to the arena and resolved against the tick's starting positions, so two gales in one tick
both hit where the player stood.
- **Nang Tani (`RingVines`)**: stands still (her `move_pattern` is `Stationary`), warns a **ring** of
radius 4.5 for 1 s around herself, then warns a **vine line** (length 7, half-width 0.55) for 0.8 s
and lashes it. Both hit once, for the row's damage, at the end of their own warning.
- A ghost with `attack_pattern = None` (the wandering spirit, Pob) gets neither component and keeps
the walking rule it had.

**Steering cooperates instead of competing**: `EnemySteerSystem` moves a ghost only while its brain is
`Chase`. A dash, a drain, a retreat or a warning stage writes position itself, so a charging Krasue
cannot be dragged at walking speed through her own telegraph. `EnemyBrain` is hashed (state, timers,
locked aim) because the cycle position is part of the fight.

The warning is an event (`TelegraphStarted` / `TelegraphEnded`) whose payload is the footprint the
damage really used — shape, origin, radius, length and angle, see
[`battle-contract.md`](battle-contract.md). Presentation draws exactly that, and nothing in it
re-derives a reach from a def. Resolved damage goes through the existing queue
(`BattleContext.PlayerHitDamage`), so god mode and the HUD are unchanged.

The numbers all live on the `enemy` row ([ADR 0029](../../docs/adr/0029-enemy-telegraphed-attacks.md));
the validator rejects a strong attack with no warning time, a ring with a line length, a drain with
no cadence, a retreat with no speed, a push with no second attack, and a contact-only ghost that
carries attack numbers. The push column and why it exists are in
[ADR 0036](../../docs/adr/0036-krahang-pushes-with-the-gale.md).

While `SweepActive`, `Step` runs only `SweepSystem` and `FramePublishSystem` — no wave, no
steering, no casts, no collision, no damage, no death, no new drops. The ghosts that are left
stand still and are simply still standing when the result is produced (nothing pays them, because
only `DeathSystem` pays a kill), and the player takes no damage. That is the simplest reading of
"the remaining ghosts are removed without dropping anything else".

Nothing about the contract changes: `BattlePhase` has no sweep value, no event type was added, and
the phase is `Running` for the whole sweep — so the Battle scene's director waits for the phase
change it already waits for, and the pickup views (entities the view already draws) move and vanish
on their own. The EditMode presentation test measures `viewsGrownDuringPlay = 0` across a real
sweep. `BattleResult.SurvivedTicks` includes the sweep ticks (a 60 s floor reports 1800 + at most
60 ticks); the HUD clamps its clock to `00:00` and its bar to 1 because the existing presenter
already clamps. See [ADR 0027](../../docs/adr/0027-end-of-floor-sweep.md).

The order is load-bearing and commented as such: steering before the grid, the grid
before collision, collision before the damage that can kill, and the frame last so it
describes the finished tick. `FramePublish` adds nothing to the simulation: since Stage 08b it
reports each caster's own clock (`Caster.CooldownTicks` / `PeriodTicks`) and the one reading a view
cannot make — "ready and only the pool is short" — as `IBattleFrame.Cooldowns`
(see [`battle-contract.md`](battle-contract.md#the-cooldown-list-stage-08b) and
[ADR 0023](../../docs/adr/0023-cooldown-state-on-the-battle-frame.md)).

## 3. Components

`Position`, `Velocity`, `Health`, `Faction`, `PlayerTag`, `EnemyTag`, `Mana`, `Caster`,
`Projectile`, `Status`, `ContactAttack`, `EnemyAttack`, `EnemyBrain`, `Pickup`, `Lifetime`,
`Collider` — all plain
structs with public fields, no references, no EcsLite types inside. Everything a system
needs about a def (move speed, contact damage, cooldown, damage, reach) is **copied onto
the entity at spawn**, so no hot path looks a def up.

Two structural choices worth remembering:

- **One caster entity per loadout entry.** Weapons keep independent cooldowns without the
  player entity holding a collection.
- **Player HP/MP live in components**, never in a mirror field on the context, so the HUD,
  the floor-end rule and the result all read one number.

## 4. Bounds (`BattleArena`)

The disc is radius 40, ghosts walk in from 0.95 × radius, the grid cell is 4 units.
Caps: 512 ghosts, 1024 projectiles, 256 pickups, 2048 queued hits per tick, 2048 entity
slots, 1280 dense slots per component pool, 1024 recycled slots, 1536 dense slots per
filter. These are **memory bounds**, not balance: the pattern Stage 02b set for the
fake's orbit constants. The arena *radius* is the one a player could feel, and Stage 11
owns whether it moves into `data/`.

## 5. Auto-cast

One cast per tick at most: among the casters that are ready **and** affordable, the one
with the shortest base cooldown fires at the nearest ghost; a tie keeps the earliest
loadout entry. Aura / melee resolve on the caster's own tick against everything in reach;
projectile / pierce spawn a shot whose lifetime is `range / projectile_speed` (the only
reading of `range` that makes it a reach rather than a radius). MP is a real cost, so a
build that costs more than its focus income casts less often than its cooldowns allow —
the number the balance pass will be tuning.

## 6. Tick, backlog and pause

- The battle owns **no clock**: no `Time`, no `DateTime`, no accumulator (an architecture
  test forbids all of them in the rule layers).
- **Paused is "not stepped".** An invisible tab stops calling `Step`; nothing accumulates.
- **Backlog is dropped, not replayed**: the Present driver feeds real deltas to
  `FixedTicker` (Stage 01), which hands out at most `MaxStepsPerFrame` (4) steps and counts
  the surplus in `DroppedTicks`. Replaying a stall as 300 steps would be a death sentence
  in a game about not being touched.
- A dropped tick is not simulated, so a replay reproduces the *battle* (seed + ticks +
  inputs), not the wall-clock history of the machine that recorded it.

## 7. Determinism and the state hash

- All randomness is the battle stream (`RandomStreams.DeriveStreamSeed(seed, Battle)`) via
  `IRandomSource`; spawning positions and projectile fan angles are its only consumers.
- `ComputeStateHash` mixes the phases, tick, counters, the per-wave spawn state, and then
  **walks every live entity in entity-index order** (`EcsWorld.GetAllEntities` into a
  preallocated buffer), mixing whichever components each entity carries. Entity-index order
  is a function of the operation sequence alone, so it is stable for a replay and does not
  depend on which pool a system happened to touch.
- Scope is one runtime only (see [`../gotchas/determinism-scope.md`](../gotchas/determinism-scope.md)).

## 8. Allocation discipline

Everything the tick touches is allocated once, in the constructor or in `Init`:
the world and its pools, every filter, the per-tick queues, the candidate/scratch arrays,
the event ring, both frame slots, the spatial grid, and the result's consumable array.
Rules the code follows:

- `foreach` over an `EcsFilter` binds the **struct** enumerator (no boxing); filters with
  `End(denseCapacity)` never resize during a battle.
- Anything that deletes an entity **collects first and deletes after the enumeration**
  (`Collision`, `ProjectileMove`, `Death`, `Pickup`), so no pool access follows a deletion.
- Dead slots are recycled, so `NewEntity` reuses an index instead of growing the world.
- The **one** allocation a battle makes is the `BattleResult` (with its consumable array)
  when the floor ends — ~88 bytes, once. The acceptance benchmark therefore measures
  *inside* a running floor: 0 bytes over 600 timed ticks at the acceptance load.

## 9. Def ids

`BattleDefIds` numbers a def by its **1-based position in its table** (enemy / skill /
item, in `IGameConfig` order = `data/tables/*.csv` file order). `0` means "no def", and
an id that is not in the table resolves to `0` rather than to a guess. The map is built
once per factory and never persisted, so inserting a row anywhere but the end of a table
renumbers ids — safe, because a def id lives only inside one battle's event stream.

## 10. What a kill pays, and what `data/` still does not carry

**A kill pays `enemy.drop_value`** (Stage 07b, [ADR 0025](../../docs/adr/0025-bia-from-battle.md)).
The value is copied onto `EnemyTag` / `WaveSlot` at spawn, `DeathSystem` queues it,
`DropSystem` spawns the pickup with it, and `PickupSystem` adds it to
`BattleContext.CurrencyEarned` and deletes the pickup in the same tick. `BattleResult.CurrencyEarned`
is therefore the sum of collected drops; the flat clear reward is separate
(`economy.clear_reward_bia`, paid by the run's settlement on a win only). The first
values are filler/fast 1, elite 3, boss 10 and clear 8 — starting values for the
Stage 11 balance pass, not a claim.

**Stage 11a closed the elemental-status gap**: a hit applies the `status` row for its
element (`damage_per_tick`, `tick_interval_seconds`, `duration_seconds`, `max_stacks`),
stacks up to the row's cap, and the component's `DefId` is the status row's own id.
[ADR 0028](../../docs/adr/0028-item-effects-and-status-table.md) supersedes ADR 0017 §4.
A charm's passive is applied by `LoadoutResolver` to the player stats the snapshot
already carries, and an offering's use, amount and auto-use threshold come from the
`item` row; both are Stage 11a's [ADR 0028](../../docs/adr/0028-item-effects-and-status-table.md).

`constant.magnet_speed` and `constant.sweep_timeout_seconds` landed in Stage 07c for the **end-of-floor
sweep** only (ADR 0027); during the fight itself a drop is still collected instantaneously inside
`constant.pickup_radius`. The sweep pays the drops a won floor used to lose, so a run's field income
is now the whole floor's drops rather than only the ones the player happened to walk over.

A run now earns bia in battle, so the bot CSV's `currency_earned` and `bia`
columns reflect both sources. The Stage 07b 200-seed before/after picture is in
`docs/stages/07b-report.md`; it is input to Stage 11, not a tuned result.

## 11. Running it

```powershell
# 200 seeds through all five floors: one row per floor in artifacts/balance.csv
pwsh -File tools/sim.ps1 --seeds 200

# the acceptance load (~300 ghosts, ~500 projectiles): per-tick timing + GC
pwsh -File tools/sim.ps1 --mode benchmark

# the same, straight through dotnet (Release build)
dotnet exec lib/Tests~/Sim/bin/Release/net9.0/ProjectSurvive.Sim.dll --mode benchmark
```

The bot (`lib/Tests~/Sim/KiteBot.cs`, `CampBot.cs`) reads a battle frame and returns one
input per tick — no randomness, no state of its own — and plays the camp greedily through
`RunSession`. It reaches a battle only through `IBattleSession`, so it runs against any
factory. `tools/test.ps1` smoke-runs two seeds; `tools/sim.ps1` is where the numbers come
from. The bot's own tuning constants (danger radius, orbit weight, …) live in the sim, not
in `lib/`, and are the bot's skill rather than the game's balance.

## 12. Facts that cost time to learn

- **`WaveCount` is the systems' contract with `BattleContext`.** It must be assigned
  (`= Waves.Length`) or `WaveSpawn` loops zero times and the floor is silently empty. The
  Stage 07 rules tests caught it: a floor with no ghosts times out as a win.
- **A filter enumerator is a lock.** `GetEnumerator()` increments the filter's lock count,
  and an add/remove inside the loop is deferred (`DelayedOp`, 512 slots) until `Dispose`.
  That is why deletions are collected outside the loop.
- **The spatial hash is one tick old when `AutoCast` reads it** (it is rebuilt *after*
  casting), so target liveness must be re-checked — a ghost that died last tick can still
  be in the grid.
- **`EcsWorld.GetAllEntities(ref buffer)` resizes the buffer you pass** if it is too small;
  hold it in a field and size it to the entity capacity.
- **`LoadTestSystem` churn retires its own batch through the death path** (HP to zero, then
  `DeathSystem` does the rest) rather than deleting entities, so a churned ghost is killed,
  rewarded and dropped like any other. The contract suite needs that churn to overflow the
  event ring the way the fake's dummy pump does.
