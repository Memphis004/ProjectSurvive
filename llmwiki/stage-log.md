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

| 09e | 2026-10-16 | **SceneCamp UI, three layout/compositing fixes (no Core, no `data/`, no WebGL build)**: the จัดย่าม overlay is rebuilt in `ProjectSetup` from canvas-edge anchors — the panel's foot is a `Stats` strip that carries only the controls the status bar does not (`กลับ` / `ไปชั้นถัดไป` no longer sit on เบี้ย · น้ำหนัก · สมาธิ · HP/MP, which are drawn **once**, on the bar), the incense pot moved into the column between the panel and the bar's depart button, and Board/ChestPanel render at **0.92** alpha with a tested floor (`UiLayoutCheck.FindOpacityViolations`); a cell's item name is primary and a bag's name is secondary and **hidden** when an item's rectangle reaches it (`LayoutPieceLabels`), measured in PlayMode over real pieces. The tooltip is a **second root canvas at sorting order 100** (documented as *"Canvas' order within a sorting layer"* in the installed engine), no `GraphicRaycaster`, `raycastTarget` off, 0.95 background, placed by the pure `CampTooltipPlacement.Place` (flips at the edges, pinned when larger than the canvas, never under the pointer) and split into the five lines the stage asks for, each hidden when empty. Synergy marks are chosen from the rule's own `synergy.kind`: **a line only for the tome→weapon same-line rule**, a corner **badge** for the bag-area bonus (never a line from a bag), and nothing permanent for the three adjacency rules (hover/drag highlight only), from Core's unchanged `ActiveSynergy`. Evidence: `artifacts/ui/` grew to **8 images** including a third `camp-tooltip-*` state whose draw order is proved **in pixels** (the overlay off/on comparison), the capture report now prints every region's **alpha**, 75/75 EditMode (+10) and 11/11 `CampBagDragPlayModeTests` (+3) green | [`09e-report`](../docs/stages/09e-report.md) |

| 09f | 2026-10-16 | **A bag move or turn carries the items standing on it (no `data/` content, no new system)**: the Stage 05 rule (a bag may not move while an item stands on it, refused whole) is replaced for the two commands that stay on the board by a **rigid assembly** — `FootprintRotation.Rotate(shape, rotation, out CellOffset shift)` now publishes the translation its re-normalisation removed, and `Backpack.CollectRiders` maps every rider cell through that same map, so a pure move is the zero-turn case of one function and the chiral `bag.wicker_basket_l` is the case that fails the naive "translate the item by the bag's delta" version ([ADR 0020](../docs/adr/0020-bag-moves-carry-the-items-on-them.md) supersedes ADR 0014 §5); a rider is an item whose **every** cell lies on that bag, an item across two bags does not ride and a move that would leave it uncovered is refused whole with the new, separately localized `inventory.item_spans_bags` (146 localization rows), while `inventory.item_stranded` narrows to *taking a bag off the board with items on it* (an item can never enter the chest). `BackpackEditor.CanPlace` fills the carry plan as it answers and the accepted command applies exactly that plan (one predicate, so preview and command cannot land an item in two places), `PlaceAssembly` lifts the bag and all riders before putting any down and bumps `Revision` **once** (one gesture = one undo step = one combo recompute). The screen draws what the stage asked for (*"แสดงเงาของไอเทมที่ติดไปที่ตำแหน่งใหม่"*) from the run's plan: `CampCarryGhost` + a pooled ghost box per rider, and a 2 000-move same-cell drag allocates **0 bytes**. Evidence: 16 new tests (dotnet **433**, EditMode **79/79**, the Camp PlayMode fixture **12/12** alone incl. a real pointer drag), the bulk PlayMode suite still 12/28 with **no failed assertion of ours** (12 `IndexOutOfRangeException` + 4 `Cached unprocessed` from the Input System's own ambient log asserts), a live probe (`BAG Bag#1 … at 5,4 \| ITEM Item#2 … at 5,4`), and an A/B capture whose item blob `#bba386` is **one 70×74 px cell before and after**, moved by exactly the 5,4 cell delta, with every differing pixel inside the board rect | [`09f-report`](../docs/stages/09f-report.md) |

