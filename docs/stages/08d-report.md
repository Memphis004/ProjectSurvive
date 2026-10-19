# Stage 08d — the mini ย่าม hangs from the caption (Presentation + scene only)

- **Stage:** 08d (a follow-up to Stage 08c's battle HUD; same brief family).
- **Date:** 2026-10-19
- **Decision record:** none. The brief fixes the target geometry (top-left, 8–12 px under the caption);
  the only choice left was *where* the origin lives, and that is documented on the code that owns it
  (`BattleMiniBagView.AnchorContent`, `MiniBagLayoutBuilder.Build`). No file under `lib/` and no
  `data/` row was touched.

---

## 1. Summary

The mini bag's grid was **centred inside a content area that was already in the right place**. The
content rect sat below the caption all along; `MiniBagLayoutBuilder` then computed
`(contentWidth − gridWidth) / 2` and `(contentHeight − gridHeight) / 2` and the view added those
offsets to every cell and box, so a 192 px grid floated in the middle of a 412 × 494 area. Stage 08d
removes the centring: the content area is anchored to the panel's top-left (same left inset as the
caption, one named 8 px gap under the caption's line box), the layout's origin is that corner, and the
grid's cell size is unchanged. Measured shift of the bag's first pixel: **110 px left and 149 px up at
1920 × 1080**, **73 px left and 100 px up at 1280 × 720**.

## 2. Diagnosis (measured, not guessed)

Rectangles the Editor capture command measured in the real scene before the change (Unity coordinates,
origin bottom-left; converted to a top-left reading in brackets):

| Region | 1920 × 1080 | 1280 × 720 |
| --- | --- | --- |
| `MiniBagPanel` | 24,510 · 432 × 546 → top-left (24, 24) | 16,340 · 288 × 364 → top-left (16, 16) |
| `BagCaption` (anchor/pivot 0/1, offset (10, −6), 240 × 30) | 34,1020 · 240 × 30 → (34, 30), bottom 60 | 22.667,680 · 160 × 20 → (22.667, 20), bottom 40 |
| `MiniBagContent` **before** (stretch 0→1, insets 10/10/10/42, pivot 0.5/0.5) | 34,520 · 412 × **494** → (34, 66) | 22.667,346.667 · 274.667 × **329.333** → (22.667, 28) |
| `MiniBagContent` **after** (top-left anchored, pivot 0/1, insets 10/10/10/**44**) | 34,520 · 412 × **492** → (34, **68**) | 22.667,346.667 · 274.667 × **328** → (22.667, **45.333**) |

The bag is 3 × 3 cells at 64 px (the fit's 64 px ceiling), i.e. 192 × 192 in reference pixels.

- **The origin.** `MiniBagLayoutBuilder.Build` ended with
  `offsetX = (contentWidth − columns·cell) / 2`, `offsetY = (contentHeight − rows·cell) / 2`; the view
  added them to `column·cell`, `−row·cell`. With the numbers above that is **110 / 151** at 1080p, which
  is exactly where the grid appeared (first drawn pixel at (144, 217) in the screenshot; 34 + 110,
  66 + 151).
- **Who is the cause.** The layout builder — not the view (it only applied the offsets) and not the
  scene's content rect (it was already below the caption at left inset 10). The content's old top inset
  of 42 also made the gap 6 px rather than the 8–12 the brief asks for, which is why the new top inset
  is 44 (`CaptionTop 6 + CaptionHeight 30 + CaptionGap 8`).
- **The panel's `Image` (asked explicitly).** In the **uncommitted working copy** `MiniBagPanel`'s
  `Image` had `m_Enabled: 0`; the committed scene and `ProjectSetup.AddPanelBackground` both create it
  **enabled**, and no code disables it. It was a hand edit, it is **not** the cause of the grid's
  position, and this stage did not toggle it deliberately — regenerating the scene (required by the
  brief) restored the builder's enabled background. The semantic `Battle.unity` diff for this stage is
  only the content rect — four property lines (anchored position, size, pivot, and the implied
  insets); the rest of the file diff is file-id and block ordering.

## 3. What changed

| File | What |
| --- | --- |
| `Presentation/Battle/Hud/BattleMiniBagLayout.cs` | the grid's origin is the content area's top-left: `OffsetX` / `OffsetY` (always 0 after this stage) are gone, so nothing can re-centre the bag |
| `Presentation/Battle/Hud/BattleMiniBagView.cs` | named constants `LeftInset` 10, `RightInset` 10, `BottomInset` 10, `CaptionTop` 6, `CaptionHeight` 30, `CaptionGap` 8, `ContentTop` 44; `AnchorContent` pins the grid's area to the panel's top-left; `TryGetPieceTopLeft` exposes the first box's origin to the tests |
| `Presentation/Battle/Hud/BattleHudView.cs` | `BagCaption` (the caption's line box) for the scene-level layout check |
| `Editor/BattleSceneBuilder.cs` | the caption and content are built from those constants and `AnchorContent`; caption width is a named constant |
| `Editor/UiLayoutCheck.cs` | `BagCaption` + `FindMiniBagStackViolations`: content inside the panel, **below** the caption, same left edge, no overlap |
| `Editor/UiCaptureTool.cs` | the battle capture reports the stacking rule and names the caption from the check |
| `Tests/EditMode/BattleMiniBagTests.cs` | +4: first box at the content's top-left at both sizes (cell size still 64), L-shaped `bag.wicker_basket_l` hugging its bounding box's corner, two bags anchoring to the union box's corner, the gap arithmetic and content size from the constants |
| `Tests/EditMode/UiLayoutCheckTests.cs` | +4: the stacking rule passes the real rects, reports a grid lifted over the caption (overlap + stack), reports a drifted left edge, and invents nothing for a screen with no mini bag |
| `Tests/PlayMode/BattlePlayModeTests.cs` | +1: in the real scene the grid's rectangle is below the caption and shares its left edge, the first box is at (0,0), `CreatedAfterBuild` and `viewsGrownDuringPlay` stay 0 over a second of ticks |
| `Assets/Main/Scenes/Battle.unity` | regenerated through `ProjectSurvive/Stage 08/3. Create Battle scene`; `BattleViewSettings.MiniBag` still wired |

Nothing reads new state: the bag is still read only from `BagSnapshot` / `CombatLoadoutSnapshot`, the
plates from `IBattleFrame.Cooldowns` copied in the same frame, and no object is created during play.

## 4. Evidence

- Before: `artifacts/ui/stage08d-before-battle-{begin,midfight,manashort}-{1920x1080,1280x720}.png`
  (plus the report copy `artifacts/stage08d/ui-report-before.md`).
- After: `artifacts/ui/battle-{begin,midfight,manashort}-{1920x1080,1280x720}.png`.
- Close-ups: `artifacts/ui/stage08d-minibag-{before,after}-{1920x1080,1280x720}.png`.
- Pixel measurement of the grid's bounding box (exact palette colours, both sizes, all three states):

| Size | Before (first pixel) | After (first pixel) | Shift |
| --- | --- | --- | --- |
| 1920 × 1080 | (144, 217) | (34, 68) | **110 left, 149 up** |
| 1280 × 720 | (96, 145) | (23, 45) | **73 left, 100 up** |

At 1080p the bag's top is 8 px below the caption's bottom (caption 30–60, bag starts 68) and its left
edge is 34, the caption's own. At 720p the same 8 reference px are 5.3 screen px (caption 20–40, bag
starts 45.3); the canvas scalar draws the same layout, it does not change it. The mid-fight and
MP-short shots keep the same rectangle and show the plates at the new position (one cell at
(90,44,37) dark plate in mid-fight, the same cell at (149,111,157) blue in the MP-short shot).

## 5. Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Compile | **0 errors** | `unity-cli recompile` → completed, `compilationFailed: false` |
| `BattleMiniBagTests` | **18/18** | per-class run (14 previously, +4) |
| `UiLayoutCheckTests` | **24/24** | per-class run (20 previously, +4) |
| `BattlePlayModeTests` (real scene) | **6/6** | per-class PlayMode run, 11.2 s |
| `tools/test.ps1 -Suite Fast` | **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0**, EditMode **155/155**, dotnet **497** (86.6 s) | `artifacts/stage08d/fast.log` |
| UI capture, both sizes | **PASS**, 0 failures; every battle state reports **0 overlaps** and **0 stacking violations** | `artifacts/ui/ui-report.md` |
| `BattleViewSettings.MiniBag` wired | `MiniBag: {fileID: 1709823314}` in the regenerated scene | grep of `Battle.unity` |

## 6. Not tested, and risks

- **WebGL build and Playwright: NOT RUN**, per the brief (no Web build for a layout stage). The browser
  half of the battle screen is unchanged and its gate is Stage 14/15.
- **The bulk PlayMode assembly run** is not quoted as a pass: it is the documented ambient-log flake in
  `docs/testing/test-suites.md` §7. The battle class is 6/6 on its own; a Full run would still show the
  flake.
- **One intermediate Fast run failed on an unrelated Result-screen assertion**
  (`ResultReportTests.A_lost_run_publishes_the_defeat_headline_its_numbers_and_both_controls`: "restart is
  off the canvas to the right", published `canvasWidth` 597 while the Editor's Game View was not at its
  usual 1920 width). It is a canvas-size-dependent test this stage does not touch; it passed in
  isolation immediately after and the final Fast run is green. Recorded rather than explained away —
  the Result report's width comes from the Game View, so the case is environment-sensitive.
- **1280 × 720 is the same UI layout scaled**, not a separately authored one (the canvas is
  scale-with-screen-size at 1920 × 1080). The test feeds both the 1080p reference rect and the 720p
  measured pixel rect, and the screenshot measures the scaled result, but there is no second layout to
  review.
- **The caption's `Image` question is reported, not resolved as intent**: if the disabled panel
  background was deliberate, the place to express that is `BattleSceneBuilder` (omit or disable the
  background), not a hand edit in the scene — a hand edit does not survive regeneration.

## 7. Commands run

```powershell
unity-cli recompile
unity-cli menu "ProjectSurvive/Tools/1. Capture UI (Title + Camp + Battle + Result, 1920x1080 + 1280x720)"   # before + after
unity-cli menu "ProjectSurvive/Stage 08/3. Create Battle scene"
unity-cli run_tests --mode editor --filter BattleMiniBagTests --filter_type testName
unity-cli run_tests --mode editor --filter UiLayoutCheckTests --filter_type testName
unity-cli run_tests --mode playmode --filter BattlePlayModeTests --filter_type testName --async_tests
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
grep -n "MiniBag" UnityProject/Assets/Main/Scenes/Battle.unity
```

## 8. Acceptance

| # | Acceptance | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Diagnosis written before the fix: real rects, the origin of the centring, the Image state | **PASS** | §2 |
| 2 | `MiniBagContent` anchored to the panel's top-left under the caption; layout hugs the top-left; cell size unchanged; panel still ≤ ¼ width | **PASS** | §2–3; `BattleMiniBagTests` (17); `battle-*-1920x1080.png` |
| 3 | Gap and left inset are named constants, anchor-based (not canvas pixels), correct at 1920 × 1080 and 1280 × 720 | **PASS** | `BattleMiniBagView.LeftInset` / `CaptionGap` / `ContentTop` / `AnchorContent`; both sizes in §2 |
| 4 | Non-rectangular (L) and multi-bag cases hug the bounding box's top-left | **PASS** | `An_l_shaped_bag_hugs_the_top_left_of_its_own_bounding_box` (2 × 3 box, missing top-right cell) and `Two_bags_anchor_to_the_top_left_of_their_shared_bounding_box` (5 × 5 union box) |
| 5 | Read-only bag/cooldowns unchanged; no `Instantiate` during play (`CreatedAfterBuild = 0`) | **PASS** | the bag/plate tests are untouched; PlayMode asserts `CreatedAfterBuild` and `viewsGrownDuringPlay` 0 |
| 6 | Scene regenerated through the Stage 08 menu (no hand YAML); `MiniBag` still wired | **PASS** | `BattleSceneBuilder.cs`; `MiniBag: {fileID: 1709823314}`; the scene diff is only the content rect |
| 7 | EditMode: first box top-left at both sizes incl. L; gap equals the constant; cell size unchanged | **PASS** | 18/18 |
| 8 | PlayMode: built in the real scene, grid rect not over `BagCaption`, counters 0 | **PASS** | `The_mini_bag_starts_under_its_caption_and_never_grows`; 6/6 |
| 9 | `UiLayoutCheck`: existing suite green + content below caption / no overlap | **PASS** | 24/24, `FindMiniBagStackViolations` |
| 10 | Evidence at `artifacts/ui/` for both sizes, start and mid-fight with plates, before/after and the px shift | **PASS** | §4 |
| 11 | `tools/test.ps1 -Suite Fast` and UI capture run | **PASS** | §5 |

**Stage verdict: PASS**, with the two limitations in §6 recorded rather than hidden: no browser run (by
the brief) and the bulk PlayMode flake, which is not ours and not quoted as green.
