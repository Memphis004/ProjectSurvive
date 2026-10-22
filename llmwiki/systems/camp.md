# System — ศาลาพัก (the camp screen)

Stage 09 (+09b, +09c, +09e, +13b). The first screen the player can *change* the run from: rent a
shelf, arrange the bag, rotate, merge, give a piece back, restore, and leave for the floor.

- Scene: `Assets/Main/Scenes/Camp.unity`, build index 5, reachable as `?scene=Camp`
  (`ProjectSetup` *Stage 09/5* authors it; the boot allow-list is `UrlSceneAllowList`).
- Composition: `CampSceneScope` (VContainer) → `CampSceneDirector` (start + the two documents) →
  `CampPresenter` (every command) → the views (`CampWorldView`, `CampPlayerView`,
  `CampBagScreenView`, `CampStatusBarView`, `CampTooltipView`).
- Rules live in `ProjectSurvive.Core.Application.Run.RunSession`; no view computes a price, a combo
  or a drop verdict ([ADR 0019](../../docs/adr/0019-camp-screen-read-model-and-gesture-preview.md)).
- Stage report: [`docs/stages/09-report.md`](../../docs/stages/09-report.md) (+ `09b`, `09c`, `09e`).

## 1. The two documents the browser reads

`CampReport` publishes onto `window` (a WebGL player cannot run Unity's test framework, so what a
browser test can see is what the player published about itself):

| Global | What it is |
| --- | --- |
| `__projectSurviveCamp` | the run and the screen: flow/floor/hp/bia/weight, the chest, every piece (`kind`, `tier`, `zone`, `x`, `y`, `chestSlot`, `rotation`), the four shop slots, the command counters, where the figure is (`playerX`/`playerZ`/`moving`/`focusedShelf`), and the gate (`canDepart`/`departError`/`statusCode`) |
| `__projectSurviveCampGeometry` | where every board cell, chest slot, altar pot, shelf station (`shelfN`, `shelfLockN`), the drum and the figure are on screen — Unity screen pixels, origin bottom-left — plus the walk area and each station's world position |

Both are read through the canvas element's own box, so a browser test's conversion survives a pixel
ratio and a canvas resize. A click is always aimed at a control the *report* named.

## 2. Three rules that bite

**(a) A live gesture is invisible.** `CampSceneDirector.Publish` writes the geometry and then returns
early while `Presenter.IsDragging` — the state document is hand-rolled JSON and the GDD calls a
per-pointer-move rebuild a hot path. So `dragging: true` is **never** observable from the page, and a
test must assert the *release* (`dragPreviews` for the pick-up; `dragCancels` / `placed` /
`invalidDrops` for the outcome). A test that polls `dragging` will time out; one did.

**(b) A preview is a dry run of the command, not a second rule.** `RunSession.PreviewDrop` asks the
same `BackpackEditor` predicates the accepted command asks (`CanPlace`, `CanMerge`,
`CanTransferToChest`, `CanRecycle`) and writes nothing: no revision, no event, no replay entry. The
only thing it decides is *intent* (which command the zone and the piece under the pointer imply). A
drop re-asks the run, so a board that moved under the pointer cannot be acted on with a stale verdict.

**(c) The UI scale reference is 1920×1080 with the scaler matching at 0.5.** Every region therefore
scales uniformly with the canvas (0.667 at 1280×720, 0.527 at 960×600) and the *relative* layout is
resolution-independent. The panel is a fullscreen translucent overlay, so the sala stays visible
behind it; since 09e every region is **anchored to a canvas edge or corner** (not placed at a fixed
pixel), which is what keeps it inside both sizes. The run's own numbers — เบี้ย, น้ำหนัก, สมาธิ, HP/MP —
are drawn **once**, on the world status bar; the panel's foot is a separate `Stats` strip that carries
only the controls the bar does not, so the two never share pixels
(`UiLayoutCheckTests`, `CampBagDragPlayModeTests.The_panel_layout_has_no_overlapping_regions…`).

## 3. The two affordances, and why both exist

