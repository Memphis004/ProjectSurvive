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

| 08b | 2026-10-17 | **The frame carries each loadout entry's cooldown (contract + Simulation only, no UI)**, landed after 10d as an amendment to 08 because the next HUD needs it: `IBattleFrame.Cooldowns` is a `ReadOnlySpan<LoadoutCooldown>` in loadout order — `{ InstanceId, RemainingTicks, PeriodTicks, Flags }`, `ManaShort` the only flag — published by the same `BattleFrameBuffer` (a bounded list of `BattleConstants.MaxLoadoutEntries = 64`, written into the unpublished slot and swapped by `Publish`, so a reader never sees half a list and there is still exactly one `IBattleFrame` implementation); `WeaponLoadout`/`SummonLoadout` gained the **board instance id** they came from (`LoadoutResolver` sets it from `ItemInstance.InstanceId`, `0` = no piece), which is the key a HUD binds a cooldown to the piece the player placed; `FramePublishSystem` reports the casters it already had (`Caster.CooldownTicks`/`PeriodTicks`, plus the one reading a View cannot make, "ready but the pool is short" against `HudState.Mp`) with **no new rule and no change to combat** — `Caster.InstanceId` is deliberately not hashed; the fake manufactures the same shape from a predictable cycle gated by MP, so a HUD can be built against it; [ADR 0023](../docs/adr/0023-cooldown-state-on-the-battle-frame.md). Evidence: dotnet **481** (a shared suite of four contract cases run against **both** factories — entry count + instance-id mapping, one tick per tick with a reset only to the full period, the MP flag in both directions, and zero allocation with a caster loadout — plus the list's lifetime and bound at the buffer, the board id at its only producer, and one state-hash pin per factory recorded on the pre-change build in a clean `git worktree` of `HEAD`: `3218847520` Ecs / `2633275827` fake, so "the change moved no fight" is a measurement rather than a claim), `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3**, EditMode **130/130** | [`08b-report`](../docs/stages/08b-report.md) |

| 08c | 2026-10-17 | **The battle HUD rearranged around a mini ย่าม (Presentation only — 08b's first consumer)**: the Battle screen is six anchored panels instead of one column of labels — the **mini ย่าม** top left (read-only, the **bounding box of the cells the bag covers** and never the 9 × 7 board, ≤ 25 % of the canvas width: 432 × 546 px at 1920 × 1080, 288 × 364 at 1280 × 720; bag as background, pieces as category-coloured boxes with their tier number), the **floor** name and number top centre, the run's numbers (time / bia / kills / wave / speed / boss band) top right, the **status panel** bottom left (figure, red HP bar, blue MP bar, current/max — the bars moved from the old top-left panel), the **timeline** along the bottom middle and the **dev menu as one column** bottom right on its own canvas; the equipped-spell list is gone (`ย่ามจิ๋วแทนแล้ว`) and synergy link lines are deliberately not drawn yet. Each caster wears a plate from `IBattleFrame.Cooldowns` — dark for `RemainingTicks / PeriodTicks`, the faint blue tone for `ReadyButManaShort`, **nothing** when ready — read from the snapshot the runner copied in the **same frame**, never from a clock of the view's own; cells, boxes, plates and labels are built once at floor start and only changed values are written, with the view counting its own builds (`CreatedAfterBuild`) and writes (`PlateUpdates`) so a test can tell "the floor moved the plate" from "the view redrew everything". `UiLayoutCheck` gained the battle HUD's six surfaces and the controls inside them, so the capture command judges this screen with the rule it already applied to the จัดย่าม panel. **The stage's own new PlayMode assertions found a real defect**: `BuildBattleHud` created the `BattleMiniBagView` but nothing assigned it to `BattleViewSettings.MiniBag`, so the real scene had a null reference and drew an empty panel — invisible to the compiler, to EditMode and to the capture (which builds the bag itself); the fix is one `out` parameter plus one `SetObjectReference`, and `Battle.unity` was regenerated through `ProjectSurvive/Stage 08/3` (the scene now carries `MiniBag: {fileID: 86127954}`). Evidence: the capture **PASS, 0 failures**, 16 images, **36 battle regions per state per size with 0 overlaps** at both sizes and the mid-fight shot a real **tick 180 with 2 casters at 2 distinct cooldown levels** ([`artifacts/ui/ui-report.md`](../artifacts/ui/ui-report.md)); `BattlePlayModeTests` **5/5** (`artifacts/stage08c/playmode-battle-class.json`); EditMode **144/144** (`BattleMiniBagTests` 14); dotnet **481** with the load test's pinned localization count moved **188 → 192** for the four new `hud.*` rows; `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0** 88.1 s. Not tested, and said so: no WebGL build or browser run (the brief forbids a Web build while working), no frame-cost measurement, and `ManaShort` unreachable on floor 1 so its shot is the one authored state | [`08c-report`](../docs/stages/08c-report.md) |

| 09 | 2026-10-15 | **ศาลาพัก (camp) + จัดย่าม screen, made usable (09b) and walkable (09c)**: `Assets/Main/Scenes/Camp.unity` (5th scene, `?scene=Camp`) + `Camp/**` + `Presentation/Camp/**` (13 files, `CampBagScreenView` 1415 lines, `CampReport` 713) — a fullscreen translucent overlay over the sala (board / chest `x/10` / incense pot / 4-step instruction scroll / restore+lock-on-restore checkbox / navigation+stats) with drag feedback, synergy lines, click-then-click fallback and Escape-cancel; since 09c the sala is walked with WASD under a follow camera, the shop is **four shelves in the world** (one affordance per slot, `E` or a click, the same `RunSession` command) with the เซียมซี drum and per-shelf lock pins, and the camp starts with the panel closed; `RunSession.PreviewDrop` is a **dry run of the same `BackpackEditor` predicates** the accepted command asks, `SetBagLocked` + the camp read model (`EvaluateLayout`/`BagWeight`/`BagPenalty`/…) added, `Domain/Run/BagDrop` extracted (+81 localization rows, 145 total); two documents published for the browser (`__projectSurviveCamp`, `__projectSurviveCampGeometry`) with every cell/slot/station as a **centre** (a published corner is outside its own rect); [ADR 0019](../docs/adr/0019-camp-screen-read-model-and-gesture-preview.md) | [`09-report`](../docs/stages/09-report.md) + [`09b`](../docs/stages/09b-report.md) + [`09c`](../docs/stages/09c-report.md) |

