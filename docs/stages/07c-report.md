# Stage 07c — every drop pays before the floor ends (end-of-floor sweep)

- **Date:** 2026-10-19
- **Prompt:** “ทำ Stage 07c เท่านั้น: กวาดเบี้ยทั้งหมดเข้าหาผู้เล่นก่อนจบชั้น (data และ Simulation)” —
  a win used to end the floor instantly, so drops still on the ground were lost. The drops must drift
  into the player and pay `CurrencyEarned` before the fade. The rule affects the reward, so it lives in
  the simulation, not in a view.
- **Result:** a win now opens a **sweep** instead of ending: `BattlePhase` stays `Running`,
  `SweepSystem` pulls every remaining pickup in at `constant.magnet_speed` and pays it once on
  arrival, and the sweep closes when the last pickup arrives or `constant.sweep_timeout_seconds`
  runs out — at the cap whatever is left is collected at once. A loss never sweeps. Only Simulation
  and `data/` changed: no view code, no contract change, no new event. [ADR 0027](../../docs/adr/0027-end-of-floor-sweep.md).

## 1. What changed

| Area | Change |
| --- | --- |
| `data/` | `constant.magnet_speed` (24 world units/s) and `constant.sweep_timeout_seconds` (2 s); `SchemaVersion` 4 → 5; the validator rejects a non-positive value (`constant.magnet_speed_not_positive`, `constant.sweep_timeout_not_positive`); `gen.ps1` / `validate-data.ps1` / `verify-generated.ps1` green |
| Simulation | `BattleContext` gained the resolved magnet speed, the sweep cap in ticks and the sweep state; `FloorEndSystem` opens the sweep on a win; new `SweepSystem` moves and collects; `EcsBattleSimulation.Step` runs only the sweep and the frame publisher while the sweep is active. A loss still ends immediately; `DevWinNow` still ends immediately (it is “skip this floor”, and the shared contract suite asserts that) |
| Presentation | **no code change.** The pickup views already exist and are drawn from the frame’s entity positions, and the Battle director already leaves when the phase changes — after the sweep |
| HUD | **no code change.** `FramePublishSystem` publishes `HudState.Currency` every tick, so the bia label climbs during the sweep through the existing subscription |

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `data/`: `constant.magnet_speed` + a sweep-time cap in `constant`; bump `SchemaVersion`, `gen`, `validate` | **PASS** | [`constant.csv`](../../data/tables/constant.csv), [`schema.xml`](../../data/Defines/schema.xml), `SchemaVersion = 5` in [`gen.ps1`](../../tools/gen.ps1) and `ConfigBuildMetadata.g.cs`; `gen.ps1` OK, `validate-data.ps1` **0 findings**, `verify-generated.ps1` PASS (`artifacts/stage07c/fast.log`) |
| 2 | On a win (timer or boss dead) the floor sweeps before `Finish`: every remaining pickup moves at `magnet_speed`, is collected on arrival and paid once; the remaining ghosts do not drop again and the player takes no damage; the sweep ends when the pickups are gone or at the cap, and at the cap the rest is collected at once | **PASS** | `A_won_floor_pulls_every_drop_in_before_it_reports`, `The_sweep_cap_collects_what_is_left_so_the_floor_always_ends`, `A_boss_floor_sweeps_after_the_boss_dies`; during the sweep `Step` runs `SweepSystem` + `FramePublishSystem` only, so no steering, cast, death, drop or contact damage can happen |
| 3 | A loss never sweeps; no other rule changes | **PASS** | `A_lost_floor_does_not_sweep` (currency 0, pickup still on the floor, tick frozen); the rest of the battle suite is unchanged and green |
| 4 | `BattlePhase` unchanged (the sweep is still `Running`); the shared contract suite passes **unmodified** against both factories | **PASS** | no Battle contract file (`Domain/Combat/Battle`, `Application`) touched — only `Domain/Config/ConfigTables` and the Infrastructure adapter gained the two new columns; dotnet **525/525**, including `BattleSessionContractTests` over the fake and the ECS factory |
| 5 | Presentation needs no pickup-movement code; the scene waits for the phase change; no view is created during the sweep (`viewsGrownDuringPlay = 0`) | **PASS** | `A_sweep_creates_no_view_and_pays_every_drop_it_pulls_in` (EditMode, real floor + real sweep + real view: `Pool.CreatedAfterWarmup == 0`, paid exactly the drops that were left); live `BattlePlayModeTests` **6/6** |
| 6 | The HUD’s bia moves during the sweep through the existing channel | **PASS** | `Frame.Hud.Currency == result.CurrencyEarned` asserted after the sweep; `BattleHudPresenter` unchanged and already subscribes to that value |
| — | Requested tests: several drops | **PASS** | 5 drops of 1+2+3+4+5 on the ground at the timer → `CurrencyEarned = 15`, `PickupsCollected = 5`, none left |
| — | Not counted twice | **PASS** | a paid pickup is deleted in the paying tick (same rule as `PickupSystem`); the counter and the sum are both asserted |
| — | Cap collects the rest | **PASS** | a drop in the arena’s far corner (farther than `magnet_speed × cap`) is still paid, and the floor ends within `duration + cap + 1` ticks |
| — | Loss does not sweep | **PASS** | `A_lost_floor_does_not_sweep` |
| — | Boss floor sweeps after the boss dies | **PASS** | `A_boss_floor_sweeps_after_the_boss_dies` (phase `Running` with `SweepActive`, then `Won / BossDefeated`, `before + left` paid) |
| — | Step still allocates nothing after warm-up (including sweep ticks) | **PASS** | `A_sweeping_tick_allocates_nothing_after_warm_up` — **0 bytes** over 10 measured sweep ticks; the benchmark still reports `allocatedBytesPerTick=0` |
| — | Benchmark 300 ghosts + 500 projectiles does not regress beyond the report | **PASS (same-machine A/B)** | see §3: with the sweep branch vs a temporarily removed branch, both inside run-to-run noise, 0 bytes/tick |
| — | Seeded replay keeps its hash | **PASS** | `A_recorded_floor_that_sweeps_replays_to_the_same_hash` (1860 ticks, through the sweep); the Stage 08b pin `The_state_hash_of_a_seeded_floor_with_casters_is_unchanged` is **still `3218847520`** because no field was added to `ComputeStateHash` |
| — | `BattleSessionContractTests` passes for both factories | **PASS** | dotnet **525/525**; the shared file is byte-for-byte untouched |
| — | `gen`, `validate`, tests pass | **PASS** | `-Suite Fast` **PASS 10 / FAIL 0 / NOT RUN 3 / WARN 0**; EditMode **157/157** (`artifacts/stage07c/fast.log`) |