From 09c the shop is **four shelves in the sala** (one per slot, each with its own price, rented state
and lock pin). Walking up and pressing `E` and clicking a station call the *same* presenter method —
the click is the phone/test path and the keyboard path, not a second rule. The additive UI buttons
(`จัดย่าม`, the panel's กลับ / ไปชั้นถัดไป / คืนสภาพ / ล็อกย่าม) remain for the same reason. The camp
starts with the panel **closed**; the figure does not walk while it is open.

*Was* a known overlap, fixed in 09e: the panel's bottom-left row (กลับ, ไปชั้นถัดไป) used to be
published *inside* the world status bar's rectangle at 960×600 and 1280×720. The row now sits in the
`Stats` strip above the bar and the two are disjoint at both sizes — measured, not argued
(`artifacts/ui/ui-report.md`, `UiLayoutCheckTests.A_shorter_altar_clears_the_status_bar…`); the
incense pot was moved for the same reason, so it no longer covers ออกเดินทาง. History:
[`../docs/stages/09b-report.md`](../../docs/stages/09b-report.md) §2(d) and
[`../docs/stages/09e-report.md`](../../docs/stages/09e-report.md) §1.

## 3a. The first-run checklist (13b)

A profile that has never finished it sees the camp's five steps in the panel's instruction block —
rent → place → rotate → read the tooltip → depart — as `[>]` on the step to do, `[x]` on the ones
done, `[ ]` on the rest, with the status bar naming the next one (`ขั้นที่ N/5: …`, 10
`camp.tutorial.*` rows). The fifth instruction label exists only for it.

- `CampTutorial` (`Assets/Main/Scripts/Presentation/Camp/CampTutorial.cs`) is the whole state: five
  booleans and a `bool completed`. It has no UnityEngine dependency and no command logic; it is
  testable in EditMode by itself.
- A step ticks only inside the branch where the **command succeeded** — `CampPresenter.Rent`,
  `Drop` (an item landing on the board, or a turned drop), `RotateSelected`, `Hover` of a piece,
  `Depart` — in any order, and repeating a step changes nothing.
- Nothing consults it: no rule, no gate, no price. A player who ignores it plays the same game.
- Finishing it sets `GameSettings.TutorialCompleted`; `CampSceneDirector` writes it to the settings
  file (`SettingsDto` key 4) before the scene changes, and `CampSceneScope` hands the flag to the
  presenter on the next visit, which is why the checklist never returns. `CampReport` publishes
  `tutorialActive` / `tutorialComplete` / `tutorialDone` / `tutorialStep`.
- The markers are ASCII because the Sarabun atlas has no `✓`/`▶`/`•`
  ([gotcha](../gotchas/symbol-glyphs-are-not-in-the-ui-font.md)).
- Report: [`docs/stages/13b-report.md`](../../docs/stages/13b-report.md).

## 3b. The tooltip, and what a mark on the board means since 09e

- **The tooltip is its own root canvas** (`TooltipSortingOrder = 100` against the camp's 0) with no
  `GraphicRaycaster` and `raycastTarget` off, so nothing about it can be clicked and nothing can be
drawn over it — the camp's own canvas order is its hierarchy order, and the board's cells and pieces
  are created at runtime *after* everything authored. The claim is proved in the capture: the same
  frame is rendered with the overlay off and on and five sampled pixels must land closer to the
  overlay's colour with it on (`artifacts/ui/camp-tooltip-*`). Placement is the pure
  `CampTooltipPlacement.Place` (flips at the edges, pinned inside the margin when the panel is larger
  than the canvas), and its five lines — ชื่อ / ระดับ / สถานะ / คำอธิบาย / คอมโบ — each hide when empty.
- **A mark is chosen from the rule's own tags since 09l**, never invented here: the rule whose two
  tags are `tome` and `weapon` (read from `data/tables/synergy.csv`, either order) is the only one
  drawn as a **line** — from the ตำรา to each อาวุธอาคม that touches it; the bag-area bonus is a
  corner **badge** on the receiving piece (a line from a bag would claim the bag is a partner on the
  board), and the other adjacency rules (ตะกรุด↔ยันต์, คุณไสย↔พระเครื่อง, น้ำแดง↔ภูติ) draw no
  permanent mark at all — they highlight on hover and during a drag. Pairs come from Core's unchanged
  `SynergyEvaluator.Active`; `CampPresenter.DrawnLinks` only decides the drawing. Choosing by `kind`
  stopped working when the tome rule became `AdjacentPair` in 09k-data (three other rules share the
  kind) — [ADR 0032](../../docs/adr/0032-a-touching-tome-draws-centre-to-centre.md).
- **A line runs from one piece's middle to the other's.** `CampLink` carries only the pair and the
  style; `CampBagScreenView.PlaceLine` reads the two drawn rectangles and draws between their
  centres, so a pair above/below draws as readily as a pair side by side, and a 2×2 tome's end sits
  on the cross of its four cells. The stroke therefore crosses the pieces it joins, which is why the
  names live in containers (`PieceLabels`, `ChestLabels`) raised above the line container on every
  render.
- **A bag is drawn as the cells it covers (09l).** `CampPieceView` keeps one pooled square per cell
  of a footprint with a hole (the L basket's four cells), and a box keeps its single rectangle; the
  bag's own name is measured against its top row, not its bounding box. The shape comes from
  `CampPieceModel.Shape` (already rotated by the presenter) — the view derives nothing.
- **A drag's origin is not the cell under the pointer.** `DragStart(piece, rotation, grabbedCell)`
  records the pressed cell inside the piece's own footprint, and every move (and every turn during
  the drag) recomputes the origin as pointer − grabbed cell, turned with `FootprintRotation`. Without
  this a 2×2 piece grabbed by its lower-right cell was previewed one cell up and left of where it
  landed (`../docs/stages/09k-report.md`).

## 4. Running the camp tests

```powershell
# rule engine + the data gates (the preview is asserted against the real tables)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Configuration Release

# the build the browser tests need (Editor closed first - the project lock is exclusive)
powershell -NoProfile -ExecutionPolicy Bypass -File artifacts/close-unity-editor.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Configuration Release -Force
node tools/serve-web.mjs --port 8080

# the three suites, on the real player
cd tests/web ; npx playwright test smoke.spec.ts battle.spec.ts camp.spec.ts
```

Unity-side: the EditMode fixtures are `CampPresenterTests` (rules and the seam, 32 tests) and
`CampTooltipPlacementTests` (4, the placement rule with no scene); the PlayMode fixtures are
`CampBagDragPlayModeTests` (16, real pointer events through the scene's own
`InputSystemUIInputModule`, plus the tooltip's draw order, the two-name rule, the grab offset and the
link kinds), `CampSynergyLinePlayModeTests` (10, where a line is and is not drawn) and
`CampWorldPlayModeTests` (9, walking, `E`, clicking a station).

Layout without a browser: `ProjectSurvive/Tools/1. Capture UI` renders Camp, Battle and the summary
screen (Result) at 1920×1080 and 1280×720 into `artifacts/ui/` and fails when two regions that must
stand apart share pixels —
the rectangles and the per-region alpha come from the scene it renders, so a layout regression costs
seconds rather than a WebGL build. Caveat: the labels in `Camp.unity` are empty until the runtime
fills them, so the shots show panels without text and the runtime layout is measured in PlayMode.

## 5. Traps found while building this screen

- **A fixture that loads the scene in `[UnitySetUp]` measures itself as broken.** `InputTestFixture.
  Setup()` rewires the input system after the coroutine has advanced frames, so a scene loaded there
  never receives another pointer event: 0 drag previews in `[UnitySetUp]`, 7 previews and 1 refused
  drop when the scene is loaded from the test body instead.
- **A drag origin taken at the threshold crossing is a cell off.** The EventSystem reports the
  crossing a few frames after the press (measured: press on (2,2), crossing on (3,2), no drag at all).
  Anchor the pick-up at `eventData.pressPosition`.
- **A published `RectTransform` position can be its corner.** `CampScreenRects` measures the four
  world corners and publishes the centre, because `Rect.Contains` excludes the top and right edges
  and a click aimed at a corner misses the control it was aimed at.
- **A click immediately after a canvas resize can be swallowed** (2 of 4 runs at ~300 ms after the
  resize; 0 of 5 when the test waits a second). Not explained; the resolution test waits.
- **A PlayMode click on a just-shown control can miss** if its canvas layout has not rebuilt: force
  the layout (`Canvas.ForceUpdateCanvases()`) before reading its rectangle.
