# Stage 08b — the frame carries each loadout entry's cooldown (contract + Simulation only)

- **Stage:** 08b (a follow-up to Stage 08, landed after 10d — the next HUD needs it).
- **Date:** 2026-10-17
- **Decision record:** [ADR 0023](../adr/0023-cooldown-state-on-the-battle-frame.md)
- **Scope, from the brief:** *สัญญาและ Simulation เท่านั้น ยังไม่แตะ UI*. No file under
  `UnityProject/Assets/Main/Scripts/Presentation/` was touched, and no scene, prefab or localization
  row changed.

---

## 1. What was delivered

The next HUD has to show, on each piece laid out on the board, how much of its cooldown is left. The
brief is explicit that a View must not guess that from its own clock, so the data had to reach
Presentation through the contract it is allowed to read — `IBattleFrame` — and the contract had no such
data. Both halves of the gap are closed:

1. **The loadout now maps back to the board piece.** `WeaponLoadout` and `SummonLoadout` gained
   `InstanceId`, set by `LoadoutResolver` from `ItemInstance.InstanceId` (`0` =
   `LoadoutCooldown.NoInstance`, for a hand-built loadout). Without it a frame could say "the second
   weapon is nearly ready" but not which item to draw it on.
2. **The frame carries the cooldowns.** `IBattleFrame.Cooldowns` is a `ReadOnlySpan<LoadoutCooldown>`
   in **loadout order** (weapons as `Weapons` lists them, then familiars as `Summons` lists them),
   one entry per loadout entry: `{ InstanceId, RemainingTicks, PeriodTicks, Flags }`, with
   `LoadoutCooldownFlags.ManaShort` the only flag. Ticks, not seconds — the simulation's clock is
   `BattleConstants.TickRate`, and a view divides for seconds or for a progress bar.
3. **Fixed capacity, no per-tick allocation, entities' lifetime.** Bound:
   `BattleConstants.MaxLoadoutEntries = 64` (above the board's 9 × 7 = 63 cells, so a real bag cannot
   be truncated). The list is written into the **unpublished slot** of the one `BattleFrameBuffer` and
   swapped by `Publish`, so a reader holding a frame never sees a half-written list; the span is valid
   until the next `Step`/`Submit`, exactly like `Entities`.
4. **One implementation, both ends.** `BattleFrameBuffer` remains the only `IBattleFrame`. The EcsLite
   simulation publishes from the `Caster` components it already had; the fake manufactures a
   predictable cycle. No `IBattleFrame` was implemented anywhere else, and the reflection test that
   says so is unchanged and green.
5. **Simulation reports, and changes nothing.** `FramePublishSystem` reads
   `Caster.CooldownTicks` / `Caster.PeriodTicks` / `Caster.MpCost` and `Mana.Mp`; no cooldown rule was
   added, no combat outcome moved. `Caster.InstanceId` is deliberately **not** mixed into
   `ComputeStateHash`, and the seeded hash is pinned by a test whose value was recorded on the
   **pre-change build** (see §4).
6. **Fake dev values.** The fake runs each entry down one tick per tick and resets it when it could
   cast, gated by MP the same way the real rule is. Periods come from the loadout's resolved
   cooldowns; only the progress through them is the fake's own, so the HUD can be developed against a
   predictable cycle without the fake becoming a second battle.
