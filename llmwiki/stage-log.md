# Stage log

A one-paragraph-per-stage running log. The full report for each stage lives in
[`../docs/stages/`](../docs/stages/); this file is the fast "what landed, in what
order" view.

| Stage | Landed | What it added | Report |
| --- | --- | --- | --- |
| 00 | 2026-10-13 | repo skeleton, `AGENTS.md`, `knowledge.md`, docs, `tools/doctor.ps1`, `versions.lock.json`, dependency matrix | [`00-report`](../docs/stages/00-report.md) |
| 01 | 2026-10-13 | `lib/` UPM package + six layer asmdefs + vendored EcsLite, the `dotnet` harness, `Shared` primitives (`IRandomSource`/`Xoshiro128`/`RandomStreams`, `IClock`, `FixedTicker`, `Result`/`Error`) | [`01-report`](../docs/stages/01-report.md) |
| 02 | 2026-10-13 | Luban 5.1.0 + protoc 33.6, `data/` (12 tables), generated C#, `IGameConfig` port + adapter, config hash, 4 gate tools, 12 broken fixtures | [`02-report`](../docs/stages/02-report.md) |
| 02b | 2026-10-14 | **Battle contract v1**: `Domain/Combat/Battle` interfaces + `BattleEventRing` + `BattleReplay`, `FakeBattleSession` in Application, `BattleSessionContractTests<TFactory>`; amended later (see the note below and [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md)) so the frame is a **Prev/Cur double buffer** with exactly one implementation | [`02b-report`](../docs/stages/02b-report.md) |
| 03 | 2026-10-14 | **Unity shell + first Web build + Thai text decision**: `UnityProject/` on `6000.3.9f1`/URP referencing `lib/` as a local package; Boot/Title/ThaiTextSpike scenes; 7-probe smoke suite published to the page; Playwright suite on the real WebGL build; [ADR 0008](../docs/adr/0008-thai-text-ui-stack.md) (uGUI+TMP, Static baked font assets) | [`03-report`](../docs/stages/03-report.md) |

| 03b | 2026-10-14 | **WebGL payload budget + size baseline**: [ADR 0011](../docs/adr/0011-webgl-payload-budget-and-size-measurement.md) fixes the payload unit and the three bases a size may be quoted in; `WebSizeProfiles` + `-SizeProfile` make each configuration a value applied in-process and recorded in `build-report.json`; the project's own configuration measured first (60.46 MiB raw, 18.04 MiB gzip, 12.41 MiB Brotli), then stripping High −26.7 % raw and IL2CPP `OptimizeSize` −23.8 % raw (player 60.46 → 33.75 MiB raw, 18.04 → **13.13 MiB gzip**, 12.41 → **9.70 MiB Brotli**); the recommended build browser-verified (Playwright 2/2); per-assembly attribution shows 67.91 % of the `.wasm` is generic sharing + IL2CPP runtime, so dropping a package is the wrong lever | [`03b-report`](../docs/stages/03b-report.md) + [`docs/build/size-baseline.md`](../docs/build/size-baseline.md) |

| 04 | 2026-10-14 | **Prototype data, `data/` only**: 14 pieces (3 bags, 4 arcane weapons one per cast behaviour, 3 charms, 1 tome, 2 offerings, 1 familiar) each merging to tier 3, a real tag vocabulary behind the 5 GDD combo rules, 4 enemies + 2-phase Pob, the 5-floor run with its wave timeline, a 14-row shop pool, 51 localization rows; `item.footprint` column; the **footprint picture format** (`XX.|.XX`, parsed in Domain) per [ADR 0009](../docs/adr/0009-footprint-picture-format.md); rules for item footprint / unknown tag / incomplete merge ladder / unreachable piece / wave spawn window; fixtures 12 → 17 | [`04-report`](../docs/stages/04-report.md) |