| 09d | 2026-10-15 | **Thai text sharpness, measured rather than eyeballed**: the font every canvas actually uses is reported from the loaded assets (`Sarabun-Regular SDF` — 90 pt sampling, 9 px padding, **SDFAA**, Static, 1024², 182 chars) and `THSarabunPSK SDF` is shown to be **orphaned** (no scene references it, 0 characters baked) while being the **largest asset in the Web build** (4,220,916 B packed); all four canvases confirmed `ScaleWithScreenSize` 1920×1080 match 0.5, scene labels ≥ 22 px (Title 28, Battle 22, Camp 22); the จัดย่าม label measured **21 px of ink at 1920×1080** in the real player and 14 px at 1280×720 (the scaler's 0.667), 27 px at deviceScaleFactor 2; THSarabunPSK measured **31 % shorter** than Sarabun at the same size (31 px needed for parity) with three comparison images; 13/13 Playwright screenshots (17 PNGs) at 1920×1080/1280×720 × deviceScaleFactor 1/2; two new tools (`tools/font-metrics.py`, `tools/screen-text-metrics.py`) and `tests/web/scripts/collect-geometry.mjs` | [`09d-report`](../docs/stages/09d-report.md) |

| 09e | 2026-10-16 | **SceneCamp UI, three layout/compositing fixes (no Core, no `data/`, no WebGL build)**: the จัดย่าม overlay is rebuilt in `ProjectSetup` from canvas-edge anchors — the panel's foot is a `Stats` strip that carries only the controls the status bar does not (`กลับ` / `ไปชั้นถัดไป` no longer sit on เบี้ย · น้ำหนัก · สมาธิ · HP/MP, which are drawn **once**, on the bar), the incense pot moved into the column between the panel and the bar's depart button, and Board/ChestPanel render at **0.92** alpha with a tested floor (`UiLayoutCheck.FindOpacityViolations`); a cell's item name is primary and a bag's name is secondary and **hidden** when an item's rectangle reaches it (`LayoutPieceLabels`), measured in PlayMode over real pieces. The tooltip is a **second root canvas at sorting order 100** (documented as *"Canvas' order within a sorting layer"* in the installed engine), no `GraphicRaycaster`, `raycastTarget` off, 0.95 background, placed by the pure `CampTooltipPlacement.Place` (flips at the edges, pinned when larger than the canvas, never under the pointer) and split into the five lines the stage asks for, each hidden when empty. Synergy marks are chosen from the rule's own `synergy.kind`: **a line only for the tome→weapon same-line rule**, a corner **badge** for the bag-area bonus (never a line from a bag), and nothing permanent for the three adjacency rules (hover/drag highlight only), from Core's unchanged `ActiveSynergy`. Evidence: `artifacts/ui/` grew to **8 images** including a third `camp-tooltip-*` state whose draw order is proved **in pixels** (the overlay off/on comparison), the capture report now prints every region's **alpha**, 75/75 EditMode (+10) and 11/11 `CampBagDragPlayModeTests` (+3) green | [`09e-report`](../docs/stages/09e-report.md) |

| 09f | 2026-10-16 | **A bag move or turn carries the items standing on it (no `data/` content, no new system)**: the Stage 05 rule (a bag may not move while an item stands on it, refused whole) is replaced for the two commands that stay on the board by a **rigid assembly** — `FootprintRotation.Rotate(shape, rotation, out CellOffset shift)` now publishes the translation its re-normalisation removed, and `Backpack.CollectRiders` maps every rider cell through that same map, so a pure move is the zero-turn case of one function and the chiral `bag.wicker_basket_l` is the case that fails the naive "translate the item by the bag's delta" version ([ADR 0020](../docs/adr/0020-bag-moves-carry-the-items-on-them.md) supersedes ADR 0014 §5); a rider is an item whose **every** cell lies on that bag, an item across two bags does not ride and a move that would leave it uncovered is refused whole with the new, separately localized `inventory.item_spans_bags` (146 localization rows), while `inventory.item_stranded` narrows to *taking a bag off the board with items on it* (an item can never enter the chest). `BackpackEditor.CanPlace` fills the carry plan as it answers and the accepted command applies exactly that plan (one predicate, so preview and command cannot land an item in two places), `PlaceAssembly` lifts the bag and all riders before putting any down and bumps `Revision` **once** (one gesture = one undo step = one combo recompute). The screen draws what the stage asked for (*"แสดงเงาของไอเทมที่ติดไปที่ตำแหน่งใหม่"*) from the run's plan: `CampCarryGhost` + a pooled ghost box per rider, and a 2 000-move same-cell drag allocates **0 bytes**. Evidence: 16 new tests (dotnet **433**, EditMode **79/79**, the Camp PlayMode fixture **12/12** alone incl. a real pointer drag), the bulk PlayMode suite still 12/28 with **no failed assertion of ours** (12 `IndexOutOfRangeException` + 4 `Cached unprocessed` from the Input System's own ambient log asserts), a live probe (`BAG Bag#1 … at 5,4 \| ITEM Item#2 … at 5,4`), and an A/B capture whose item blob `#bba386` is **one 70×74 px cell before and after**, moved by exactly the 5,4 cell delta, with every differing pixel inside the board rect | [`09f-report`](../docs/stages/09f-report.md) |

