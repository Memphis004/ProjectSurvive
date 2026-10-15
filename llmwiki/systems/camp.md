# System — ศาลาพัก (the camp screen)

Stage 09 (+09b, +09c). The first screen the player can *change* the run from: rent a shelf, arrange
the bag, rotate, merge, give a piece back, restore, and leave for the floor.

- Scene: `Assets/Main/Scenes/Camp.unity`, build index 5, reachable as `?scene=Camp`
  (`ProjectSetup` *Stage 09/5* authors it; the boot allow-list is `UrlSceneAllowList`).
- Composition: `CampSceneScope` (VContainer) → `CampSceneDirector` (start + the two documents) →
  `CampPresenter` (every command) → the views (`CampWorldView`, `CampPlayerView`,
  `CampBagScreenView`, `CampStatusBarView`, `CampTooltipView`).
- Rules live in `ProjectSurvive.Core.Application.Run.RunSession`; no view computes a price, a combo
  or a drop verdict ([ADR 0019](../../docs/adr/0019-camp-screen-read-model-and-gesture-preview.md)).
- Stage report: [`docs/stages/09-report.md`](../../docs/stages/09-report.md) (+ `09b`, `09c`).

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
behind it; its regions are authored from the canvas' top-left at the reference resolution.

## 3. The two affordances, and why both exist

From 09c the shop is **four shelves in the sala** (one per slot, each with its own price, rented state
and lock pin). Walking up and pressing `E` and clicking a station call the *same* presenter method —
the click is the phone/test path and the keyboard path, not a second rule. The additive UI buttons
(`จัดย่าม`, the panel's กลับ / ไปชั้นถัดไป / คืนสภาพ / ล็อกย่าม) remain for the same reason. The camp
starts with the panel **closed**; the figure does not walk while it is open.

Known overlap, measured and accepted for now: the panel's bottom-left row (กลับ, ไปชั้นถัดไป) is
published *inside* the world status bar's rectangle at 960×600 and 1280×720. The panel is the later
sibling and its own graphics win the raycast, so the button works — but the strict reading of 09b's
"no elements overlap" is not met between the panel and the world bar. Hiding the bar while the panel
is open is the one-line change that would make it true. See
[`../docs/stages/09b-report.md`](../../docs/stages/09b-report.md) §2(d).

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

Unity-side: the EditMode fixture is `CampPresenterTests` (rules and the seam, 9 tests); the PlayMode
fixtures are `CampBagDragPlayModeTests` (8, real pointer events through the scene's own
`InputSystemUIInputModule`) and `CampWorldPlayModeTests` (8, walking, `E`, clicking a station).

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