| 05 | 2026-10-14 | **Bag rules, no Unity UI yet**: `Domain/Inventory` — `Board` (9×7 with bag/item owner maps), `Chest` (10), `PieceInstance`/`BagPieceInstance`/`ItemInstance` with `DefId` ≠ `InstanceId`, `PieceRef` handles, `Footprint` rotation + normalisation, `Backpack` (state + `Revision`) and `BackpackEditor` (atomic `Move`/`Rotate`/`TransferToChest`/`Merge`/`Recycle`/`Undo`/`Reset`/`SetLocked`, typed `Result` + `InventoryError.*`); `SynergyEvaluator` (adjacency counted once per pair, row **and** column kinds, power decay both ways, bag-area bonus, cooldown floor) and `LoadoutResolver` → the **Battle v1** `CombatLoadoutSnapshot` cached on the backpack revision; MessagePack snapshot undo through a `Contracts` DTO; `constant.weight_speed_percent_per_unit` added ([ADR 0013](../docs/adr/0013-weight-speed-penalty-curve.md)), editing/ownership [ADR 0014](../docs/adr/0014-backpack-editing-undo-and-placement-ownership.md), `SameColumn` kind [ADR 0015](../docs/adr/0015-same-line-synergy-kinds.md); `SchemaVersion` 2 → 3 | [`05-report`](../docs/stages/05-report.md) |

| 06 | 2026-10-14 | **Run, economy, camp shop and scene flow (no UI, still no battle sim)**: `Domain/Run` — `Wallet` (the only place bia changes), `RunFlowState` + `SceneId` joined by `SceneFlowMap`, `RunCommand`/`RunEvent`, the hashable `RunState`, `PurchaseOutcome`/`RefreshOutcome`/`FloorSettlement`, `RunDefaults` (starting ids only) and `RunError`; `Domain/Shop` — `ShopStock` (4 slots, one stock per camp entry, re-roll 5 then +2 per re-roll in the same camp), `ShopSlot` (a rented slot is **emptied**) and `ShopRoller` (weighted, floor-banded, no repeat, price captured at roll time); `Application/Run` — `RunSession` (the only command surface: `StartRun`/`OpenShop`/`Purchase`/`Sell`/`RefreshShop`/`SetSlotLocked`/`StartFloor`/`ApplyBattleResult`/`ReturnToTitle` plus the Stage 05 bag commands, each validating fully before its first mutation), `SceneFlow` (the only place a battle session is created, through `IBattleSessionFactory`) and `ReplayLog` (seed + accepted commands + one `BattleReplay` per floor); the one-payout, rented-slot, desynced-result and composition-root boundaries in [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md); no `data/` change at all | [`06-report`](../docs/stages/06-report.md) |

| 07 | 2026-10-15 | **Battle core (EcsLite, headless) + bot sim**: `lib/Runtime/Simulation/Battle` — `EcsBattleSimulation` + `EcsBattleSessionFactory` implementing the Stage 02b contract, `BattleContext` (pre-resolved wave timeline, per-tick queues, counters), `BattleArena` (structural bounds), 14 components, 14 systems in the GDD's order (`WaveSpawn → LoadTest → PlayerMove → EnemySteer → AutoCast → ProjectileMove → SpatialHash → Collision → DamageStatus → Death → Drop → Pickup → FloorEnd → FramePublish`), a uniform-grid spatial hash, `BattleDefIds`; the frame and the event ring are the **shared** `BattleFrameBuffer`/`BattleEventRing` (single-`IBattleFrame`-implementation test); `lib/Tests~/Sim` grew a kiting bot + greedy camp policy (`tools/sim.ps1 --seeds 200 → artifacts/balance.csv`) and an acceptance-load benchmark; [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) records the tick/backlog/pause policy, the def-id mapping, the auto-cast rule, and the three numbers `data/` does not have yet (status magnitude, `enemy.drop_value`, magnet speed) | [`07-report`](../docs/stages/07-report.md) |