| 09g | 2026-10-16 | **The upgrade-choice window on SceneCamp, and with it the second half of 09f (the engine)**: a drop of one piece onto another with the same id and tier asks `RunSession.OfferMerge`, and when the offer has choices the screen opens a **modal** built at runtime by `CampBagScreenView` from one published model (`CampUiState.MergeChooser` → `CampMergeChooserModel` / `CampMergeChoiceModel`) — a full-screen dim layer at 0.72 that swallows pointer events (the physical drag lock), the window 1560×600 centred on the canvas, three cards 328×400 laid out horizontally, a left panel carrying the **same five lines the tooltip publishes** (one producer, `CollectSourceLines`, so "เนื้อหาเดียวกับ tooltip" is structural rather than a copy), each card naming its variant plus the item (*"คมอาคม ยันต์เพลิงกาลี"*), its ระดับ N → N+1 line and one line per stat the variant changes coloured from the Domain's own `UpgradeVocabulary.IsImprovement` (green when the change helps, red when it hurts), a hint line, a back button, and `1 2 3` / a card click / `Escape` to pick or close. `CampPresenter.ConfirmMergeChoice` closes the window **before** the command runs, so a double click cannot fold a pair twice (`MergeChoicesOpened` / `Confirmed` / `Cancelled`), the board is locked and hovering behind the modal rewrites nothing, and the floating tooltip is suppressed while it is up. The reachability change is Core's: `RunSession.PreviewDrop` now **accepts** the merge gesture (naming the piece under the pointer) instead of refusing `inventory.merge_needs_variant`, because the question the pair raises is answered by the window and not by the preview. The engine behind it — `OfferMerge` / `ConfirmMerge`, the pure-function draw, the per-step `VariantIds`, snapshot schema 2 — is [ADR 0021](../docs/adr/0021-merge-upgrade-choice.md); `data/` gained 20 localization rows (7 `camp.merge.*` labels, the 12 `upgrade.stat.*` names a card line reads and the unknown-stat fallback), 159 → **179**. Evidence: dotnet **466** passed, `tools/test.ps1 -Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3** in 127.3 s while the stage was built and 86.2 s on the closing run (`artifacts/stage09g/fast-suite.log`), Unity EditMode **94/94** (+15, `UiLayoutCheckTests` alone 12 → 19, including the rule that a modal may cover the panel it is asking about while still not excusing a real collision underneath), the new `CampMergeChooserPlayModeTests` **4/4** and the existing camp drag fixture **12/12** with real pointer events (drag onto the twin → window open, nothing changed → tap the second card → tier 2 **in the same cell** with that card's variant id; `Escape` and the back control → hash byte-identical), and the window captured in Play Mode at both sizes (`artifacts/ui/stage09g-chooser-{1920x1080,1280x720}.png` plus `-closed-*`, measured rather than eyeballed: the dim is the whole canvas, the window 1560×600 at (180,240) at 1920×1080 → 1040×400 at (120,160) at 1280×720, the cards 328×400 → 219×267 at x = 453/683/912, and every modal label ≥ 22 px at the canvas scaler's own 1.0). The browser suite is **not run** (no WebGL build this stage): `tests/web/camp.spec.ts` was re-pointed at the window's key path and is unverified | [`09g-report`](../docs/stages/09g-report.md) |

| 10 | 2026-10-16 | **เชื่อมวงจรเต็ม (vertical slice): Boot → Title → Camp → Battle floor 1 → Camp → … → floor 5 → Result, and back out to the title.** The stage is mostly about *where a run lives*: `RootLifetimeScope` is now the **App** scope (Boot calls `PersistAcrossScenes`, which moves it into `DontDestroyOnLoad` and — through VContainer's own `EnqueueParent` — makes it the parent of every scene scope that loads afterwards), `RunScopeOwner` holds the single **Run** scope (`RequireRun` builds the child scope on first use, `DisposeRun` is the only thing that ends it), and Camp/Battle/Result keep only their own **scene** scope; `RootLifetimeScope.Configure` returns immediately when it has a parent, so a scene scope inherits the shell instead of rebuilding a config, a clock and — the bug this stage exists to kill — a second run ("ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene"). `SceneLoader` is the only way a scene changes: one transition at a time behind a fade, four typed refusals (`transition.in_progress` / `scene_missing` / `load_failed` / `disposed`), the target checked against the build settings *before* anything is hidden, and **arriving at the title disposes the run scope** — which is what makes "Title ไม่มี Run scope เก่าค้าง" a one-line assertion instead of a per-screen habit. `BattleSceneDirector` plays the floor the run is already pointing at (`RunSession.PendingSetup`), settles it with `ApplyBattleResult` (HP, bia, consumables, kills, and the **15 % clear-heal that belongs to the run and is not applied to a loss**) and then leaves for `Result` when the run is over or back to `Camp` otherwise, retrying the ask every frame because a floor can end while a transition is still in flight. The summary screen (`Assets/Main/Scenes/Result.unity`, `Presentation/Result*` + `Game/ResultScene/`, 8 new `result.*` localization rows → **187**) shows `RunSession.Summary()` (headline, floor reached, ghosts, bia, HP) with **Restart** (a brand-new run: floor 1, starting purse, empty bag) and **กลับ Title**; the dev menu (**`DEV MENU (test only)`**, its own canvas at `sortingOrder` 200, authored **inactive**) speaks only `BattleCommand` and is gated twice — `DevCheats.Allowed` decided once at the root (`Debug.isDebugBuild`/`Application.isEditor`) and `BattleSetup.DevCheatsAllowed` copied into a floor when it departs, so a release player shows no menu and a floor that left with cheats off refuses every dev command with `RejectedNotAllowed`. **The loop found a real leak and it is fixed**: the fade overlay's fallback `EventSystem` was created on the `DontDestroyOnLoad` overlay and never taken down, so every scene after Boot carried two of them and Unity logged `There are 2 event systems in the scene` **every frame** — the 20-round-trip test measured a **linear 314 KB per iteration, 5.40 MiB over 18 trips** against its own 3 MiB budget, and after the fallback is destroyed in the transition's `finally` it measures **0.73 MiB** with the warning storm gone (1 930 entries → 5). Evidence: dotnet **466** passed, `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3** in 85.3 s then 76.0 s, Unity EditMode **115/115** (+21: `DevCheatTests` 8, `ResultScreenTests` 7, `RunScopeLifetimeTests` 3, `ResultScreenLayoutTests` 3), the four PlayMode classes green on their own after the repair (`--filter Camp` **25/25**, `SceneFlowPlayModeTests` **5/5** incl. the 20-loop), Result-screen shots from the real game (`result-victory.png`, `result-defeat.png`, `battle-devmenu.png`) and the summary's rectangles measured rather than eyeballed (`result-layout.md`) | [`10-report`](../docs/stages/10-report.md) |