7. **ADR 0023** (the contract change, the reasons, the alternatives rejected: a View-side clock, a flag
   on `RenderEntity`, a separate port) and `llmwiki/systems/battle-contract.md` §6 (a new
   *The cooldown list (Stage 08b)* subsection, the buffer's swap diagram and its bullet list).

The MP flag deserves one sentence of its own, because it is the only value on the frame that is
*derived* rather than read: `ManaShort` is "the cooldown has elapsed **and** the pool cannot pay the
cast's cost", compared against `HudState.Mp`. It deliberately says nothing about whether a target is in
reach or which weapon auto-cast would choose — those stay inside the simulation, and a HUD showing
them would be second-guessing the rule instead of reflecting it.

## 2. Files that changed

| File | What |
| --- | --- |
| `lib/Runtime/Domain/Combat/Battle/LoadoutCooldown.cs` (+ `.meta`) | **new** — the struct, its flags word, `NoInstance`, and why it is on the frame |
| `lib/Runtime/Domain/Combat/Battle/IBattleFrame.cs` | `+ Cooldowns` (loadout order, entities' lifetime) |
| `lib/Runtime/Domain/Combat/Battle/BattleFrameBuffer.cs` | a bounded cooldown list in the unpublished slot, swapped by `Publish`; `AddCooldown`, `CooldownCount`; `BeginFrame` resets the write cursor |
| `lib/Runtime/Domain/Combat/Battle/BattleConstants.cs` | `+ MaxLoadoutEntries = 64` |
| `lib/Runtime/Domain/Combat/Battle/CombatLoadout.cs` | `+ InstanceId` on `WeaponLoadout` / `SummonLoadout` |
| `lib/Runtime/Domain/Inventory/LoadoutResolver.cs` | sets `InstanceId` from the piece it is resolving |
| `lib/Runtime/Simulation/Battle/BattleComponents.cs` | `Caster.InstanceId` (and why it is not hashed) |
| `lib/Runtime/Simulation/Battle/BattleSpawner.cs` | `SpawnCaster` carries the instance id |
| `lib/Runtime/Simulation/Battle/EcsBattleSimulation.cs` | `BuildCasters` passes the loadout's instance id |
| `lib/Runtime/Simulation/Battle/Systems/FramePublishSystem.cs` | `PublishCooldowns` — reads the casters, adds the MP-short reading |
| `lib/Runtime/Application/Battle/Fake/FakeBattleSession.cs` | the manufactured cycle: `BuildCasters`, `AdvanceCooldowns`, the publish, `Ticks` |
| `lib/Tests~/Core.Tests/Battle/BattleSessionContractTests.cs` | `CreateCasterSetup` + the four shared cases |
| `lib/Tests~/Core.Tests/Battle/BattleTestFixtures.cs` | the caster fixture: the two skill ids, their instance ids, `Weapon()`/`Summon()` built from the real `skill` rows |
| `lib/Tests~/Core.Tests/Battle/{Ecs,Fake}BattleSessionContractTests.cs` | the seeded-hash pin, per factory |
| `docs/adr/0023-cooldown-state-on-the-battle-frame.md` | **new** |
| `llmwiki/systems/battle-contract.md`, `llmwiki/index.md`, `llmwiki/stage-log.md`, `knowledge.md` | the contract note, the systems row, the stage row, the carried-forward paragraph and the test count |
| `docs/architecture/overview.md`, `docs/testing/test-suites.md` | the §4b bullet and the suite table's counts |

## 3. Commands run (and what they said)

```
# the loop, while working (lib/Tests~):
dotnet build ProjectSurvive.Core.sln -c Debug --nologo -v minimal     # Build succeeded.
dotnet test  ProjectSurvive.Core.sln -c Debug --nologo -v minimal     # 478 passed, 0 failed (×6)
dotnet test  ... --filter "FullyQualifiedName~FakeBattleSessionContractTests"   # 23 passed

# the project's own gate (Editor open, connected), from the repo root:
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1    # Fast
  PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0 — 147.1 s      (artifacts/stage08b/fast-suite.log)

# Unity, through the Editor that was launched for this stage:
unity-cli recompile        -> up_to_date (package imported at launch, 0 errors)
unity-cli console          -> 0 error, 2 pre-existing warnings
unity-cli run_tests editor -> 130/130 passed
```

The two warnings are the ones every stage since 03 has carried (the Input Manager deprecation notice
and `WebSizeProfiles.cs(123,19)` `CS0618`); neither is new and neither is from this stage.

## 4. Results, and where the evidence is

| Evidence | Where |
| --- | --- |
| Fast suite, verbatim: **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0**, 147.1 s, `dotnet test` **478 passed**, `unity: EditMode` **130/130** | `artifacts/stage08b/fast-suite.log`, `-summary.md`, `-summary.json` |
| The xUnit run itself: `<Counters total="478" executed="478" passed="478" failed="0" ...>` | `artifacts/stage08b/core-tests.trx` |
| Every new case is **in** that run (1 architecture + 2 + 2 + 2 + 2 + 2 hits, 0 failed) — the check the Stage 08 stale-assembly lesson demands | the same `.trx`, grepped by test name |
| EditMode 130/130 in the Editor | the `run_tests` result of this session |

**The state hash did not move, and this was measured rather than argued.** The stage's own requirement
is that the seeded hash is identical before and after, so the value was recorded on the *pre-change*
code: a `git worktree add ../ps-pin-08b HEAD` (a clean checkout of `HEAD`, with `lib/Tests~/data` copied
in), the caster fixture and the two pin tests placed there with the new API removed, and
`dotnet test --filter …The_state_hash_of_a_seeded_floor_with_casters_is_unchanged` run in it. It
reported `Actual: 3218847520` (EcsLite, with a real weapon and familiar casting for 300 ticks, so the
hash covers the code this stage touched) and `Actual: 2633275827` (fake). Those two literals are now
asserted in the working tree, where the same run prints exactly the same two numbers — 300 ticks of a
real fight behind the new publish path, byte-identical. The worktree was removed afterwards
(`git worktree list` shows only the main tree).

**The new contract cases, and what each one pins** (all four run against *both* factories, which is what
makes the fake and the real session answer the same sentence):

| Case | What it asserts |
| --- | --- |
| `The_frames_cooldowns_are_the_loadout_entries_with_the_board_pieces_they_came_from` | `Cooldowns.Length == Weapons.Count + Summons.Count`, and entry *i*'s `InstanceId` is the loadout's own — weapons first, then familiars |
| `A_cooldown_runs_down_one_tick_at_a_time_and_resets_to_its_full_period` | over 90 consecutive frames: `PeriodTicks` is the resolved cooldown in ticks, `RemainingTicks ∈ [0, Period]`, every step down is exactly 1, the only step up is a reset to the full period, and each entry really did reset (so the case cannot pass vacuously) |
| `A_ready_caster_the_player_cannot_afford_is_flagged_mana_short` | with a pool below the cheapest cost in the table, every entry sits at `0` and is flagged, and the flag is exactly `IsReady && MpCost > Hud.Mp`; with a pool that pays, the flag is never raised |
| `Step_with_a_caster_loadout_does_not_allocate_after_warm_up` | 200 warm-up ticks then 200 measured: **0 bytes** with a caster loadout (the pre-existing allocation case runs a loadout with no casters, so this is the one that covers the publish path) |
| `The_state_hash_of_a_seeded_floor_with_casters_is_unchanged` (per factory) | the recorded pre-change literal, above |

Two behaviours the cases encode are worth naming, because a HUD author would get them wrong otherwise:

- **A tick is atomic, so a cast reads `1 → Period`, not `1 → 0 → Period`.** `TickCooldowns` reaches zero
  and the cast that resets it happen in the same tick, and the frame is published after both. `0` is
  what a **blocked** entry shows (no target, or no MP). The first draft of the test asserted `1 → 0`,
  which failed on both factories with `Expected: 0, Actual: 27` — the code was right and the test's
  model of a tick was wrong; that is now written down in the case's own remarks and in
  `llmwiki/index.md`'s gotcha row.
- **Reset is only ever *to* the full period.** `0 → 1` or `3 → 27` would be a bug the walk catches.

## 5. Not tested, and why

- **No UI, by scope.** Nothing reads `Cooldowns` yet, so there is no view-level test: no assertion that
  a cooldown *looks* right on a board item, no PlayMode/browser check, and no scene or prefab change.
  That belongs to the stage that builds the HUD, and it will be the first consumer of both the span and
  `InstanceId`.
- **`MaxLoadoutEntries` is unreachable in practice** (a bag cannot hold 64 casters on a 63-cell board),
  so the truncation branch has no test. It is the same policy `MaxRenderEntities` has, and the
  bound's own justification is written on the constant.
- **No browser or WebGL run.** This stage changes no Unity-side asset and no player behaviour; the
  contract/number changes are covered by the dotnet suite and the Editor's compile + EditMode run. The
  Full suite (WebGL + Playwright) was not requested and is reported **NOT RUN** rather than assumed.
- **PlayMode was not run** (it is a Full-suite step). EditMode 130/130 plus a clean compile is what
  this stage's surface needs; the previously-known PlayMode suite-level flake (the Input System's
  ambient-log asserts) is untouched by it and is documented in the Stage 10/10d reports.

## 6. Blockers, risks, and one thing left unexplained

- **One non-reproducible allocation reading.** On the first full-suite run after the fixture changes,
  `FakeBattleSessionContractTests.Step_with_a_caster_loadout_does_not_allocate_after_warm_up` failed
  once with `Actual: 5480` bytes over 200 ticks. It did not reproduce in **ten** subsequent runs (five
  back-to-back full-suite runs immediately after, then the Fast suite's own `dotnet test`), and a
  temporary probe measuring eight windows of 25 ticks in the same test reported `0, 0, 0, 0, 0, 0, 0, 0`
  (that probe was removed). A per-thread allocation counter can include runtime work on the measuring
  thread — JIT/tier-1 promotion and OSR of the loop are the candidates — and the fake's own step path
  measured zero in every other run. The assertion is unchanged and the case is in the accepted suite;
  it is named here rather than hidden, and the shape is the one the pre-existing allocation case uses
  (which has been stable since Stage 07's benchmark). If it recurs, the next step is to measure with
  `DOTNET_TieredCompilation=0` to separate the runtime from the code.
- **The instance id is only as good as the resolver.** `InstanceId` is the board piece's id at the
  moment the loadout was resolved; if a floor's snapshot were ever reused across a bag edit (it is
  cached on `Backpack.Revision`, so it is not), a HUD would bind a cooldown to a moved piece. Same
  lifetime question as every other field of `CombatLoadoutSnapshot`, not new here.
- **A future `ManaShort` reader must not read it as "will cast".** Documented on the flag, in ADR 0023
  and in the wiki, because it is the one field whose obvious reading is wrong.

## 7. Acceptance, item by item (from the brief)

| # | Acceptance | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `CombatLoadoutSnapshot` maps each `WeaponLoadout`/`SummonLoadout` back to its board piece, added if absent (it was absent) | **PASS** | `InstanceId` on both types, set by `LoadoutResolver`; asserted per entry by `The_frames_cooldowns_are_the_loadout_entries_with_the_board_pieces_they_came_from` (both factories) |
| 2 | `IBattleFrame` gains remaining ticks, full ticks and an MP-blocked flag per loadout entry; fixed capacity in `BattleConstants`; no allocation per tick; entities' span lifetime; `BattleFrameBuffer` still the only implementation and both ends write through it | **PASS** | `LoadoutCooldown` + `Cooldowns`; `MaxLoadoutEntries = 64`; `Step_with_a_caster_loadout_does_not_allocate_after_warm_up` = 0 bytes; `Battle_frame_has_exactly_one_implementation` green in the 478 |
| 3 | Simulation writes the values from existing caster components, no new cooldown rule, no change to any fight — proven by the seeded state hash | **PASS** | `FramePublishSystem.PublishCooldowns` reads `Caster`/`Mana` only; the pin was recorded on the pre-change build in a clean worktree (`3218847520` Ecs / `2633275827` fake) and asserts the same values now |
| 4 | The fake produces predictable values to develop the UI against | **PASS** | `BuildCasters`/`AdvanceCooldowns`: one tick per tick, reset on a cast it can afford, periods from the loadout; the same four shared cases pass against it |
| 5 | A short ADR (the change, why, the alternatives not chosen) and an updated `llmwiki/systems/battle-contract.md` | **PASS** | [`docs/adr/0023`](../adr/0023-cooldown-state-on-the-battle-frame.md); the wiki's §6 cooldown subsection (plus `index.md`, `stage-log.md`, `knowledge.md`) |
| T1 | Contract tests shared with both factories: entry count = loadout count | **PASS** | `The_frames_cooldowns_are_the_loadout_entries_…`, 2 cases, 0 failed in the `.trx` |
| T2 | Remaining ticks fall by one per tick to 0 and reset when the entry casts | **PASS** | `A_cooldown_runs_down_one_tick_at_a_time_and_resets_to_its_full_period`, 2 cases, 0 failed (the `1 → Period` reading in §4) |
| T3 | The MP flag goes up when MP is short | **PASS** | `A_ready_caster_the_player_cannot_afford_is_flagged_mana_short`, 2 cases, 0 failed |
| T4 | `Step` does not allocate | **PASS** | `Step_does_not_allocate_after_warm_up` (pre-existing) and `Step_with_a_caster_loadout_does_not_allocate_after_warm_up`, both 0 bytes in the accepted run |
| T5 | The state hash of the original seed is unchanged before/after | **PASS** | the per-factory pins against the pre-change literals |
| A1 | `dotnet test` passes (Fast) | **PASS** | 478/478 in `artifacts/stage08b/core-tests.trx`; `tools/test.ps1` Fast **PASS 10 \| FAIL 0 \| NOT RUN 3** |
| A2 | An architecture test confirms `IBattleFrame` has a single implementation | **PASS** | `ArchitectureTests.Battle_frame_has_exactly_one_implementation` — present once in the `.trx`, 0 failed |
| A3 | A short PASS/FAIL/NOT RUN report | **PASS** | this file |

**NOT RUN (named, not assumed):** Unity PlayMode, the WebGL build and the Playwright suite — all three
are Full-suite steps and none is requested by this stage's acceptance. The Fast suite reports them as
NOT RUN with that reason in `artifacts/stage08b/fast-suite-summary.md`.

**Stage verdict: PASS** on every acceptance item. The one non-reproducible reading in §6 is reported as
a limitation rather than absorbed into the result.