| 08 | 2026-10-15 | **Battle presentation 2.5D, played in a browser**: `Assets/Main/Scenes/Battle.unity` (authored by `ProjectSetup`, build index 3) + `Presentation/Battle/**` (25 files) — `BattleSimulationRunner` (fixed-step accumulator at 30 Hz, capped `MaxStepsPerFrame`, dropped-tick counter, interpolation alpha, `OnApplicationPause` reset) over `BattleFrameSnapshot` (the Prev/Cur copy taken inside the frame that stepped) and `BattleViewScene` (`BattlePlaceholderArt` generated sprites, `EntityViewPool` 1793 views warmed with an `CreatedAfterWarmup` counter, `BattleRenderBridge`, `BattleWorldMap` `(x,y)→(x,0,y)` + authored sorting order, `BattleCameraRig` 50°, `FloatingDamagePool` 48 labels on the visual RNG, HUD state/presenter/view/timeline, `BattleEventPump` with one cursor per channel) ; `Battle/BattleSceneScope` (composition root, `_useDevBattle` fake↔EcsLite switch), `BattleSceneDirector`, `BattleRunSource` (the run's real `CombatLoadoutSnapshot`); `BattleInputAdapter` + `BattleSpeed` 1×/2×; `IBattleSession : IDisposable`; 13 `hud.*` localization rows; [ADR 0018](../docs/adr/0018-battle-camera-projection.md) (**orthographic**, near÷far sprite scale 1.000 vs perspective 0.832, from `artifacts/stage08/camera-projection.json`); `Presentation` still may not name `Core.Simulation` (EditMode test). Evidence: a **Release WebGL player in Chrome plays floor 1 to `Won / SurvivedAllWaves`** — 1800 ticks, 0 dropped ticks, `viewsGrownDuringPlay: 0` of 1793, 0 dropped events, kills 20; 413 xUnit + 48 EditMode (15 new) + 4 PlayMode + 4 Playwright (2 new) green | [`08-report`](../docs/stages/08-report.md) |

| 09 | 2026-10-15 | **ศาลาพัก (camp) + จัดย่าม screen, made usable (09b) and walkable (09c)**: `Assets/Main/Scenes/Camp.unity` (5th scene, `?scene=Camp`) + `Camp/**` + `Presentation/Camp/**` (13 files, `CampBagScreenView` 1415 lines, `CampReport` 713) — a fullscreen translucent overlay over the sala (board / chest `x/10` / incense pot / 4-step instruction scroll / restore+lock-on-restore checkbox / navigation+stats) with drag feedback, synergy lines, click-then-click fallback and Escape-cancel; since 09c the sala is walked with WASD under a follow camera, the shop is **four shelves in the world** (one affordance per slot, `E` or a click, the same `RunSession` command) with the เซียมซี drum and per-shelf lock pins, and the camp starts with the panel closed; `RunSession.PreviewDrop` is a **dry run of the same `BackpackEditor` predicates** the accepted command asks, `SetBagLocked` + the camp read model (`EvaluateLayout`/`BagWeight`/`BagPenalty`/…) added, `Domain/Run/BagDrop` extracted (+81 localization rows, 145 total); two documents published for the browser (`__projectSurviveCamp`, `__projectSurviveCampGeometry`) with every cell/slot/station as a **centre** (a published corner is outside its own rect); [ADR 0019](../docs/adr/0019-camp-screen-read-model-and-gesture-preview.md) | [`09-report`](../docs/stages/09-report.md) + [`09b`](../docs/stages/09b-report.md) + [`09c`](../docs/stages/09c-report.md) |

| 09d | 2026-10-15 | **Thai text sharpness, measured rather than eyeballed**: the font every canvas actually uses is reported from the loaded assets (`Sarabun-Regular SDF` — 90 pt sampling, 9 px padding, **SDFAA**, Static, 1024², 182 chars) and `THSarabunPSK SDF` is shown to be **orphaned** (no scene references it, 0 characters baked) while being the **largest asset in the Web build** (4,220,916 B packed); all four canvases confirmed `ScaleWithScreenSize` 1920×1080 match 0.5, scene labels ≥ 22 px (Title 28, Battle 22, Camp 22); the จัดย่าม label measured **21 px of ink at 1920×1080** in the real player and 14 px at 1280×720 (the scaler's 0.667), 27 px at deviceScaleFactor 2; THSarabunPSK measured **31 % shorter** than Sarabun at the same size (31 px needed for parity) with three comparison images; 13/13 Playwright screenshots (17 PNGs) at 1920×1080/1280×720 × deviceScaleFactor 1/2; two new tools (`tools/font-metrics.py`, `tools/screen-text-metrics.py`) and `tests/web/scripts/collect-geometry.mjs` | [`09d-report`](../docs/stages/09d-report.md) |

## Notes carried forward

- **The bulk PlayMode suite fails on ambient Input System log-asserts, while every test passes
alone** (2026-10-15, Stage 09d). In a freshly launched Editor, three whole-suite runs gave
  **10 passed / 10 failed** with three different duration sets (so they were real runs, not the
  `run_tests` cache — see the next note): seven failures are `[Exception] IndexOutOfRangeException`
  raised inside `InputSystem.InputManagerStateMonitors.AddStateChangeMonitor` via
  `InputSystemUIInputModule.OnEnable`, three are `[Error] Cached unprocessed value unexpectedly became
  outdated` from `NativeInputRuntime`, and **none is a failed assertion** — they are `LogAssert`
  failures on ambient messages. The test that owns the 22 px floor passed **1/1** when run on its own,
  and Stage 09's session reported all 24 PlayMode tests green on the same code. Run a failing-looking
  suite test-by-test before believing it, and log the whole-suite result as unsolved rather than
  tidying it.
- **`run_tests` can answer with the *previous* run's results** (2026-10-15, Stage 09d). A
  `filter_type = class` call returned the whole suite's cached result verbatim (identical per-test
  durations), and an `async_tests = true` call right after a sync attempt returned a run that had
  already completed. The tell is always the durations: compare them against the run you think you
  started before quoting a number.
- **A `data/` edit is not in the build until `gen.ps1` runs** (2026-10-15). Stage 09 added 81
  localization rows to `data/localization/localization.csv` and never regenerated: the tracked
  `ConfigBuildMetadata.g.cs` hash no longer matched the tables, `verify-generated` failed on
  `tblocalization.bytes` in both `lib/Tests~/data` and `Resources/Config`, and the **first browser
  evidence of the stage described a player with 64 localization rows instead of 145** — the camp
  screen's own labels were not in it. The pinned count in `ConfigLoadTests` then caught the rest: it
  said 144 while `data/` held 145 (the pin was corrected to the real number, and that is what a pin is
  for). The fix is a regeneration and a rebuild, so the cost is a stage's worth of browser evidence,
  not a diff. Run `tools/gen.ps1` and `tools/verify-generated.ps1` before any build or browser run.
- **Unity's default desktop WebGL template pins the canvas at 960×600** (2026-10-15). The desktop
  branch of the template writes `canvas.style.width/height = 960px/600px`, so the player renders
  960×600 in a letterbox on every desktop browser, a 1920×1080 *viewport* changes nothing (measured:
  the report stayed 960×600 and republished nothing) and every browser measurement in Stages 03–09 was
  taken in that configuration. What a player can change is the canvas **element** — the template's own
  fullscreen button and a HiDPI backing store — and the camp follows that within one publish
  (960×600 → 1280×720 → 1920×1080 → back). A window-filling template is a small change and a
  project-wide re-measurement ([09-report §6](../docs/stages/09-report.md)).
- **A gesture is not observable while it is live** (2026-10-15). `CampSceneDirector.Publish` returns
  before writing the state document when a drag is active (the document is hand-rolled JSON, and a
  per-pointer-move rebuild is a hot path by the GDD's own list). A browser test waited 15 s for
  `dragging: true` and timed out; the camp's tests now assert the *release* counters. The same freeze
  is why a gesture's interior can only be checked in PlayMode, where the presenter is reachable.
- **Two measurement flakes, reduced but not explained** (2026-10-15). A click aimed at a small panel
  control ~300 ms after a canvas resize was swallowed in 2 of 4 runs (0 of 5 with a one-second wait,
  which the resolution test now takes). And one full PlayMode run failed with *clicking จัดย่าม did not
  open the panel* (0.26 s in) while three runs after passed; the fixture now forces the canvas layout
  (`Canvas.ForceUpdateCanvases()`) before reading the button's rectangle. Both are recorded in
  09-report §6 rather than tidied away.

- **A test-assembly compile error makes a test run report the *previous* assembly's results as
  passing** (2026-10-15). `run_tests` returned `14 passed, success: true` while the new test had a
  `CS0117` — Unity had run the stale compiled assembly. The count was the only tell. Never read a
  suite verdict without checking that the test you just added is in it (or that
  `compilationFailed` is false).
- **A size-profile build poisons the next one** (2026-10-15). `WebSizeProfiles.ApplyAndPersist`
  *persists* `exceptionSupport: None` + stripping High, and a later `tools/build-web.ps1
  -Configuration Release` inherits them: the player halts in the boot scene with `Uncaught
  undefined` (no message — that is what exception support off looks like) while the build reports
  `0 errors`. Stage 03's browser gate was a Development build and Stage 03b only measured bytes,
  so a Release player had never been opened in a browser before Stage 08 — where it cost an hour.
  Read `build-report.json → sizeSettings` before trusting a build's configuration:
  [`gotchas/webgl-size-profile-settings-residue.md`](gotchas/webgl-size-profile-settings-residue.md).
- **A scene must publish its own name** (`__projectSurviveScene`), or a browser test sees a boot
  failure instead of a missing assertion. `SceneBoot`'s `?scene=` allow-list and the scene marker
  are two halves of the same feature; Stage 08 needed both for `Battle`.
- **Re-running `ProjectSetup`'s scene-authoring step re-creates the scenes**, so `EditorSceneManager`
  assigns fresh fileIDs and `Title.unity`/`ThaiTextSpike.unity` show a ~520-line diff that means
  nothing. Commit it with that said, or change the step to patch instead of recreate.
- **Stage 02b was amended after Stage 04** (2026-10-14) to give `IBattleFrame` a
  Prev/Cur pair (`Entities` + `PreviousEntities`) and to make `BattleFrameBuffer` a
  real double buffer — [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md).
  The trigger was Stage 08's "interpolate Prev → Cur ด้วย alpha" while reading only
  `IBattleFrame`, which a single array cannot satisfy. Contract change, fake, tests
  and GDD DoD lines moved together; no gameplay number changed.
- Stage 02b is an insertion between the original Stage 02 and Stage 03. It does
  not change `data/` and does not open Unity.
- **Stage 05 is the first gameplay rule set**: the bag rules and the combo rules exist and
  are measured (105 dotnet cases, 90.92 % of the backpack rule set, 9 EditMode cases).
  The shop/economy flow and the run/scene flow followed in Stage 06; the battle simulation
  is still absent — the *contract* it implements has existed since Stage 02b.
- **Stage 06 is the run, and still no battle**: the economy and the phase machine are real
  and measured (42 dotnet cases over the real tables + 2 architecture tests + 4 EditMode
  cases over the shipped `Resources` tables). What still does not exist: the battle
  *simulation* (Stage 07), any Camp/Battle/Result scene or UI (Stages 08–10), and a
  composition-root binding for the run — the last one deliberate
  ([ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md)), so the Stage
  02b fake cannot become the shipped battle by accident.
- **Stage 07 is the battle, and the balance harness with it**: the EcsLite session passes every case of
  the Stage 02b contract suite unchanged (413 dotnet cases in total), runs with no scene, and measures
  **0 bytes allocated per tick** at ~300 ghosts + ~500 projectiles on .NET (avg 0.114 ms, p95 0.126 ms per
  30 Hz tick, Release, machine quoted in the report). Three prototype gaps surfaced as *data* gaps rather than code decisions —
  no `status` table, no `enemy.drop_value`, no `constant.magnet_speed` — and because a run earns no bia
  inside a battle, the 200-seed balance run (floor 1 200/200, floor 2 198/200, floor 3 91/198, floor 4
  71/91, floor 5 71/71) describes the tables *with no way to buy anything after the first camp*, which
  is the first thing Stage 11 has to fix ([ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md)).
- Determinism is guaranteed within one runtime only (see the scope gotcha).
- `lib/` is now compiled twice: by `dotnet` (lib/Tests~) and by Unity as the local
  package `com.projectsurvive.core`. A change to a rule-engine file must keep both
  green; `Assets/csc.rsp` exists only to keep the Unity compile quiet about the
  nullable annotations the `dotnet` build already enables.
- **Stage 04 is data only.** No rule, system or scene was added; `lib/` changed only in
  `Domain/Config` (the footprint parser and `ItemConfig.Footprint`), the Infrastructure
  adapter, the validator and tests. `data/` is now the canonical description of the
  prototype's content, so a balance change is a CSV edit, a regeneration and a diff.
- **Item-local effects are still unmodelled** (charm passive size, offering heal/bia
  and uses) — [ADR 0010](../docs/adr/0010-item-local-effects-deferred.md). The numbers
  must land in `data/` with their consumer, never in code.
- **Size is now a measured, defined quantity** ([ADR 0011](../docs/adr/0011-webgl-payload-budget-and-size-measurement.md)):
  the gate (`< 30 MiB` gzip) and the target (`< 15 MiB` Brotli) are met with 5.30 MiB of Brotli
  headroom (17 % on the project's default configuration), and any new size claim must name its
  basis. The "before" player Stage 03 left behind (48.81 MiB) turned out to carry no record of its
  settings and is 12.2 MiB of `.wasm` below the configuration the project actually has, so it is out
  of the table. Only `-SizeProfile` CLI builds count — a
  Build-Settings click rewrites player settings and the shared IL2CPP tree. Nothing yet *enforces*
  the budget with content landing from Stage 04 on; a CI size gate on `measure-web-size.mjs` is the
  follow-up.
- Nothing verifies behavior in a *player* except the browser test. Two Stage 03 bugs
  (stripped DI constructors, emptied font tables) were invisible to Mono-based
  EditMode/PlayMode tests and only the real build caught them — `tests/web/` is the
  gate for anything IL2CPP-, AOT- or asset-serialization-sensitive.
- **Process change (2026-10-15, not a Stage): two suites, and a layout check in the Editor.**
  `tools/test.ps1 -Suite Fast|Full` splits the same tests by how often they need to run — Fast is
  **72–77 s warm / 229 s cold** (dotnet + Sim + data gates + EditMode); Full adds PlayMode, a WebGL
  Development build (269 s cold / 16.5 s warm) and Playwright, and its run with the Editor closed was
  **204.5 s, 11 PASS / 0 FAIL / 2 NOT RUN**. `ProjectSurvive/Tools/1. Capture UI` renders Camp and
  Battle at 1920×1080 and 1280×720 in ~1 s and fails on overlapping panels, so a layout no longer
  needs a Web build. Tests
  514 → 522, each one mapped in [`docs/testing/test-suites.md`](../docs/testing/test-suites.md);
  [`process-fast-full-report`](../docs/stages/process-fast-full-report.md).