| 09g | 2026-10-16 | **The upgrade-choice window on SceneCamp, and with it the second half of 09f (the engine)**: a drop of one piece onto another with the same id and tier asks `RunSession.OfferMerge`, and when the offer has choices the screen opens a **modal** built at runtime by `CampBagScreenView` from one published model (`CampUiState.MergeChooser` → `CampMergeChooserModel` / `CampMergeChoiceModel`) — a full-screen dim layer at 0.72 that swallows pointer events (the physical drag lock), the window 1560×600 centred on the canvas, three cards 328×400 laid out horizontally, a left panel carrying the **same five lines the tooltip publishes** (one producer, `CollectSourceLines`, so "เนื้อหาเดียวกับ tooltip" is structural rather than a copy), each card naming its variant plus the item (*"คมอาคม ยันต์เพลิงกาลี"*), its ระดับ N → N+1 line and one line per stat the variant changes coloured from the Domain's own `UpgradeVocabulary.IsImprovement` (green when the change helps, red when it hurts), a hint line, a back button, and `1 2 3` / a card click / `Escape` to pick or close. `CampPresenter.ConfirmMergeChoice` closes the window **before** the command runs, so a double click cannot fold a pair twice (`MergeChoicesOpened` / `Confirmed` / `Cancelled`), the board is locked and hovering behind the modal rewrites nothing, and the floating tooltip is suppressed while it is up. The reachability change is Core's: `RunSession.PreviewDrop` now **accepts** the merge gesture (naming the piece under the pointer) instead of refusing `inventory.merge_needs_variant`, because the question the pair raises is answered by the window and not by the preview. The engine behind it — `OfferMerge` / `ConfirmMerge`, the pure-function draw, the per-step `VariantIds`, snapshot schema 2 — is [ADR 0021](../docs/adr/0021-merge-upgrade-choice.md); `data/` gained 20 localization rows (7 `camp.merge.*` labels, the 12 `upgrade.stat.*` names a card line reads and the unknown-stat fallback), 159 → **179**. Evidence: dotnet **466** passed, `tools/test.ps1 -Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3** in 127.3 s while the stage was built and 86.2 s on the closing run (`artifacts/stage09g/fast-suite.log`), Unity EditMode **94/94** (+15, `UiLayoutCheckTests` alone 12 → 19, including the rule that a modal may cover the panel it is asking about while still not excusing a real collision underneath), the new `CampMergeChooserPlayModeTests` **4/4** and the existing camp drag fixture **12/12** with real pointer events (drag onto the twin → window open, nothing changed → tap the second card → tier 2 **in the same cell** with that card's variant id; `Escape` and the back control → hash byte-identical), and the window captured in Play Mode at both sizes (`artifacts/ui/stage09g-chooser-{1920x1080,1280x720}.png` plus `-closed-*`, measured rather than eyeballed: the dim is the whole canvas, the window 1560×600 at (180,240) at 1920×1080 → 1040×400 at (120,160) at 1280×720, the cards 328×400 → 219×267 at x = 453/683/912, and every modal label ≥ 22 px at the canvas scaler's own 1.0). The browser suite is **not run** (no WebGL build this stage): `tests/web/camp.spec.ts` was re-pointed at the window's key path and is unverified | [`09g-report`](../docs/stages/09g-report.md) |

| 10 | 2026-10-16 | **เชื่อมวงจรเต็ม (vertical slice): Boot → Title → Camp → Battle floor 1 → Camp → … → floor 5 → Result, and back out to the title.** The stage is mostly about *where a run lives*: `RootLifetimeScope` is now the **App** scope (Boot calls `PersistAcrossScenes`, which moves it into `DontDestroyOnLoad` and — through VContainer's own `EnqueueParent` — makes it the parent of every scene scope that loads afterwards), `RunScopeOwner` holds the single **Run** scope (`RequireRun` builds the child scope on first use, `DisposeRun` is the only thing that ends it), and Camp/Battle/Result keep only their own **scene** scope; `RootLifetimeScope.Configure` returns immediately when it has a parent, so a scene scope inherits the shell instead of rebuilding a config, a clock and — the bug this stage exists to kill — a second run ("ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene"). `SceneLoader` is the only way a scene changes: one transition at a time behind a fade, four typed refusals (`transition.in_progress` / `scene_missing` / `load_failed` / `disposed`), the target checked against the build settings *before* anything is hidden, and **arriving at the title disposes the run scope** — which is what makes "Title ไม่มี Run scope เก่าค้าง" a one-line assertion instead of a per-screen habit. `BattleSceneDirector` plays the floor the run is already pointing at (`RunSession.PendingSetup`), settles it with `ApplyBattleResult` (HP, bia, consumables, kills, and the **15 % clear-heal that belongs to the run and is not applied to a loss**) and then leaves for `Result` when the run is over or back to `Camp` otherwise, retrying the ask every frame because a floor can end while a transition is still in flight. The summary screen (`Assets/Main/Scenes/Result.unity`, `Presentation/Result*` + `Game/ResultScene/`, 8 new `result.*` localization rows → **187**) shows `RunSession.Summary()` (headline, floor reached, ghosts, bia, HP) with **Restart** (a brand-new run: floor 1, starting purse, empty bag) and **กลับ Title**; the dev menu (**`DEV MENU (test only)`**, its own canvas at `sortingOrder` 200, authored **inactive**) speaks only `BattleCommand` and is gated twice — `DevCheats.Allowed` decided once at the root (`Debug.isDebugBuild`/`Application.isEditor`) and `BattleSetup.DevCheatsAllowed` copied into a floor when it departs, so a release player shows no menu and a floor that left with cheats off refuses every dev command with `RejectedNotAllowed`. **The loop found a real leak and it is fixed**: the fade overlay's fallback `EventSystem` was created on the `DontDestroyOnLoad` overlay and never taken down, so every scene after Boot carried two of them and Unity logged `There are 2 event systems in the scene` **every frame** — the 20-round-trip test measured a **linear 314 KB per iteration, 5.40 MiB over 18 trips** against its own 3 MiB budget, and after the fallback is destroyed in the transition's `finally` it measures **0.73 MiB** with the warning storm gone (1 930 entries → 5). Evidence: dotnet **466** passed, `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3** in 85.3 s then 76.0 s, Unity EditMode **115/115** (+21: `DevCheatTests` 8, `ResultScreenTests` 7, `RunScopeLifetimeTests` 3, `ResultScreenLayoutTests` 3), the four PlayMode classes green on their own after the repair (`--filter Camp` **25/25**, `SceneFlowPlayModeTests` **5/5** incl. the 20-loop), Result-screen shots from the real game (`result-victory.png`, `result-defeat.png`, `battle-devmenu.png`) and the summary's rectangles measured rather than eyeballed (`result-layout.md`) | [`10-report`](../docs/stages/10-report.md) |