| 10d | 2026-10-17 | **The title screen and its start control (the loop's first screen, which could not be left)**: `TitlePresenter.RequestStart` starts the run through the **same** `RunSession.StartRun` the camp calls and then records the intent — the run is asked for **lazily at the moment of the press** (`RunScopeOwner.RequireRun`), because this presenter is an **App-scope singleton** and arriving at the title is what disposes the run scope, so "กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่" is true by construction rather than by rebuilding something ([ADR 0022](../docs/adr/0022-the-title-starts-a-run.md)); `TitleSceneDirector` brings the screen up, publishes `__projectSurviveTitle` (scene marker, the two localized strings, the build line, canvas size, one named control with its centre in canvas pixels) and asks the `SceneLoader` for the camp, retrying a typed refusal every frame while leaving the screen as it was; `TitleView` mirrors four reactive strings and forwards one press, selects the control so `Enter` submits it (measured: the Input System version this project pins does not carry `Space` in the module's default submit bindings, so `Update` reads the key and forwards to the same `PressStart` — a second call is refused by the presenter); `Title.unity` is rebuilt from `ProjectSurvive/Stage 10d/7. Create Title scene (start control)` as three centre-anchored regions (1800×140 heading, 1800×60 build line, 400×76 control) on a new `title.start` row (**188** localization rows). **The browser gate found two real defects and both are fixed at the cause**: the report was written for the new canvas size with the **old** control position (`canvasWidth: 1280` with `buttons[0].x = 505.964`, 134 px from the 640 the centre had moved to) because `Screen` reports a size in the frame the element changed while the `CanvasScaler` lays the widgets out during that frame's render — the director now republishes while the point it published and the point the view reports disagree — and the control itself sat **26 px right of the centre** on the template's own 960×600 canvas, because it was laid out from the canvas' top-left corner with 1920-wide arithmetic while the scaler's virtual width follows the aspect (1821.6 units there); `ProjectSetup.CreateUiButton` now takes an optional `anchor` and the title passes the centre. Evidence: dotnet **468**, `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3** in 81.6 s with EditMode **130/130** (+9: `TitleScreenTests` 5, `TitleScreenLayoutTests` 4), PlayMode **45** with every class green on its own (the bulk run's 25 failures are the ambient-log flake, whose *cascade* in that run looked like title defects — `artifacts/stage10d/playmode-per-class.md`), the closed-Editor Full gate **PASS 11 \| FAIL 0 \| NOT RUN 2** in 268.4 s (WebGL **PASS** 70.2 s, Playwright **10 passed** 130.7 s incl. the new `title.spec.ts` clicking what the screen drew), and the Editor capture judging the same three rectangles at both GDD sizes (3 regions, 0 overlaps) with the player's own 1280×720 shot carrying ink in exactly those bands (5.24 % / 1.34 % / 1.54 % brighter than 0.55 luma, against 0 % in the Editor's shots, whose labels are empty until runtime) | [`10d-report`](../docs/stages/10d-report.md) |

| 06b | 2026-10-19 | **แก้บั๊ก: stock ของศาลาไม่ตาม run ไปศาลาถัดไป.** `ShopStock.BeginCamp()` เรียก `ShopSlot.Clear()` เพื่อหมายถึง "ศาลาใหม่" แต่ `Clear()` ตั้งใจไม่แตะ `IsPurchased` (ซึ่ง `MarkPurchased()` ต้องใช้) ช่องที่เช่าแล้วจึงติดธงเช่าค้างทั้ง run และ `Roll` ข้ามมันผ่าน `CanBeRerolled` ตลอด — คนละเจตนา จึงแยกเมธอด: เพิ่ม `ShopSlot.ResetForCamp()` (ล้าง offer + `IsPurchased` + `IsLocked`) ให้ `BeginCamp()` เรียกแทน โดย `Clear()` ยังเป็นของ re-roll/การเช่า. | เทสต์ A (dotnet, ข้อมูลจริง) ล้มก่อนแก้ **6 เคส** (`Expected: 4 / Actual: 3`, `2`, `0`) และผ่านหลังแก้ **21/21**; dotnet ทั้งชุด **504**; `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0** 153.1 s, EditMode **155/155**; ลูป PlayMode `ResultScenePlayModeTests` ได้เงื่อนไข "ศาลาที่ 2 เต็ม 4 ช่อง ไม่ติดเช่า/ล็อก และ `RefreshCount` 0" → **3/3**, พร้อม red-check ที่ย้อน fix แล้วเทสต์ล้มจริง. ไม่มี Presentation bug (presenter/view อ่าน stock สดใหม่ทุกครั้ง). | Stage ถัดไป: กลับเข้าแถว 11 (Thai ghost AI, Pob boss, balance); WebGL build/Playwright ไม่ได้รันในสเตจนี้ตามกติกา — [report](../docs/stages/06b-report.md) |
| 09h | 2026-10-19 | **The tooltip's numbers follow the upgrade**: the camp tooltip used to read the `skill` row, so a 3/3 piece printed its 1/1 damage and cooldown and a picked variant changed nothing on screen. It now quotes one Application read model — `RunSession.ExplainPiece` → `LoadoutResolver.TryExplain` → `PieceReadout` (base row, resolved row, one entry per confirmed variant with the variant's own modifier rows, the combos acting on the piece, and `CooldownAtMinimum` decided from the value **before** the clamp) — whose resolved numbers are copied out of the very `WeaponLoadout`/`SummonLoadout` the next floor receives, so the screen cannot disagree with the battle; a board piece is quoted from the resolver's own snapshot, a chest piece is resolved with the same accumulator and no combos. The panel grew its sixth line (`CampUiState.TooltipUpgrades` → `CampTooltipView._upgradesLabel`, 460×500 in `CampSceneBuilder`) and the upgrade window's left column reads the same `CollectSourceLines`, so the two cannot drift. The diagnosis is on the record: 3/3 resolved **15 / 0.765 / 4** against a base **10 / 0.9 / 4** with `constant.min_cooldown_seconds` **0.2** — the floor was never what held 0.9, and the *first* read-model build still printed 0.8 because the localization row formatted `{1:0.#}`; it is now `{1:0.##}` and prints 0.77 (display only, no balance number moved). The capture command also had to be repaired: `ScreenCapture.CaptureScreenshotAsTexture` returns an **all-black** frame from an unfocused Editor, which is why both shots were 1920×1080 and one flat colour and the "drawn there" check failed — it now renders through the scene camera into a 1920×1080 target like the rest of the tool, and the report is **PASS**, 0 failures, 5/5 sample pixels. Evidence: `dotnet test` **510 → 519** (`PieceReadoutTests` 8), EditMode **155 → 156**, `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0** 107.9 s, the two camp PlayMode fixtures green alone (**12/12** and **4/4**), and before/after shots at 1920×1080. | [`09h-report`](../docs/stages/09h-report.md) |

| 11a | 2026-10-19 | **Item-local effects and the `status` table (data, Domain, Simulation; no ghost AI)**: the three charms now carry a `charm_stat`/`charm_method`/`charm_magnitude` row each, applied by `LoadoutResolver` to the player stats `CombatLoadoutSnapshot` already carries (order-independent accumulation, no new mechanic); the two offerings carry `offering_effect`/`amount`/`uses`/`auto_hp_percent`, the battle spends one use per threshold crossing and reports **remaining uses** in `BattleResult.RemainingConsumables`, and the settlement rounds the difference up to whole pieces; the new `status` table (`damage_per_tick`, interval, duration, `max_stacks`) replaces ADR 0017 §4 and its def id is the status row's own. Tooltip: `PieceReadout` carries the charm/offering rows and the camp status line prints them from the same read model. [ADR 0028](../docs/adr/0028-item-effects-and-status-table.md); `SchemaVersion` 5 → 6. | dotnet **541** passed (18 new/changed: per-charm snapshot effects, per-offering effect fields, auto-use/uses/settlement, status values and stacking, zero allocation); `gen.ps1` / `verify-generated.ps1` OK; `check-fixtures` **27/27** with two new fixtures; wiki regenerated/verified. | **Next:** Stage 11's Thai ghost AI, Pob boss and balance; the charm magnitudes and status numbers are starting points for that pass. |

| 11b | 2026-10-20 | **Krasue and Nang Tani, with telegraphs**: one small FSM (`EnemyAttackSystem`) runs both ghosts — `Chase → Telegraph → Strike → FollowUpTelegraph → FollowUp → Retreat → Recover`, skipping the stages a row leaves at zero — so Krasue warns a line, dashes it, warns an aura, drains and retreats while Nang Tani stands still and alternates a ring around herself with a vine lash along a line; `EnemySteerSystem` moves a ghost only while its brain is `Chase`, so a warning is never dragged along at walking speed, and `EnemyBrain` (state, timers, locked aim) is hashed. Every strong hit is preceded by `TelegraphStarted` carrying the footprint the damage uses — `Value` = `TelegraphShape`, `Radius`/`Length`/`Angle` are three **additive** `BattleEvent` fields, `DefId` is now the **enemy** — and `TelegraphEnded` closes it in the tick the stage ends; `TelegraphPool` draws that payload as a pooled floor decal (64 slots, no instantiation, a 6 s safety expiry). Fifteen new `enemy` columns (`attack_pattern` `None\|DashDrain\|RingVines`, range, telegraph/ strike times, damage, radius, length, the `follow_up_*` set, `retreat_*`) are the whole attack, validated (no warning time, a ring with a line length, a drain with no cadence, a retreat with no speed, a contact-only ghost carrying attack numbers are all findings); shape is decided by `length` (0 = circle). [ADR 0029](../docs/adr/0029-enemy-telegraphed-attacks.md). | dotnet **555** (8 behaviour tests incl. stage order and warning-before-hit ticks, 5 validator rules, one shared contract case for both factories, zero allocation in a telegraphing tick), `-Suite Fast` **PASS 11 \| FAIL 0 \| NOT RUN 3 \| WARN 0** 101.3 s, Unity EditMode **159/159**, and an Editor capture of the real floor-3 warning at **1920×1080** (`artifacts/ui/stage11b-telegraph-1920x1080.png`, tick 272). Also repaired the inherited red 11a tooltip test: `CampPresenter.Hover` published the tooltip before rebuilding the board cache. | **Next:** Pob's phases and corpse eating, then the balance pass over the new attack numbers (dash 9 / ring 12 / vines 8 are untuned starting points). |
| 11c | 2026-10-21 | **Pob the boss**: `BossSystem` runs the FSM — phase 2 begins the tick HP reaches `boss_phase2_hp_percent` of max (resolved to absolute HP once at spawn by `BossRules`, announced once as `BossPhaseChanged`, never re-opened by a heal), and from then on the boss warns a `TelegraphStarted` circle of `boss_possess_range` (same payload and `Boss` flag as a ghost's attack), possesses a player still inside it for `boss_possess_seconds` — `Possessed.RemainingTicks` on the player, negated by `PlayerMoveSystem`, counted down before the movement step — then waits `boss_possess_interval_seconds`. `DeathSystem` feeds a ghost that dies inside `boss_corpse_range` to the boss for `boss_corpse_heal` HP, capped at max, and publishes only the actual regain as `CorpseConsumed`; a dying boss ends the possession it held and pays `drop_value` on the ordinary drop queue, so Stage 07c's sweep collects it. The HUD's one new contract field is `HudState.ControlFlipTicks`, published from the player's component and drawn as `hud.possession` — no event pair, because a view-owned clock drifts from the tick [ADR 0030](../docs/adr/0030-boss-possession-hud-channel.md). Six new `enemy` columns, seven validator codes, and `wave.forest_5.spirits` (26 ghosts to eat on the boss floor). | dotnet **563** (5 boss behaviour tests: phase threshold, warn→flip→release timing, heal cap, drop-and-release, replay hash), `-Suite Fast` **PASS 11 \| FAIL 0 \| NOT RUN 3 \| WARN 0** 155.2 s, EditMode **159/159**, `-Suite Full` closed-Editor **PASS 12 \| FAIL 0 \| NOT RUN 2** (WebGL 276.6 s, Playwright 10/10), and an Editor capture of floor 5 mid-possession at **1920×1080** (`artifacts/ui/stage11c-boss-possession-flipped-1920x1080.png`, tick 594). PlayMode in the open Editor: `BattlePlayModeTests` 6/6 and 25 ambient Input System log failures elsewhere (the documented flake). | **Next:** Stage 11d's balance pass — 50 % phase, 3 s flip on a 14 s rhythm and 12 HP a corpse are untuned, and the boss floor has never been played to a win or a loss. |

| 11d | 2026-10-21 | **The balance pass, data only**: `tools/sim.ps1` and the harness grew `--build` — `default` (the Stage 07 greedy shopper, unchanged), `ranged` (fire bolt + piercing thread), `melee` (mor knife + takrut aura, and the bot *walks in* instead of kiting) and `familiar` (kuman thong) — so a 200-seed run can say which loadout a floor accepts; the CSV gained a `build` column. Eighteen recorded rounds of table edits: skills (fire 10→7, thread 9→7, knife 14→17 @1.7, takrut 7→9), krasue (speed 3.2→2.9, touch 7→4, cycle 1.2→2.2 s, dash 9→6), nang tani (ring 12→9 @4.5→3.4), Pob (320→250 HP, touch 12→22, corpse heal 12→16, possession every 14→10 s at range 9), the floor-3 krasue wave (16 @1.5 s → 12 @2.0 s) and the floor-1 wave (30 @2.0 s → **20 @3.0 s**, because the thinner wave is what keeps a stationary player alive on floor 1 — the browser gate caught that fire 10→7 had broken it). Boss win rate: ranged **58.0 %**, default **45.0 %**, melee **37.2 %**, familiar **31.0 %** (all inside 30–60 %) with PlayerDied up from 2–6 % to 12–26 %; floors 1–4 clear ~100 %. Income is 9–23 bia a floor (≈1.5 pieces at 5–14 bia prices), per ADR 0025. Two tests that had pinned skill damage now read the row instead, and the replay fingerprint was re-recorded once because the data a fight reads changed. | dotnet **563**, `-Suite Fast` **PASS 11 \| FAIL 0 \| NOT RUN 3 \| WARN 0** (99.3 s), EditMode **159/159**, wiki regenerated and verified; WebGL build `configHash D5B4813E…` and Playwright **10/10** in Chrome (run outside the harness, open Editor holds the project lock). PlayMode in the open Editor: 22/48 — the documented ambient Input System flake, Battle 6/6. 200-seed evidence in `artifacts/stage11d/{baseline-default,r18-*}.csv` with every intermediate round kept as `r0…r17`. | **Next:** floors 1–4 are now formalities and boss floors still end in a Timeout 30–52 % of the time — Pob has no telegraphed attack yet, so giving it one is the next lever for lethality, and early-floor tension belongs to the content stage. |


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
  no `status` table, no `enemy.drop_value`, no `constant.magnet_speed` — and because a run earned no bia
  inside a battle, the 200-seed balance run (floor 1 200/200, floor 2 198/200, floor 3 91/198, floor 4
  71/91, floor 5 71/71) describes the tables *with no way to buy anything after the first camp*, which
  is the first thing Stage 11 has to fix ([ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md)).
  **Stage 07b closed the drop-value half**: `enemy.drop_value` and `economy.clear_reward_bia` now pay a
  run ([ADR 0025](../docs/adr/0025-bia-from-battle.md)).
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

## Stage 10t — generic buffers in Shared, one builder per scene, one name per global

- **Did:** moved the battle ring and the Prev/Cur double buffer's mechanics into
  `ProjectSurvive.Core.Shared` (`EventRing<T>`, `PrevCurBuffer<T>`, ADR 0024) and left the battle types as
  thin adapters; split `ProjectSetup`'s scene creation into six `*SceneBuilder` files with the same menu
  paths; gave the three page globals one home per side (`WebGlBridge` constants, `tests/web/globals.mjs`,
  `.jslib` takes the name as an argument) with an EditMode test that fails on drift; AGENTS rule 14 asks
  for a `generic:` commit prefix on work that names no game type.
- **Result:** dotnet **497** (16 new), EditMode **147/147** (3 new), PlayMode **46/46 per class** (bulk is
  the same 21/46 ambient-log FAIL before and after), Playwright **10/10** on the harness's five specs,
  WebGL build 244.6 s. All six scenes regenerate to the same scene object-for-object; the checked-in
  files are the builder's current output, which also repaired Camp's chooser colours and the Thai spike's
  input module.
- **Next:** Stage 11's Thai ghost AI and balance; the bulk PlayMode flake and the camp specs' 15 s polls
  are still the two things that make a Full run red for reasons nobody owns yet.

## Stage 06c — a paid re-roll refills a rented shelf

- **Did:** `ShopSlot.CanBeRerolled` is now `!IsLocked` (was `!IsLocked && !IsPurchased`) and
  `ShopRoller` calls a new `ShopSlot.BeginReroll()` — rent flag *and* offer go — on every slot it is
  about to fill, so a paid เสี่ยงเซียมซี refills the shelves that were rented in this sala and a shelf
  with no candidate is left empty **and unrented**; `RunSession.SetSlotLocked` now refuses an empty
  slot too (`shop.slot_empty`), and nothing else moved — same price ladder, same shop stream, same
  atomic "charge, then roll" ([ADR 0026](../docs/adr/0026-a-paid-reroll-refills-a-rented-slot.md)
  supersedes the roller-skip half of [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) §2).
  No `data/` change at all.
- **Result:** dotnet **511** (1 new case, the two old assertions rewritten into refill cases), EditMode
  **155/155** (the rented-slot case re-asserted as "a paid re-roll clears the rent"), `-Suite Fast`
  **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0** (82.4 s), PlayMode per class — CampWorld **9/9** (the new
  rent-then-click-the-drum case), CampBagDrag **12/12**, CampMergeChooser **4/4**, ResultScene **3/3**.
  Replay, seed-determinism and the "money short changes nothing" hash assertions all still hold.
- **Next:** Stage 11 (Thai ghost AI, Pob boss, balance). WebGL build and Playwright were **NOT RUN**
  (Stage 06c is not a browser gate and no browser spec casts the sticks after a rent); the new
  `shop.slot_empty` pin refusal has no directly reachable test with shipped data.

## Stage 08d — the mini ย่าม hangs from its caption

- **Did:** anchored `MiniBagContent` to the panel's top-left (same left inset as the caption, a named
  8 px gap under it) and made `MiniBagLayoutBuilder`'s origin that corner instead of the centre of a
  412 × 494 area; added `UiLayoutCheck.FindMiniBagStackViolations` (below the caption, same left edge,
  no overlap — a centred grid is disjoint, so the overlap rule could not see the defect); +4 EditMode
  mini-bag cases, +4 layout-check cases, +1 battle PlayMode case; regenerated `Battle.unity` through
  the Stage 08 menu.
- **Result:** the first drawn pixel moved **110 px left / 149 px up at 1080p** and **73 / 100 at 720p**,
  cell size unchanged at 64; `BattleMiniBagTests` 18/18, `UiLayoutCheckTests` 24/24, `BattlePlayModeTests`
  6/6, EditMode **155/155**, `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**. WebGL and
  Playwright NOT RUN by the brief.
