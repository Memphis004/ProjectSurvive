# Stage 09 — ศาลาพัก: the camp scene and the จัดย่าม screen

The stage arrived as three prompts — **09** (the Camp scene and the จัดย่าม screen), **09b** (make
the bag screen actually usable: diagnose the drag first, then the layout) and **09c** (make the camp
walkable, with the shop on shelves in the scene). This report covers the stage as a whole, its
measurements and its acceptance table; the two sub-stages' own required artifacts are
[`09b-report.md`](09b-report.md) (the diagnosis, with evidence) and
[`09c-report.md`](09c-report.md) (the walkable camp).

What the stage had to do, in the GDD's own words: a 2.5D camp where the player rents, arranges the
bag, rotates, merges, gives a piece back and restores — *"ซื้อ → วาง → หมุน → รวม → ถวายคืน →
คืนสภาพ ผ่าน UI แล้ว state ตรงกับ Core"* — with an invalid drop losing nothing, no GC spike during
a drag, and Playwright driving the real mouse and keyboard in a WebGL build.

## 1. What was built

**(a) The camp as a scene.** `Assets/Main/Scenes/Camp.unity` (fifth scene, build index 5, authored by
`ProjectSetup`'s *Stage 09/5* item) with `?scene=Camp` on the boot allow-list. `CampSceneScope`
composes it (VContainer), `CampSceneDirector` starts it and publishes the two report documents,
`CampWorldView` / `CampPlayerView` / `CampBagScreenView` / `CampStatusBarView` / `CampTooltipView`
are the views, and `CampPresenter` is the only thing that calls Application.

**(b) The บาง-MVP seam, kept.** No view computes a price, a combo or a drop verdict. The screen's
numbers (weight, the weight penalty, สมาธิ per second, the chest count, the departure gate) come from
`RunSession`'s camp read model, and a drag's legality comes from `RunSession.PreviewDrop` — a **dry
run of the same `BackpackEditor` predicates the accepted command asks**, so a green preview and an
accepted drop cannot disagree ([ADR 0019](../adr/0019-camp-screen-read-model-and-gesture-preview.md)).

**(c) The จัดย่าม screen** (`Presentation/Camp/CampBagScreenView.cs`, 1415 lines): a fullscreen
translucent overlay over the sala — board (left), chest panel with `x/10` and its ten slots (right),
incense pot (bottom-centre), four-step instruction scroll (top-right), restore + its
lock-on-restore checkbox (top-left), navigation and stats (bottom-left) — with the 09b fixes: drag
feedback (piece follows the pointer, legal/illegal cells highlighted, a refused drop puts the piece
back), a synergy line drawn between the cells of a working combo, the click-then-click fallback, and
`Escape` cancelling a drag.

**(d) The camp as a place (09c).** One shelf per shop slot standing in the sala, each with its own
piece, price, rented/empty state and lock pin in world space (*"ไม่มีรายการร้านใน UI ซ้ำอีกชุด"*), the
เซียมซี drum, and a figure that walks with WASD under a following oblique camera, clamped to the
walk area, stopped while the panel is open, with `E` renting the shelf it stands at. The shelf is
clickable without walking, which is the path a phone and the browser tests use.

**(e) The two documents the browser reads** (`CampReport`): `__projectSurviveCamp` (the run, the
screen, the counters, the gate) and `__projectSurviveCampGeometry` (every board cell, chest slot,
shelf station, altar pot and the figure as on-screen rectangles). A WebGL player cannot run Unity's
test framework, so what a browser test can see is what the player published about itself.

**(f) Rules and data.** `Domain/Run/BagDrop.cs` (the zone/action/preview vocabulary),
`RunSession.PreviewDrop` + `SetBagLocked` + the camp read model, `BackpackEditor`/`Board` changes it
needed, and **81 new localization rows** (145 in total) for the camp screen and its failure codes.

**(g) Tests.** 9 EditMode (`CampPresenterTests`), 16 PlayMode (8 `CampBagDragPlayModeTests` over real
pointer events, 8 `CampWorldPlayModeTests` — walking, `E`, clicking a shelf), 11 dotnet
(`BagDropPreviewTests`), and 4 browser tests in `tests/web/camp.spec.ts`.

**(h) One tool fixed, one decision recorded.** `tools/build-web.ps1` called `Confirm-Build` above its
own definition, so the first build of this stage died with *"not recognized as the name of a
cmdlet"* and produced no player at all; the call now sits below the function and `-Force` exists for
an unattended run. [ADR 0019](../adr/0019-camp-screen-read-model-and-gesture-preview.md) records the
preview-as-dry-run rule, the frozen state document during a gesture, and why the shop is a place.

## 2. Files that matter

| Path | What it is |
| --- | --- |
| `UnityProject/Assets/Main/Scenes/Camp.unity` | the camp scene (new, build index 5) |
| `Assets/Main/Scripts/Camp/CampSceneScope.cs`, `CampSceneDirector.cs` | composition root; start + the two published documents |
| `Assets/Main/Scripts/Presentation/Camp/CampPresenter.cs` | every command the screen runs, and the drag state machine (1126 lines) |
| `.../CampBagScreenView.cs` | the จัดย่าม overlay: cells, drag, synergy lines, tooltips (1415 lines) |
| `.../CampWorldView.cs` | the sala, the four shelves, the drum, world clicks, the camera |
| `.../CampPlayerView.cs` | the walking figure (WASD, clamped to the walk area, stopped while the panel is open) |
| `.../CampStatusBarView.cs`, `CampTooltipView.cs` | the bottom bar with the departure gate; the piece tooltip |
| `.../CampReport.cs`, `CampModels.cs`, `CampUiState.cs`, `CampScreenRects.cs` | the published documents (713 lines of hand-rolled JSON) and the screen's models |
| `Assets/Main/Scripts/Editor/ProjectSetup.cs` | +486 lines: the Camp scene authoring, the URL allow-list, the build list |
| `lib/Runtime/Domain/Run/BagDrop.cs` | zones, actions, `DropPreview` |
| `lib/Runtime/Application/Run/RunSession.cs` | +169 lines: `PreviewDrop`, `SetBagLocked`, the camp read model |
| `lib/Runtime/Domain/Inventory/BackpackEditor.cs`, `Board.cs`, `Config/Footprint.cs` | the predicates the preview and the commands share |
| `data/localization/localization.csv` | +81 rows (`camp.*`, `error.*`) |
| `Assets/Main/Tests/EditMode/CampPresenterTests.cs` | 9 tests: the seam, the preview/command agreement, the 0-byte drag, Escape, the bottom bar |
| `Assets/Main/Tests/PlayMode/CampBagDragPlayModeTests.cs` | 8 tests driven by real pointer events through the scene's own input module |
| `Assets/Main/Tests/PlayMode/CampWorldPlayModeTests.cs` | 8 tests: walking, `E`, clicking a shelf, the clamp, the panel stopping the walk |
| `lib/Tests~/Core.Tests/Run/BagDropPreviewTests.cs` | the preview against the real tables |
| `tests/web/camp.spec.ts` | 4 browser tests: the sala's controls, the whole flow, walk + `E`, two resolutions |
| `docs/adr/0019-camp-screen-read-model-and-gesture-preview.md` | the stage's decision record |
| `tools/build-web.ps1` | the `Confirm-Build` ordering fix and `-Force` |

## 3. Commands actually run

```powershell
# Rule engine, architecture, the sim, and the static-data gates (lib/, Release)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Configuration Release
#   -> 11/11 checks, 425 xUnit passed, generated code reproducible, 17 broken fixtures rejected

# data/ -> generated code + tables (needed: 81 localization rows had never been regenerated)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1   # 8 PASS / 0 FAIL

# Unity Editor suites (MCP pipeline, Editor open)
run_tests --mode editor                                  # 57 tests
run_tests --mode playmode --async_tests                   # 24 tests
run_tests --mode playmode --async_tests --filter CampBagDragPlayModeTests   # after the fixture edit

# The real player, in a real browser (Editor closed first:
# artifacts/close-unity-editor.ps1 - the project lock cannot be shared with a batch-mode build)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Configuration Release -Force
node tools/serve-web.mjs --port 8080                      # already running from the fixture run
cd tests/web ; npx playwright test smoke.spec.ts battle.spec.ts camp.spec.ts
```

## 4. Results and where the artifacts are

| Gate | Result | Artifact |
| --- | --- | --- |
| `tools/test.ps1 -Configuration Release` | **11 / 11 PASS**, 425 xUnit passed, deterministic sim digests, generated code reproducible, 17 fixtures rejected | `artifacts/stage09/dotnet-tests.log`, `artifacts/dotnet/*` |
| `tools/gen.ps1` + `verify-generated.ps1` | **OK** — 33 `.cs`, 12 `.bytes`, `configHash 9B9F302A…`; regeneration reproduces the tracked output | `artifacts/stage09/gen.log` |
| Unity EditMode (all) | **57 / 57 passed**, 5.7 s — 9 are `CampPresenterTests` | MCP result payload |
| Unity PlayMode (all) | **24 / 24 passed** in two back-to-back full runs — 8 pointer-drag + 8 world + 4 battle + 4 shell | MCP result payload |
| PlayMode, `CampBagDragPlayModeTests` alone | **8 / 8 passed** after the fixture hardening of §5 | MCP result payload |
| WebGL build (Release, CLI) | **Succeeded** — 64,426,152 bytes, **0 errors, 1 warning**, 201.4 s, 5 scenes incl. Camp, `sizeProfile: none` | `artifacts/stage09/unity-build-web.log`, `build-info.json`, `build-report.json` |
| Browser suite (Release player, Chrome, 127.0.0.1:8080) | **8 / 8 passed** (4 camp + 2 battle + 2 smoke), 1.9 m | `artifacts/stage09/playwright-web-final.log` (the earlier run: `playwright-web.log`) |
| Resize test alone, 12 runs after the click retry of §5 | **12 / 12 passed** — the retry covered a lost click in the majority of runs | `artifacts/stage09/playwright-resize-probe.log` |
| Screenshots, closed camp and panel at 1280×720 and 1920×1080 | saved | `artifacts/stage09/screenshots/web-camp-*.png` |
| Editor console after the runs | **0 errors**; warnings are the two pre-existing ones of §5 | MCP `console` payload |

The browser evidence, verbatim from `playwright-web.log`:

```jsonc
// the smoke self-check in the same player - the tables it really holds
{"platform":"WebGLPlayer","configHash":"9B9F302AAD2D33FBFB51C3A5DB110E8BDA727E8190023C269113B6241D68FDC9",
 "probeCount":7,"failedCount":0,"passed":true,
 "probes":[ …, {"name":"config.tables","passed":true,
   "detail":"hash=9B9F302A generated=9B9F302A localization=145 items=11 enemies=4"}, … ]}

// the camp's own report, when the whole flow has run (counters, verbatim shape)
{ … "bia":10,"chestUsed":0,"backpackRevision":11,"bagVisible":true,
  "counters":{"rented":1,"placed":1,"merged":1,"recycled":1,"rotated":1,"resets":1,
              "dragPreviews":50,"invalidPreviews":30,"invalidDrops":1,"dragMoves":23,
              "dragRotations":0,"dragCancels":1,"departures":0}, … }

// the two resolutions the layout is measured at (canvas element resized, as fullscreen does)
camp at 1280x720 (closed): statusBar {"x":16,"y":10.667,"width":640,"height":126.667} openBag {"x":640,"y":169.333} shelves 4 controls 11
camp at 1920x1080 (closed): statusBar {"x":24,"y":16,"width":960,"height":190} openBag {"x":960,"y":254} shelves 4 controls 11
camp at 1280x720 (panel open): board {"x":26.667,"y":213.333,"width":493.333,"height":386.667} closeBag {"x":70,"y":126}
camp at 1920x1080 (panel open): board {"x":40,"y":320,"width":740,"height":580} closeBag {"x":105,"y":189}
```

The walking test's own line: `camp: W walked the figure 1.25 units in 0.4 s`. The battle and Thai
spike suites still pass on this build, so the camp did not disturb them (1800 ticks,
`viewsGrownDuringPlay: 0`, 0 dropped events; 6 Thai rows with 0 missing glyphs).

## 5. What was not tested, and why

- **A browser *window* resize.** Unity's default desktop template pins the canvas' CSS size at
  960×600, so a 1920×1080 viewport leaves the player at 960×600 and republishes nothing. Measured
  with a throwaway probe (viewport 1280×720 → report 960×600; 1920×1080 → 960×600; one publish).
  What *is* tested is the size path a player actually has — the canvas element itself, which the
  template's fullscreen button and a HiDPI backing store both change: styling it to 1280×720 and
  1920×1080 moves the report to those sizes and the layout is asserted at each (§7, 09b item B). A
  template that fills the window would make the *window* a resize path too; that changes the
  reference configuration of every browser measurement in the project, so it is a decision for the
  owner rather than a repair, and it is listed in §6.
- **A Profiler capture of a drag.** The acceptance says *"ไม่มี GC spike ระหว่างลาก (ดูจาก Profiler)"*;
  the measurement taken instead is an allocation count, which is exact where a flame chart is not:
  `CampPresenterTests.A_long_pointer_sweep_allocates_nothing` asserts **0 bytes** for 2000 pointer
  moves inside one cell **and** 0 bytes for 2000 moves sweeping the whole board (`GC.
  GetAllocatedBytesForCurrentThread`). No frame-time capture of a drag in a browser was taken.
- **A second run of `Escape` in the browser.** Escape-cancel is asserted in EditMode
  (`CampPresenterTests.Escape_cancels_the_drag_and_the_picture_comes_back`) and end-to-end in the
  browser flow test (step 9), and it is that browser step whose first version **failed** and taught
  the lesson below; the browser cancel is therefore measured once per run, not repeatedly.
- **The whole PlayMode suite after the last fixture edit.** The edit was to
  `CampBagDragPlayModeTests.LoadCamp` only, so the affected fixture was re-run (8/8, §4) rather than
  the 24-test suite a third time.
- **Anything on a device other than this desktop Chrome.** ADR 0003's rule: a measurement is only
  about the browser it was taken in.

Two things learned the hard way are recorded here because a test will hit them again:

1. **A gesture cannot be observed while it is live.** `CampSceneDirector.Publish` returns early when
   a drag is active (the state document is a string concat, and the GDD calls a per-pointer-move
   rebuild a hot path), so `dragging: true` is never published. The first version of the browser
   cancel test waited for exactly that and timed out at 15 s
   (`artifacts/stage09/playwright-camp.log`, `camp.spec.ts:765`). The assertions now sit on the
   *release*: `dragPreviews` for the pick-up, `dragCancels`/`placed`/`invalidDrops` for the outcome.
2. **A synthesised click into the player is sometimes lost — and waiting does not fix it.** With a
   single click, the resize test's *close* control went unregistered in **3 of 4 runs** and its
   *open* control in 1 of 5; adding a deliberate pointer move plus a two-second pause before the
   click did not help (open lost 1 of 4, close 1 of 4 with the pause in place). It is not the aim:
   the canvas box reads back identical to the box at load (`{x:160,y:39,width:960,height:600}` before
   and after the two resizes). `clickUntilPublished` in `tests/web/camp.spec.ts` therefore retries the
   gesture at most three times and the caller asserts the same thing it always did — the command has
   to appear in the published document. The retry fired on attempt 1 in 2 of the 4 runs after the
   change, registered on attempt 2 every time, and the test has not failed since: **0 failures in 12
   runs** (`artifacts/stage09/playwright-resize-probe.log`). The mechanism is not established; a
   flake that is *reduced*, not explained (see §6). It is a property of the synthesised CDP gesture
   over a stalled WebGL frame as far as anything here shows — no human-handed measurement was taken,
   so it is **not** claimed to be a player-facing defect.

## 6. Blockers and risks

- **The reference configuration of the browser is 960×600 canvas in a 1280×720 window.** Every
  browser measurement in Stages 03–09 was taken there. Making the desktop canvas fill the window is a
  small template change (`Assets/WebGLTemplates/<name>/` with Unity's own template modified in two
  places: the desktop branch's `canvas.style.width/height` and `.unity-desktop`'s size) but it moves
  the configuration under ADR 0011's size baseline and every browser claim, so it is left as a
  recommendation, not taken.
- **The generated tables were stale when this stage's verification started.** `data/localization/
  localization.csv` had gained 81 rows that `lib/Runtime/Generated`, `lib/Tests~/data` and
  `Resources/Config` had never received: `verify-generated` failed on `tblocalization.bytes` and
  `ConfigBuildMetadata.g.cs`, and the config hash in the working tree (E46C…) did not match the
  tables (9B9F…). It also means the **first browser evidence of this stage described a player with
  64 localization rows, not 145** — the camp screen's labels were not in it. `tools/gen.ps1` then
  `verify-generated.ps1` (8 PASS) fixed the tree, the pinned count in `ConfigLoadTests` was corrected
  from 144 to the real 145, and the player was rebuilt so the browser evidence matches the data.
  *Rule 4 of AGENTS.md already says this; the cost was a rebuild, not just a regeneration.*
- **Two pre-existing warnings, neither from this stage.** The build's one warning is the Unity
  Pipeline package's `No RuntimePipelineConfig asset found … Pipeline will be disabled in Player
  builds` (an Editor-only package; present in the Stage 03b log too). The Editor console also carries
  `WebSizeProfiles.cs(123,19): warning CS0618: WebGLWasmArithmeticExceptions.Throw is obsolete` —
  `WebSizeProfiles.cs` is untouched by this stage and the setting's replacement is a separate
  decision. Nothing new was left unexplained.
- **The PlayerSettings diff now pins the WebGL IL2CPP configuration** (`il2cppCompilerConfiguration:
  WebGL: Release`, `il2cppCodeGeneration: WebGL: OptimizeSpeed`). This is what the recorded build
  actually used (`build-report.json → sizeSettings`), so the tree and the artifact agree; it does make
  the baseline configuration explicit rather than inherited.
- **`CampBagDragPlayModeTests` needed a deterministic guard.** One full PlayMode run failed with
  *"clicking จัดย่าม did not open the panel"* (0.26 s in), and three runs after it passed. The fixture
  now calls `Canvas.ForceUpdateCanvases()` before reading the button's screen rectangle, so the aim
  cannot be a matter of which side of the frame boundary the coroutine step landed on. One observed
  failure in four runs, not reproduced since, is a flake that is *reduced*, not explained.
- **A synthesised click is lost occasionally, and the browser tests retry rather than explain it.**
  §5 item 2 has the numbers: the panel's close control was lost on the first click in 3 of 4 runs,
  the open control in 1 of 5, and neither a settle nor a pointer move prevented it; a second click
  always worked. `clickUntilPublished` retries at most three times and the assertion is unchanged,
  but the underlying behaviour — a click the player drops — is unexplained. If it is the player and
  not the harness, a hand on a mouse would notice it after a resize; that measurement has not been
  taken and is the honest reason this is a risk and not a defect claim.
- **The build had to be driven from a closed Editor.** `tools/build-web.ps1` refuses to run while
  `Temp/UnityLockfile` exists, and Unity's own `PipelineQuitScheduler` fails in the Editor
  (`DontDestroyOnLoad` from an editor script), so the Editor was closed with
  `artifacts/close-unity-editor.ps1` (the helper Stage 08 left behind) before the CLI build. The
  Unity suites therefore ran *before* the browser suites rather than after them.

## 7. Acceptance criteria

### Stage 09

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | ซื้อ → วาง → หมุน → รวม → ถวายคืน → คืนสภาพ ผ่าน UI แล้ว state ตรงกับ Core | **PASS** | `camp.spec.ts` *rents a shelf, then places, rotates, merges, gives back and restores through the real UI*: counters go `rented 1, placed 1, rotated 1, merged 1, recycled 1, resets 1` and each step asserts the run's own state (zone, tier, cell, bia, revision); `CampPresenterTests.Buying_placing_rotating_merging_giving_back_and_restoring_reach_the_same_state_as_the_same_commands_issued_directly` asserts the same equality against directly-issued commands |
| 2 | invalid drop ไม่ทำของหาย | **PASS** | browser step 3 drops a chest piece on an illegal cell: `invalidDrops 1`, `backpackRevision` unchanged, bia unchanged, the piece still in the chest, a refusal code on screen; `CampPresenterTests.An_invalid_drop_loses_nothing_and_says_which_rule_refused_it` |
| 3 | ไม่มี GC spike ระหว่างลาก (Profiler) | **PASS** (different instrument) | 0 allocated bytes over 2000 pointer moves, twice (same cell and a full sweep) — `CampPresenterTests.A_long_pointer_sweep_allocates_nothing`. No Profiler capture; see §5 |
| 4 | Playwright ทดสอบเมาส์และ keyboard focus ใน Web จริง | **PASS** | 4 camp browser tests drive real mouse gestures, `R`, `Escape`, WASD and `E` against the Release player; every click is aimed at a control the *report* named, through the canvas' own box |
| 5 | tooltip: stat, tier, synergy ที่ทำงานอยู่ + highlight ช่องที่เชื่อมกัน | **PASS** | `CampPresenterTests.The_tooltip_shows_the_tier_the_skill_and_the_combos_of_the_piece_under_the_pointer`; the synergy lines are drawn from `RunSession.EvaluateLayout()` and only for the pairs it names |
| 6 | แถบล่าง: น้ำหนัก, สมาธิ, หีบ x/10, เบี้ย, ราคา refresh | **PASS** | `CampPresenterTests.The_bottom_bar_reads_the_run_and_a_disabled_departure_explains_itself`; the bar is published as the `statusBar` region the browser asserts on |
| 7 | ปุ่มออกเดินทาง (disable + เหตุผล); กันคลิกซ้ำ; pointer ออกนอก canvas และ canvas resize | **PASS** (resize via the canvas element — see §5) | the depart click runs the real command once and a second press cannot reach it (browser step 10 and `Departing_runs_the_real_command_once_and_a_second_press_cannot_run_it_again`); the canvas-element resize drives 1280×720 → 1920×1080 → 960×600 with the report following within one publish |
| 8 | Presenter เรียก Application เท่านั้น (ห้ามเขียนกฎซ้ำใน View) | **PASS** | views hold no rule: the verdict is `RunSession.PreviewDrop`, the tooltip's numbers are `EvaluateLayout`/`BagWeight*`; `CampPresenterTests` covers the seam and the architecture tests still pin the references |
| 9 | (เพิ่ม) เดินด้วย WASD, ป้ายรายละเอียด, กด E เช่า, ปุ่ม UI ใช้ได้โดยไม่ต้องเดิน, ห้ามเดินทะลุ UI/ตอนหน้าจัดย่ามเปิด | **PASS** | 8 `CampWorldPlayModeTests` (walk + camera, the walk-area clamp, `E` at a shelf, click-without-walking, the panel stopping the walk) and the browser's *walks the figure with WASD and rents a shelf by pressing E* (`camp: W walked the figure 1.25 units in 0.4 s`, `lastWorldAction: rent:3`, goods in the chest) |

### Stage 09b

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | มีรายงานสาเหตุที่ลากไม่ได้พร้อมหลักฐาน | **PASS** | [`09b-report.md`](09b-report.md): four candidate causes checked against the scene as authored — input module vs Input System, the raycaster and its raycast targets, an invisible element over the board, and whether the handlers were called at all — with the run that measured each |
| 2 | ภาพหน้าจอ 1920×1080 และ 1280×720 ไม่มี element ทับกัน | **PASS** (canvas resized, §5) | `artifacts/stage09/screenshots/web-camp-1920x1080.png`, `web-camp-1280x720.png` (closed camp) and `web-camp-panel-*.png`; the disjointness of every published UI rect is asserted at both sizes by the browser test, and the panel's regions + ≥14 px labels by `The_panel_layout_has_no_overlapping_regions_and_no_tiny_labels` |
| 3 | เทสต์ลากด้วย pointer events จริงผ่าน | **PASS** | 8/8 `CampBagDragPlayModeTests` (real `Mouse` device → the scene's own `InputSystemUIInputModule` → the canvas' own raycaster) and the browser flow test's drags; no test that calls a use case directly is counted as a drag |

### Stage 09c

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | เดิน → เช่า → ของเข้าหีบ → เปิดหน้าจัดย่าม → ลากลง Board ผ่านจริง | **PASS** | the browser suite, split across two tests that share the entry path: *walks the figure with WASD and rents a shelf by pressing E* walks and rents into the chest; *rents a shelf, then places…* opens จัดย่าม and drags a chest piece onto the board (`placed 1`, `chestUsed 0`) |
| 2 | PlayMode จำลองการเดินและกด E; Playwright กด WASD และ E | **PASS** | `CampWorldPlayModeTests` (walking, `E`), the browser's WASD/`E` test; `The_character_stops_walking_while_the_bag_panel_is_open` also pins item 5's rule |
| 3 | ภาพหน้าจอไม่มี element ทับกัน | **PASS** | the two-resolution check above (the closed camp's UI is asserted disjoint at 1280×720 and 1920×1080; world silhouettes are deliberately not part of that rule — a tilted 2.5D sala has stations whose pictures overlap) |
| 4 | ร้านบนชั้นวาง 4 จุด แสดงไอเทม+ราคา, ไม่มีรายการร้านใน UI ซ้ำ | **PASS** | one `shelfN` + `shelfLockN` per shop slot published at every resolution; `Clicking_a_shelf_rents_it_without_walking` and `Clicking_a_shelfs_lock_pins_it_against_the_reset` drive the shelf itself |
| 5 | ปุ่ม จัดย่าม กลางล่างของจอ; ระหว่างเปิดหน้าตัวละครหยุดเดิน | **PASS** | `openBag` is the one control outside the panel, hidden while it is open (browser test 1); `The_character_stops_walking_while_the_bag_panel_is_open` |
| 6 | คลิกชั้นได้โดยไม่ต้องเดิน | **PASS** | browser test 1 checks every `rentN`/`pinN` point is on the canvas; the PlayMode click tests rent and pin with no walking |

**DoD.** Compile: 0 errors in both the Editor and the CLI Release build. Tests: 425 dotnet + 57
EditMode + 24 PlayMode + 8 browser, all green, with the two flakes-of-measurement recorded in §5 and
§6. Warnings: two, both pre-existing and named. Docs and test commands: this report, `09b`,
`09c`, ADR 0019 and `llmwiki/systems/camp.md`. Committed as `stage-09`.