| 10d | 2026-10-17 | **The title screen and its start control (the loop's first screen, which could not be left)**: `TitlePresenter.RequestStart` starts the run through the **same** `RunSession.StartRun` the camp calls and then records the intent — the run is asked for **lazily at the moment of the press** (`RunScopeOwner.RequireRun`), because this presenter is an **App-scope singleton** and arriving at the title is what disposes the run scope, so "กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่" is true by construction rather than by rebuilding something ([ADR 0022](../docs/adr/0022-the-title-starts-a-run.md)); `TitleSceneDirector` brings the screen up, publishes `__projectSurviveTitle` (scene marker, the two localized strings, the build line, canvas size, one named control with its centre in canvas pixels) and asks the `SceneLoader` for the camp, retrying a typed refusal every frame while leaving the screen as it was; `TitleView` mirrors four reactive strings and forwards one press, selects the control so `Enter` submits it (measured: the Input System version this project pins does not carry `Space` in the module's default submit bindings, so `Update` reads the key and forwards to the same `PressStart` — a second call is refused by the presenter); `Title.unity` is rebuilt from `ProjectSurvive/Stage 10d/7. Create Title scene (start control)` as three centre-anchored regions (1800×140 heading, 1800×60 build line, 400×76 control) on a new `title.start` row (**188** localization rows). **The browser gate found two real defects and both are fixed at the cause**: the report was written for the new canvas size with the **old** control position (`canvasWidth: 1280` with `buttons[0].x = 505.964`, 134 px from the 640 the centre had moved to) because `Screen` reports a size in the frame the element changed while the `CanvasScaler` lays the widgets out during that frame's render — the director now republishes while the point it published and the point the view reports disagree — and the control itself sat **26 px right of the centre** on the template's own 960×600 canvas, because it was laid out from the canvas' top-left corner with 1920-wide arithmetic while the scaler's virtual width follows the aspect (1821.6 units there); `ProjectSetup.CreateUiButton` now takes an optional `anchor` and the title passes the centre. Evidence: dotnet **468**, `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3** in 81.6 s with EditMode **130/130** (+9: `TitleScreenTests` 5, `TitleScreenLayoutTests` 4), PlayMode **45** with every class green on its own (the bulk run's 25 failures are the ambient-log flake, whose *cascade* in that run looked like title defects — `artifacts/stage10d/playmode-per-class.md`), the closed-Editor Full gate **PASS 11 \| FAIL 0 \| NOT RUN 2** in 268.4 s (WebGL **PASS** 70.2 s, Playwright **10 passed** 130.7 s incl. the new `title.spec.ts` clicking what the screen drew), and the Editor capture judging the same three rectangles at both GDD sizes (3 regions, 0 overlaps) with the player's own 1280×720 shot carrying ink in exactly those bands (5.24 % / 1.34 % / 1.54 % brighter than 0.55 luma, against 0 % in the Editor's shots, whose labels are empty until runtime) | [`10d-report`](../docs/stages/10d-report.md) |

## Notes carried forward

- **The bulk PlayMode suite fails on ambient Input System log-asserts, while every test passes
alone** (2026-10-15, Stage 09d; re-measured 2026-10-16, Stage 09e: **12 passed / 15 failed of 27**,
  the same eleven `IndexOutOfRangeException` + four `Cached unprocessed value` messages, and the same
  Camp fixture passing **11/11** on its own with different per-test durations; re-measured again
  2026-10-16, Stage 09f: **12 passed / 16 failed of 28** — twelve `IndexOutOfRange` + four
  `Cached unprocessed`, still no failed assertion of the project's own — and once more for Stage 10,
  **20 passed / 20 failed of 40**: the same twelve plus eight, with every Stage 10 test green inside
  that same payload and `--filter Camp` **25/25** on its own; the payload also drops four-of-five
  `ShellPlayModeTests` without reporting a failure, so a filtered run is the only way to see that class
  whole. The Camp fixture was
  **12/12** alone in 3.71 s; in Stage 10 it is green in the same way — **25/25** for the three camp
  classes plus the 20-loop when filtered, against 20/40 in the unfiltered run). In a freshly launched
  Editor, three whole-suite runs gave
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

- **Follow-up to Stage 10 (2026-10-16, not a Stage): the last leg of the loop is now covered twice.**
  `ProjectSurvive/Tools/1. Capture UI` renders the summary screen too (both GDD sizes, its panel, five
  lines and two buttons measured — `artifacts/ui/result-*.png`, `ui-report.md`), and the summary screen
  and the *dev menu* publish documents to the page (`__projectSurviveResult`, `__projectSurviveDevMenu`)
  so `tests/web/result.spec.ts` can play a standalone floor, press แพ้ทันที / lose now and assert the
  summary the player really reaches — the gap Stage 10's report recorded as NOT RUN in a build.
- The two new documents follow `CampReport`'s contract (globals the page reads; coordinates only, never
  a decision): `ResultView.TryGetButtonScreenPoint` + `DevMenuView.TryGetButtonScreenPoint` publish
  centres, and a release build writes no dev-menu document because its panel never shows. Fast has
  `ResultReportTests` (4) and `DevMenuReportTests` (2) pinning the content, and `ResultScenePlayModeTests`
  asserts both documents where the real scenes were reached.
- The Closed-Editor Full run did re-do the web gate, and its **first** attempt found a real rule bug:
  the `loseNow` dev command finished the floor `Lost` / `PlayerDied` with the player still at full HP, so
  the run kept 100 HP and the summary screen printed a defeat at full health. It is fixed in both
  sessions (`EcsBattleSimulation.KillPlayer` before `Finish`, `FakeBattleSession._hp = 0`) and pinned for
  both by `BattleSessionContractTests.Dev_lose_now_reports_the_death_it_stands_for` (dotnet 466 → 468);
  the failing run's own artifacts are kept in `artifacts/stage10/playwright-result-first-run/`. PlayMode
  is green per class (41 results, 0 failures — `artifacts/stage10/playmode-per-class-followup.md`), the
  20/40 bulk payload being the ambient Input-System/R3 log assert rather than an assertion of ours.
- The capture tool's Result shots and a play-mode capture of the same size were compared rather than
  assumed to agree: the white-text bands in `result-victory.png` fall inside the label rects the tool
  measures. What that comparison also showed is that `Summary` had **no background image** — every other
  named panel in `ProjectSetup` gets one — so the screen was white text on the clear colour plus its two
  buttons; asked for afterwards, the column now carries the camp `Board`'s own colour and alpha
  (`0.07/0.09/0.12` at **0.92**, `raycastTarget` off), the scene rebuilt from the builder
  (`ProjectSurvive/Stage 10/6. Create Result scene`) and the capture re-shot: `Summary (Surface, alpha
  0.92)`, **PASS**, 0 failures, 0 overlaps, and the panel's colour now **54.9 %** of the frame (55.0 % at
  1280×720) where those pixels used to be the scene's clear colour alone, the bare column's only other
  marks being the two buttons at 2.8 % (`artifacts/stage10/measure-result-panel.ps1`). The floor is enforced, not just printed — `UiLayoutCheck.OpaqueRegions` names `Summary`
  beside the camp's `Board`/`ChestPanel` — and two new EditMode tests hold it
  (`ResultScreenLayoutTests.The_summary_column_carries_the_camp_panels_own_background` reads the camp
  `Board`'s alpha out of `Camp.unity`; `UiLayoutCheckTests.The_summary_column_is_held_to_the_same_floor_
  as_the_camp_panels`), EditMode **119 → 121**, `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3**, 85.9 s. The
  WebGL gate agrees with the Editor: with the Editor closed, `web: WebGL build (Development)` **PASS**
  (20.7 s) and Playwright **9 passed in Chrome** (127.0 s, `result.spec.ts` 5.7 s incl. the `Restart`
  click) — `PASS 11 | FAIL 0 | NOT RUN 2`, 393.8 s (`artifacts/stage10/full-panel-web.log`), the two
  NOT RUNs being EditMode and PlayMode, which the same day's Editor-open runs cover (EditMode 121/121,
  `ResultScenePlayModeTests` 3/3). The panel itself is in the player's own screenshot:
  `tests/web/test-results/web-result-summary.png` is 33.8 % the panel colour `22,22,28` — the value the
  Editor capture measures inside the column — against 26.2 % of the scene's clear colour, and its
  middle scan line is white → clear → panel (x≈340..940) → clear → white, the column's rectangle.