## 3. Benchmark (measured, same machine, same session)

`.NET 9.0.13`, Windows `10.0.26300` x64, 16 logical cores, Intel64 Family 6 Model 191 — the Unity
Editor was **open** during the measurement, which is why both arms are slower than the Stage 07
number (0.114 ms / p95 0.126 ms was recorded with the Editor closed):
`artifacts/benchmark.csv`, `artifacts/benchmark-base-*.csv`.

| Arm | avg ms | p95 ms | bytes/tick |
| --- | --- | --- | --- |
| with the sweep branch, run 1 / 2 / 3 | 0.1493 / 0.1325 / 0.1274 | 0.1914 / 0.1580 / 0.1505 | 0 / 0 / 0 |
| sweep branch removed (baseline), run 1 / 2 / 3 | 0.1476 / 0.1374 / 0.1342 | 0.1744 / 0.1848 / 0.1566 | 0 / 0 / 0 |

The two arms overlap; the branch is one boolean per `Step`, so a normal tick costs the same. The
benchmark never reaches a sweep (`floorEndedAtTick=0`), which is exactly the point — a won floor's
sweep is not on the 30 Hz fight path.

## 4. The 200-seed picture changed, and is not tuned

A won floor now pays the drops it used to lose, so the bot earns more bia and its camp policy buys
differently. `artifacts/stage07c/balance-after.csv`:

| Floor | wins / attempts | mean ticks | mean kills |
| --- | --- | --- | --- |
| 1 | 200 / 200 | 1827.7 (1800 + ~28 sweep ticks) | 15.9 |
| 2 | 193 / 200 | 1828.6 | 17.1 |
| 3 | 84 / 193 | 1313.4 | 10.7 |
| 4 | 54 / 84 | 1711.5 | 17.5 |
| 5 | 26 / 54 | 2172.8 | 0.5 |

Runs cleared 26/200 (Stage 07b: 52/200). The extra field income changes what the greedy camp buys,
and the balance consequences belong to Stage 11 — nothing here was tuned.

## 5. Not tested, and why

1. **WebGL build + Playwright: NOT RUN.** No scene, prefab, layout or UI changed, and 07c is not one
   of the gate stages (10/12/14/15). The Full run was made with the Editor open, so the build step
   correctly reported the project lock. The drift is therefore proven through frames and the view
   pool, **not** by a browser screenshot — the one piece of evidence this stage does not have.
2. **Bulk PlayMode is red on the known ambient Input System asserts** (`22/48`, with 26 not passed:
   `IndexOutOfRangeException`, `Cached unprocessed value…`, and cross-test scene pollution). There is
   **no failed assertion of the project's own code**; the classes this stage can affect were re-run
   alone and are green: `BattlePlayModeTests` **6/6**, `SceneFlowPlayModeTests` **5/5**,
   `ResultScenePlayModeTests` **3/3** (the same flake recorded in
   [`docs/testing/test-suites.md`](../testing/test-suites.md) §7).
3. **The sweep is a balance change, not a tuned one** (see §4).
4. **The ghosts are frozen, not deleted.** The prompt allowed either; freezing is the smaller change
   and leaves the kill/drop accounting untouched, but it means a won floor's last frame can show the
   ghosts still standing for up to the cap. Recorded in [ADR 0027](../../docs/adr/0027-end-of-floor-sweep.md).
5. **A drop past the cap is paid instantly, wherever it is.** That is what makes the floor finish; it
   is not a claim that the player saw it fly all the way in.

## 6. Commands actually run

```powershell
powershell -NoProfile -File tools/gen.ps1
powershell -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
powershell -NoProfile -File tools/verify-generated.ps1
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" --no-build
powershell -NoProfile -File tools/test.ps1 -Suite Fast      # artifacts/stage07c/fast.log
powershell -NoProfile -File tools/test.ps1 -Suite Full      # artifacts/stage07c/full.log (see §5.1-2)
powershell -NoProfile -File tools/sim.ps1 --seeds 200 --output artifacts/stage07c/balance-after.csv
powershell -NoProfile -File tools/sim.ps1 --mode benchmark  # artifacts/benchmark.csv + the A/B arms in §3
# per-class PlayMode through the Editor bridge: BattlePlayModeTests 6/6, SceneFlowPlayModeTests 5/5,
# ResultScenePlayModeTests 3/3
```

Unity: `recompile` → `recompile_status` (clean, `compilationFailed: false`; the only warning is the
pre-existing `WebSizeProfiles.cs` obsolete API) → the EditMode suite **157/157**.
