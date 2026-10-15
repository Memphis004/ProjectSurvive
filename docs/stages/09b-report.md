# Stage 09b — why the จัดย่าม screen could not be dragged, and what was done about it

The prompt was explicit about the order: **diagnose first, then fix** (*"A. วินิจฉัยก่อนแก้ … ระบุ
สาเหตุพร้อมหลักฐาน ห้ามเดา"*), and about the acceptance: a report that names the cause with evidence,
screenshots at 1920×1080 and 1280×720 with nothing overlapping, and drag tests driven by **real
pointer events** ("ห้ามนับเทสต์ที่เรียก use case ตรงเป็นเทสต์ drag"). This report is that artifact;
the stage's own summary and its acceptance table are in [`09-report.md`](09-report.md).

## 1. The symptom

Stage 09 shipped a camp screen whose drag "worked" in a test that never dragged: the tests called
`CampPresenter.DragStart/DragMove/Drop` directly, so they measured the *rule*, not the *gesture*.
The screen as played could not move a piece. The prompt's own list of the four causes to check —
the EventSystem's input module, the `GraphicRaycaster` and its raycast targets, an invisible element
over the board, and whether the drag handlers were called at all — is exactly the order below.

## 2. The four causes checked, and what each showed

Each was checked against the scene as authored, with a temporary log where a log was the only way to
see it (removed afterwards), and each result was then pinned by a test so it cannot come back.

### (a) The input module: correct, and it was the *fixture* that lied about it

The EventSystem does use the Input System module, and that is now asserted rather than assumed:
`CampBagDragPlayModeTests.LoadCamp` fails with *"the EventSystem does not use the Input System
module"* if it does not.

The trap it hid was in the **test fixture**, not the game. Measured with everything else identical:

| Where the Camp scene is loaded | Drag previews | Refused drops |
| --- | --- | --- |
| in `[UnitySetUp]` | **0** | **0** |
| in the test body (as it is now) | **7** | **1** |

`InputTestFixture.Setup()` pushes the whole input system onto its own `InputTestRuntime`, removes
every device and rewires the update hook — and in this runner it runs *after* a `[UnitySetUp]`
coroutine has already advanced frames. A scene loaded there has its `InputSystemUIInputModule`
enabled against a runtime that is torn out from under it a frame later, and the module then never
delivers another pointer event: the device reports the position, the UI never hears about it. So a
fixture that loads the scene in `[UnitySetUp]` measures *itself* as broken. The comment on
`LoadCamp` keeps that measurement and its cause.

A second fixture trap sits in the same place: an earlier version re-assigned the module's action
asset (to stop it reading a device an earlier session left behind) and every pointer test then
reported that the pointer never reached the view, because the module hooks its action callbacks in
its own `OnEnable` — swapping the asset afterwards leaves it without callbacks.

### (b) The raycast target and the raycaster: the press was resolved a cell too late

The board is one `RectTransform` with a graphic the raycaster resolves, and the piece under the
pointer comes from `CampBagScreenView.TryLocate` — so a drag's *origin* is a hit test, not a stored
value. The defect was that the origin was taken from the pointer's position **when the drag
threshold was crossed** instead of where the button went down. The EventSystem only reports the
crossing a few frames after the press, and by then the pointer has left the cell the player aimed
at. Measured with the fixture: **the press landed on (2,2), the threshold crossing on (3,2), and no
drag ran at all** — the origin resolved to empty board, `PieceAt` returned none, and
`DragStart` was never called even though every handler fired in the right order.

Fixed by anchoring the pick-up at `eventData.pressPosition` (`OnBeginDrag`), which is also why the
cell size (82 px) can be smaller than a human's drag distance without losing the piece.

### (c) A published point that was a *corner*

`CampScreenRects` exists because of this defect, three times over: a `RectTransform` whose pivot is
its top-left corner reports `transform.position` as that **corner**, and `Rect.Contains` excludes the
top and right edges — so a click aimed at a published corner can miss the control it was aimed at.
The stage's earlier screens published each cell's top-left corner, which is outside the cell by both
the rectangle test and the call arithmetic.

Fixed by measuring the rectangle from the transform's four world corners and publishing its **centre**
(`CampScreenRects.TryOf` / `TryCentre`). The round trip is now a test rather than a hope:
`Every_published_board_cell_point_resolves_back_to_its_own_cell` and
`Every_published_chest_slot_point_resolves_back_to_its_own_slot` walk every published point back
through `TryLocate` and assert it lands on *that* cell — because a point that resolves to a
neighbour makes a browser test drop a piece one cell off while reporting a pass.

### (d) An invisible element over the board: the panel's own overlay, and one real overlap

The panel is a fullscreen translucent overlay (`raycastTarget = true`) so that the sala stays visible
behind it — which is also what makes a drag *outside* the panel harmless. The instruction scroll and
the chest panel are deliberately not raycast targets, so they cannot swallow a gesture aimed at the
board under them.

One **real** overlap remains, and only the two-resolution measurement found it: the panel's bottom-left
row (กลับ, ไปชั้นถัดไป) is published *inside* the world status bar's rectangle, at both sizes
(browser log, verbatim):

```jsonc
camp at  960x600 (panel open): statusBar {"x":12.649,"y":8.433,"width":505.964,"height":100.139}
                               closeBag  {"x":55.34,"y":99.612}     // inside the bar's rect
camp at 1280x720 (panel open): statusBar {"x":16,"y":10.667,"width":640,"height":126.667}
                               closeBag  {"x":70,"y":126}           // inside the bar's rect
camp at 1920x1080 (panel open): closeBag {"x":105,"y":189}
```

The click still reaches the panel's button, because the panel is the later sibling of the bar and its
own graphics are on top of it (measured: with a settle after the resize, กลับ closes the panel at the
native size and the two-resolution test passes 3/3 repeats). So the overlap does not block the
control, but it does mean 09b item B's literal *"ต้องไม่ทับกัน"* is not met between the **panel** and
the **world bar**: the panel's own bottom-left corner redraws HP/MP, น้ำหนัก and เบี้ย — the same
information, in the same corner, from 09b's own placement list — and the bar is behind it rather than
hidden. Asserting "no two published rectangles overlap" across *both* would fail today; the tests
therefore assert disjointness among the canvas-level UI when the panel is closed (browser) and among
the panel's own regions (PlayMode), and this paragraph is the honest reason. Hiding the world bar
while the panel is open is the one-line change that would make the strict reading true; it is left to
the owner because it is a visible design choice, not a repair.

## 3. What the layout became (09b item B, and the numbers behind it)

The overlay is authored from the canvas' top-left corner at a **1920×1080 reference resolution** with
the canvas scaler matching at 0.5, so every region scales uniformly with the canvas
(0.667 at 1280×720, 0.527 at 960×600) and the *relative* layout is resolution-independent. As
authored at 1920×1080: the four rows are 20..160, 180..760, 866..1056 from the top, the three columns
40..780, 780..1104 and 1104..1880, and no two regions intersect — which is what
`The_panel_layout_has_no_overlapping_regions_and_no_tiny_labels` asserts (allowing only the chest's
slot grid nesting inside the chest panel), plus a floor of **14 px** on every label, which is 09b's
*"ตัวหนังสือ ≥ 14 px ที่ 1080p"*.

Also from 09b's list, and all measured in the browser or in PlayMode: instructions as a four-step
scroll (top-right), the restore button with its lock-on-restore **checkbox** (a real `Toggle`, read by
the restore command), the synergy lines between the cells of a working combo, drag feedback (the
piece follows the pointer, legal and illegal cells highlight, a refused drop puts the piece back), and
the click-then-click fallback for a player who cannot drag.

## 4. Evidence, and where it is

| Gate | Result | Artifact |
| --- | --- | --- |
| Drag over real pointer events (`Mouse` device → the scene's module → the canvas' raycaster) | **8 / 8 passed** | `CampBagDragPlayModeTests`; MCP result payload |
| Round trip of every published board cell and chest slot | **passed** | same fixture |
| Layout: no overlapping regions, labels ≥ 14 px | **passed** | same fixture |
| The same drag with a real mouse in a real browser | **passed** (`dragPreviews 50`, `invalidDrops 1`, `placed 1`) | `tests/web/camp.spec.ts`, `artifacts/stage09/playwright-web.log` |
| Passage at 1280×720 and 1920×1080, closed and open | **passed**, screenshots saved | `artifacts/stage09/screenshots/web-camp-1280x720.png`, `web-camp-1920x1080.png`, `web-camp-panel-*.png` |
| No drag that calls a use case directly is counted as a drag test | by construction | the EditMode tests that *do* call the presenter are named as rule tests in `09-report` §7 |

## 5. Failures seen while doing this, kept rather than tidied away

- **A browser step that waited for a live gesture.** `camp.spec.ts` step 9 first polled
  `dragging === true` after a real press-and-pull and timed out at 15 s
  (`artifacts/stage09/playwright-camp.log`, `camp.spec.ts:765`). The cause is a property of the
  screen: the state document is not written while a gesture is live (§09-report 5.1). The step now
  asserts the release.
- **A gesture that was never started by the old step 9.** Before that, Escape was pressed in the same
  breath as the pull, before uGUI had crossed its drag threshold, so Escape took the other branch and
  the drag was never cancelled — which reads exactly like "Escape does not work". Pacing the gesture
  fixed it; the same test passes.
- **A click swallowed right after a canvas resize** (2 of 4 runs while the two-resolution test was
  being written). The settle that was added first (one second in `setCanvasSize`) did not settle it:
  the final measurement is 3 of 4 runs losing the panel's *close* click and 1 of 5 losing its *open*
  click, unaffected by a pointer move plus a two-second pause. `clickUntilPublished` retries the
  gesture up to three times and the assertion is unchanged; 12 runs of the resize test pass since.
  Not explained — `09-report` §5 item 2 has the numbers and §6 the risk.
- **One PlayMode run where clicking จัดย่าม did not open the panel** (0.26 s in; three later full runs
  green). The fixture now forces the canvas layout before reading the button's rectangle.