- Next: anything that still needs a real player — a Release-build dev-menu check, IL2CPP stripping of
  the Result scene's DI beyond what the Development build exercises — stays named in
  `docs/stages/10-report.md` rather than assumed; and the summary screen's look is now a decision with
  evidence behind it rather than a guess.

### Stage 10d (2026-10-17) — the title screen, and what the browser gate caught

- **Did:** put a เริ่มเล่น control on the loop's first screen, started the run from the title through the
  same Application command the camp uses (`RunScopeOwner.RequireRun` at the moment of the press, never a
  held `RunSession`), recorded the press as an intent the scene's own director commits through the
  `SceneLoader`, published `__projectSurviveTitle` for the browser suite, rebuilt `Title.unity` as three
  centre-anchored regions, and wrote [ADR 0022](../docs/adr/0022-the-title-starts-a-run.md).
- **Result:** the browser check paid for itself twice — a report that named the new canvas size while
  aiming at the old control position (134 px out; the frame boundary between `Screen` and the
  `CanvasScaler`), and a control 26 px right of the centre at the template's own 960×600, because a
  1920-wide corner layout is not centred once the scaler's virtual width follows the aspect. Both fixed at
  the cause and re-measured in the player (480 of 960, 640 of 1280). Green: dotnet **468**, EditMode
  **130/130**, PlayMode **45** per class, Fast **PASS 10 \| FAIL 0 \| NOT RUN 3** (81.6 s), closed-Editor
  Full **PASS 11 \| FAIL 0 \| NOT RUN 2** (268.4 s, WebGL + **10 passed** in Chrome).
- **Next:** Stage 11's Thai ghost AI and balance; and the two things this stage named rather than fixed —
  a Release-build check of the title/Result screens, and the bulk PlayMode run, which is still a FAIL as a
  suite (25 of 45) and still nobody's failed assertion.