- **Next:** Stage 11's Thai ghost AI and balance; the panel `Image`'s hand-disabled state is reported
  (not a code intent) — if "no backdrop" is wanted it has to be expressed in `BattleSceneBuilder`,
  because a hand edit does not survive regeneration.

## Stage 07b — bia from battle

- **Did:** added `enemy.drop_value` (filler/fast 1, elite 3, boss 10) and `economy.clear_reward_bia`
  (8); the value is copied onto the entity at spawn, carried by its pickup and credited to
  `CurrencyEarned` exactly once when collected, while the run's `ApplyBattleResult` credits the
  collected drops on any outcome plus the clear reward **on a win only** in the existing
  once-per-floor settlement ([ADR 0025](../docs/adr/0025-bia-from-battle.md) supersedes ADR 0017 §5).
  Schema 3 → 4, validator + `enemy-drop-value-negative` fixture, and the HUD assertion that its
  bia is `startingBia + collected`.
- **Result:** dotnet **510** passed (+6), `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**
  (EditMode **155/155**), 200-seed sim before/after: floor 1 200/200, floor 2 198→197/200, floor 3
  88→75/197, floor 4 69→53/75, floor 5 69→52/53; realized bia per floor 0 → about 4 collected + 8
  clear. The 200-seed win rates are **not tuned** (Stage 11 owns that).
- **Next:** Stage 11 (Thai ghost AI, Pob boss, balance); the remaining ADR 0017 gaps are the
  elemental-status `status` table and `constant.magnet_speed`, and `DevKillAll` still queues drops
  that the next `Step` clears (pre-existing, left untouched).

## Stage 07c — the end-of-floor sweep

- **Did:** a win now opens a sweep instead of ending the floor: `constant.magnet_speed` (24) and
  `constant.sweep_timeout_seconds` (2) are new `constant` columns (Schema 4 → 5), and
  `SweepSystem` pulls every remaining pickup in at that speed and pays it once, while the tick runs
  only the sweep and the frame publisher — the phase stays `Running`, the remaining ghosts stand
  still and no damage lands. At the cap the rest is collected at once, so the sweep always ends; a
  loss never sweeps and the shared contract suite is unchanged
  ([ADR 0027](../docs/adr/0027-end-of-floor-sweep.md)).
- **Result:** dotnet **525** passed (+6, the shared contract suites green on both factories),
  `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0** (EditMode **157/157**, one new
  presentation case proves `viewsGrownDuringPlay = 0` across a real sweep), the seeded state hash pin
  unchanged, and the 300-ghost/500-projectile benchmark still **0 bytes/tick**.
- **Next:** Stage 11 (Thai ghost AI, Pob boss, balance) should read the changed 200-seed picture: a
  won floor now pays every drop, so field income rises (runs cleared 26/200 in this stage's sample)
  — that is a balance input, not a tuning result.

## Stage 10e — a synthesised LLM wiki, generated from data/ and linted

- **Did:** added the wiki's content layer — `llmwiki/content/` (8 pages: items, skills, synergies, upgrades, enemies, floors, economy, index) generated from `data/` by `tools/gen-wiki.ps1` — plus `llmwiki/SCHEMA.md` (three layers, routing table, query/ingest/lint cycles, the data-wins rule), `llmwiki/INGEST.md`, `llmwiki/log.md`, and `tools/verify-wiki.ps1` (regeneration diff, links/anchors, id coverage, orphan notes, a WARN heuristic) with three broken-wiki fixtures. `llmwiki/index.md` grew a Content section; `AGENTS.md` gained the SCHEMA reading row and the "a `data/` change regenerates the wiki" DoD line. No `lib/`, `UnityProject/` or `data/` change and no game rule change.
- **Result:** `verify-wiki.ps1` PASS (8 pages identical, 498 links/anchors, 43 ids, 0 orphans, 0 WARN) and FAIL on all 3 fixtures for the right codes; `-Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0**, 237.4 s cold, wiki step **1.4 s** (1.15 s inside the tool) against the ~5 s budget; dotnet **525** and EditMode **157/157** unchanged. The five content questions were answered from `content/` alone and matched `data/` (9/9 machine checks). Full with the Editor closed is **PASS 12 | FAIL 0 | NOT RUN 2** (WebGL build 17.5 s, Playwright 10/10 in Chrome, 130.6 s), leaving only the Unity steps NOT RUN.
- **Next:** any Stage that changes `data/` must run `tools/gen-wiki.ps1` and commit `llmwiki/content/` (AGENTS DoD); ingest stage reports through `llmwiki/INGEST.md`. The LLM ingest was exercised once (10d, source back-link added) but is not an automated test.

## Stage 09k — a drag keeps the grabbed cell, and the line is level

- **Did:** `CampPresenter.DragStart(piece, rotation, grabbedCell)` now records the pressed cell inside the piece's own footprint and every move/turn recomputes the origin as pointer − grabbed cell (turned with `FootprintRotation`), so the ghost and the placed piece agree; `Drop` clears the selection whether the drop was accepted or refused; `CampLink` carries the two cells its line joins and the view draws cell-centre to cell-centre, so the mark is level by construction (a vertical pair keeps its combo but draws no mark); item names moved into their own containers above the line container. No Core, `data/`, localization or scene change.
- **Result:** `-Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (273.7 s) with dotnet **563** and EditMode **168/168**; EditMode `CampPresenterTests` **33/33**, PlayMode `CampBagDrag` **16/16**, `CampSynergyLine` **10/10**, `TitleScene` **5/5** when each fixture is run on its own. Running the whole PlayMode assembly in one session gave **32/62** — the 30 failures happen at fixture setup inside the Input System (`AddStateChangeMonitor` → `IndexOutOfRangeException`, `Cached unprocessed value`), in suites this stage did not touch, and each of those suites is green when run alone.
- **Next:** run the Full suite on the commit before this one to prove the one-session PlayMode failure predates 09k; a 1280×720 screenshot path still does not exist (the Game View size cannot be driven and a RenderTexture render has no UI in Play Mode).

## Stage 09j — combo lines draw only between pieces that touch

- **Did:** moved the "which pairs get a permanent line" decision into `CampPresenter.DrawnLinks` — a `SameRow`/`SameColumn` pair now draws a line only when `PieceInstance.IsAdjacentTo` says the two pieces share a board edge (Core's own adjacency predicate, no rule copied), and `CampBagScreenView.PlaceLine` draws edge-to-edge between the two drawn `RectTransform`s instead of centre-to-centre, so the mark no longer runs through the two names. Fixed the camp's two lifetime NREs (`CampPlayerView.Bind` set `_bound` before resolving, `CampSceneDirector.Publish` had no presenter guard). No `data/`, Core, localization or scene change.
- **Result:** `-Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** with EditMode **163/163** (4 new presenter cases: touching / one-cell gap / diagonal / two weapons) and PlayMode `CampSynergyLine` **10/10** + `CampBagDrag` **13/13**; a live Camp session logs no NRE. Evidence `artifacts/ui/stage09j-touching-1920x1080.png` (70 red pixels) and `stage09j-apart-no-line-1920x1080.png` (0). A 1280×720 capture is NOT RUN — the Game View size cannot be driven from a script and a camera-to-RenderTexture render in Play Mode contains no UI.
- **Next:** the third dark vertical line the previous stage reported is still unexplained (needs a board hierarchy dump at the moment it appears), and the two-size screenshot gate needs a capture path that works in Play Mode.

## Stage 09k-data — a tome empowers the weapon it touches

- **Did:** `data/tables/synergy.csv` row `synergy.tome_row_damage` changed `SameRow` → `AdjacentPair`
  (id, tags, `Damage` +20 %, `max_stacks` 2 unchanged; no code, no schema, no `SchemaVersion`), the
  tome's Thai/English description now says "placed together/touching" instead of "same row" (and the
  English rule name `Tome Row Surge` became `Touching Tome`), and the
  rule's decision is recorded in [ADR 0031](../docs/adr/0031-a-tome-touches-its-weapon.md), which
  supersedes the shipping choice of ADR 0015 (both same-line kinds stay, unshipped). Core tests were
  rewritten around contact — a four-edge `[Theory]`, the one-cell gap, the corner touch, two weapons
  on different rows, the three-tome cap, a move-and-recompute case, and a 10 000-command randomized
  walk that re-derives the pair from the cells — and the camp's EditMode line cases now supply the
  same-line kind themselves, plus one new case pinning that a touching tome pair is a combo that
  draws **no** permanent mark until Stage 09l.
- **Result:** `tools/test.ps1 -Suite Fast` PASS with dotnet **571** and EditMode **169/169**; data
  gates (gen/validate/verify/fixtures/wiki) all PASS, config hash `F651981D…`. `tools/sim.ps1
  --seeds 200` before/after over the four bot builds: boss win rate 45.0 → 44.5 % (default; one seed
  of 200 changed outcome) and 58.0 → 58.0 %, 37.8 → 37.8 %, 31.0 → 31.0 % for ranged/melee/familiar —
  the greedy camp bot almost never stands the 2×2 tome against a weapon, so the instrument cannot
  exercise the rule.
- **Next:** Stage 09l gives `AdjacentPair` its own permanent mark in `CampPresenter.DrawnLinks` (the
  line the tome rule used to get from the `SameRow` case is gone) and rewrites
  `CampSynergyLinePlayModeTests` + the two line cases in `CampBagDragPlayModeTests`, which describe
  the old "touching vs one cell away" shape.

## Stage 09l — the จัดย่าม screen: the tome's line, the bag's real shape, the drag

- **Did:** chose the line by the rule's own two tags (`tome`+`weapon`) instead of `synergy.kind`, drew it
  between the two pieces' drawn middles (`CampLink` lost its cells), and drew a bag as the cells its
  footprint covers (the L basket's four cells, pooled per view); diagnosis in the report: the grab
  offset already existed (09k) and the L-basket refusal is Core's `inventory.item_not_covered`, so no
  drag code changed — [ADR 0032](../docs/adr/0032-a-touching-tome-draws-centre-to-centre.md).
- **Result:** `-Suite Fast` PASS with dotnet **573** and EditMode **167/167**; PlayMode camp suites run
  through the open Editor: `CampSynergyLinePlayModeTests` **9/9**, `CampBagDragPlayModeTests` **20/20**
  (a first-test-of-run input flake remains, 1 run in 3 — recorded in the report); pictures from the real
  Game View in `artifacts/ui/stage09l-*` (the `Tools/5` capture renders the panel-less frame, reported).
- **Next:** 12 (save, resume, settings) — or give the L bag's drag ghost its real shape, which this
  stage left as the bounding box.

## Stage 12 — save, resume and settings

- **Did:** one MessagePack checkpoint envelope for a run (schema/config hash, seed + the three RNG
  streams, floor, HP, bia, bag/chest, shop flags, refresh count) with a single serialized writer, a
  camp debounce plus an awaited save before Battle, and a WebGL flush that is *polled* from `Update`
  instead of completed from the browser callback of `FS.syncfs` ([ADR 0033](../docs/adr/0033-application-awaits-capture-the-caller-context.md));
  the title screen gained volume / TH-EN / starting-speed settings.
- **Result:** `dotnet` **591**, EditMode **172/172**, PlayMode **66/66 across the nine classes**,
  WebGL build **OK**, Playwright **13 passed / 13 skipped** (persistence spec 3/3). The PlayMode suite
  caught a real regression the browser could not: the new save-warning label swallowed shelf clicks
  (`raycastTarget`), fixed in the camp builder — [report](../docs/stages/12-report.md).
- **Next:** 13 (content complete and UX: 14 items, 5 floors, tutorial, audio) — or a real
  crash-during-flush test for the `.bak` path.

## Stage 09m — a piece's colour after a move (bug fix)

- **Did:** measured the จัดย่าม piece colours with a real pointer drag (PlayMode) and found the cause in
  `CampBagScreenView`: the drop repaints the board from inside the call while `_selectedPiece` still names
  the piece, so the moved piece was painted `(0.950, 0.850, 0.300, 1.000)` (selection) instead of its
  category colour. One function now decides a piece's colour at paint time and the selection is cleared
  *before* every command that publishes the board (drop, Escape, click-to-place, closing the panel).
- **Result:** five new PlayMode colour cases, `CampBagDragPlayModeTests` **25/25** (and the first one fails
  on the old order, with the old colour in the message); Fast suite PASS (dotnet **591**, EditMode
  **172/172**); 1920x1080 before/after shots in `artifacts/ui/stage09m-*` — [report](../docs/stages/09m-report.md).
- **Next:** 13 (content complete and UX) — or give the L bag's drag ghost its real shape, still open from 09l.

## Stage 09n — a shelf offer explains itself before you rent it

- **Did:** gave the shelf tooltip its own read model — `RunSession.ExplainOffer(slot)` resolves the
  offer through `LoadoutResolver` (the same core a tier-1 copy in the chest uses, so still no second
  calculator), lists the synergy rows it could pair in, and takes its rent verdict from the new
  `CheckPurchase`, which `Purchase` itself now calls. The pointer's shelf comes from the click's own
  hit test; the shelf the player walks up to prints the same numbers on its board.
- **Result:** dotnet **598**, EditMode **178/178**, PlayMode camp classes **52/52** (new
  `CampShelfTooltipPlayModeTests` 5/5, including 0 bytes allocated moving inside one shelf),
  `UiLayoutCheck` PASS at 1920×1080 and 1280×720; four Game View shots in `artifacts/ui/stage09n-*`
  (weapon, charm, bag, "เบี้ยไม่พอ") — [report](../docs/stages/09n-report.md).
- **Next:** 13 (content complete and UX) — or the still-open world-label legibility at 1280×720.

## Stage 13a — content complete, text fit and attribution

- **Did:** pinned the GDD's prototype content with a test (14 pieces in six categories, every piece
  merging to tier 3, three ghosts + two-phase Pob, five floors and their escalation, five combos);
  moved the last two player-facing literals into localization and added two architecture guards for
  embedded text and unknown keys; added `UiTextFitReport` (`__projectSurviveTextFit`) that measures
  every label in the player (rect, needed size, rendered size, missing glyphs) and asserted it from
  Playwright on all four screens; wrote `ATTRIBUTION.md`.
- **Result:** Fast suite **PASS 11 | 0 | 3** (601 xUnit, 178/178 EditMode); WebGL rebuild and **13/13**
  browser tests green with Title 9 / Camp 35 / Battle 30 / Result 7 labels, **0 overflow, 0 missing
  glyphs**; the audit found and fixed a clipped MP caption and nine too-short HUD boxes (Editor capture
  0 overlaps at both sizes); `THSarabunPSK` flagged as a Stage 15 must-remove (non-OFL, unused) —
  [report](../docs/stages/13a-report.md).
- **Next:** 13b (tutorial buy → place → rotate → combo → depart) and then 13c (CC0 audio, pause,
  audio settings); the human playtest acceptance waits for 13b.

## Stage 13b — the first-run checklist (2026-10-22)

- **Did:** a five-step first-run checklist in the camp (rent → place → rotate → read the tooltip →
  depart) that ticks only from commands that succeeded, prints in the จัดย่าม panel's instruction
  block and the status bar, and writes one flag into the settings file (`SettingsDto` key 4) when the
  last step lands; `CampTutorial` (Presentation, no UnityEngine) holds the state, `CampSceneDirector`
  persists it, and five new tests in `CampTutorialTests` drive the presenter's real commands.
- **Result:** Fast suite green (601 xUnit, **185/185** EditMode); PlayMode **75/75** per class (the
  bulk run is §7's ambient flake again); WebGL rebuild + **14/14** browser tests, including a new camp
  spec that walks the checklist, departs, and reads `tutorialCompleted` back from the persistence
  report; camp text audit 36 labels, 0 overflow, 0 missing glyphs. The checklist's first draft used
  `✓`/`▶` markers, which the font has no glyphs for — found by the audit, now `[x]`/`[>]`/`[ ]`
  ([gotcha](gotchas/symbol-glyphs-are-not-in-the-ui-font.md)).
- **Next:** the human playtest (Stage 13's headline acceptance — a real person reaching the first
  battle unaided, still **NOT RUN**) and 13c (CC0 audio, pause, audio settings).

## Stage 13c-1 — sound (2026-10-22)

- **Did:** `tools/gen-audio.py` synthesises all 13 clips (11 effects, two 6 s loops, mono 22 050 Hz PCM,
  seeded LCG); `AudioDirector` on the App scope owns eight pooled voices, one music source and the game's
  only `AudioListener`; presenters ask for a cue on an R3 channel and only after a command succeeded, and
  `BattleAudioListener` reads the battle event channel. The player publishes `__projectSurviveAudio`.
- **Result:** 601/601 xUnit, **193/193** EditMode, WebGL Development build (0 C# errors) + **16 passed /
  13 skipped / 0 failed** browser tests incl. two new audio specs; music switched to `DecompressOnLoad`
  after the 20-visit memory test caught a decoder buffer per `Play`. PlayMode did **not** re-run after the
  final two edits (Editor bridge lost), so the stage is not closed as PASS — see
  [report](../docs/stages/13c-1-report.md), [system note](systems/audio.md).
- **Next:** 13c-2 (pause + the audio settings panel), a PlayMode re-run, and the Stage 13 human playtest
  with headphones, still **NOT RUN**.

## Stage 13c-2a — the audio settings panel (2026-10-22)

- **Did:** the title's settings panel gained music and effects rows beside the master, persisted and
  applied live (`AudioDirector` reads `UiState.MusicVolume`/`SfxVolume` instead of serialised levels);
  the settings file went to schema 2 with a declared read path for schema 1, because an absent
  MessagePack float decodes as 0 and 0 is a legal volume ([ADR 0034](../docs/adr/0034-settings-schema-and-per-bus-volume.md)).
- **Result:** 603/603 xUnit, **194/194** EditMode, WebGL Development build (0 C# errors) + **17 passed /
  13 skipped / 0 failed** browser tests, including a spec that opens the panel, presses a bus and reads
  both the panel's document and storage. The first capture showed the title bleeding through the 96 %-opaque
  panel and blank ± controls; both fixed in `TitleSceneBuilder` and re-captured
  ([screenshot](../artifacts/stage13c-2a/web-title-settings.png)). PlayMode still cannot run (Editor
  bridge), so the stage is code-complete, not PASS.
- **Next:** 13c-2b (pause overlay), a PlayMode re-run, and the Stage 13 human playtest with headphones.

## Stage 13c-2b — the pause overlay (2026-10-22)

- **Did:** the arena gained หยุด: a HUD control opens a card above the HUD and stops the fixed step
  (`BattleSimulationRunner.SetPaused`, the accumulator reset on resume), with เล่นต่อ and กลับศาลา.
  The retreat is a new Application command, `RunSession.AbandonFloor(currentHp)` — the floor is not
  settled, so the run keeps its purse, floor cursor and shelves, takes the damage back, unlocks the
  bag and can depart for the same floor again; `RunCommand.AbandonFloor = 13` logs the HP because no
  `BattleResult` exists for it. Five `ui.pause.*` rows, the overlay report `__projectSurvivePause`
  (rewritten twice a second only while open), and a new EditMode layout case over the authored card.
- **Result:** 604/604 xUnit, **199/199** EditMode, WebGL Development build (0 C# errors,
  122,292,971 B) and **18 passed / 13 skipped / 0 failed** browser tests, including a spec that
  freezes the floor, resumes it and leaves for the camp at floor 1
  ([screenshot](../artifacts/stage13c-2b/web-battle-pause.png)). The first capture found a card that
  did not read as a menu (fixed to 0.98 alpha over a lighter surface) and the first browser run found
  the overlay publishing before the director attached the floor (fixed with a first-frame republish).
  A stale localization row pin (228 vs the 232 rows since 13c-2a) also failed at HEAD and is corrected
  to 237. PlayMode still cannot run (Editor bridge), so the stage is code-complete, not PASS.
- **Next:** the PlayMode re-run, the Stage 13 human playtest (audio mix + pause feel), then Stage 14.
