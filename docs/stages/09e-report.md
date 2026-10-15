# Stage 09e — SceneCamp UI: layout, tooltip on top, synergy lines

**Stage number.** The task arrived as "Stage 09d — แก้ SceneCamp UI 3 เรื่อง". `09d` in this repo is
already spent on the Thai-text-sharpness pass ([`09d-report.md`](09d-report.md), commit `b7222e4`),
and the code comments written for this work already say "Stage 09e work item 1"
(`ProjectSetup.cs:1084`, `CampBagScreenView.cs:314`), so the report is filed as **09e** rather than
overwriting a landed report (AGENTS rule 1). Renaming is one `git mv` if the plan says otherwise.

Three UI matters, all in the scene / Presentation layer: no Core, `data/` or synergy rule was touched
(`git diff --stat` shows no file under `lib/`, `data/` or `proto/`). No WebGL build was made.

## 1. Layout inside the canvas at both sizes (item 1)

Every panel is anchored to a corner or edge of the canvas (`CanvasScaler` 1920×1080, match 0.5) — no
element is placed by an absolute screen pixel — and the numbers were re-measured rather than guessed:
`artifacts/ui/ui-report.md` now prints the rectangle **and the opacity** of every region it measures.

| Panel | 1920×1080 | 1280×720 | alpha |
| --- | --- | --- | --- |
| Board | `40,360 740×580` | `26.667,240 493.333×386.667` | **0.92** |
| ChestPanel | `1104,480 776×400` | `736,320 517.333×266.667` | **0.92** |
| Chest (grid) | `1120,528 744×300` | `746.667,352 496×200` | 1 |
| Altar | `822,214 210×106` | `548,142.667 140×70.667` | 0.92 |
| Instructions | `1104,884 776×176` | `736,589.333 517.333×117.333` | 0.85 |
| Stats (panel foot) | `40,232 740×108` | `26.667,154.667 493.333×72` | 1 |
| StatusBar | `24,16 1400×190` | `16,10.667 933.333×126.667` | 0.88 |
| Tooltip | `8,8 440×330` | `5.333,5.333 293.333×220` | **0.95** |

- **No region leaves the canvas and no two overlap** at either size: 0 violations in 8 shots, and the
  1280×720 board still lands on the rectangle the browser published in Stage 09
  (`{x:26.667, y:213.333, 493.333×386.667}`) — the Editor shot and the player agree.
- **กลับ / ไปชั้นถัดไป do not sit on the run's numbers, and no number is printed twice.** The panel's
  foot is `Stats` (y 232..340 above a bar that ends at 206) and it carries only the controls the bar
  does not; เบี้ย · น้ำหนัก · สมาธิ · HP/MP are drawn **once**, on the status bar
  (`CampBagScreenView.cs:314`). The checked fact is geometric: `Stats` is disjoint from `StatusBar` at
  both sizes, and the bar's own labels are the only ones holding those values.
- **The altar clears ออกเดินทาง.** The pot sits in the column the bar does not reach (x 822..1032,
  between the panel's foot at 780 and the bar's depart button at 1050), so the two are disjoint at both
  sizes — the `Altar`/`StatusBar` pair the old layout failed on.
- **Board and chest are opaque enough** (0.92 ≥ the 0.9 floor) and the rule is now enforced, not
  commented: `UiLayoutCheck.FindOpacityViolations` names `Board` and `ChestPanel` as the two regions
  that must reach 0.9, and the capture command reports the alpha of every region it measures. The
  instruction scroll (0.85), the altar (0.92) and the bar (0.88) are deliberately left translucent —
  the จัดย่าม screen is an overlay on ศาลาพัก.
- **A name in a cell never lands on another name.** An item's name is the primary text (centred over
  its own rectangle) and a bag's name is secondary (a strip along its top edge) which the view
  **hides** when an item's rectangle reaches that strip (`SetLabel(visible, secondary)`,
  `LayoutPieceLabels`). Measured in play mode on the starting kit: the bag ย่ามผ้าดิบ has an item
  (ยันต์เพลิงกาลี) on it, the bag's label is hidden and the item's is drawn —
  `CampBagDragPlayModeTests.An_items_name_keeps_its_place_over_the_bag_under_it_and_no_two_names_overlap`
  asserts no two visible name rectangles intersect, before and after the item is dragged off the bag.

## 2. The tooltip is always on top (item 2)

- **Its own canvas, sorting order 100**, above the camp's canvas (0) — documented in the installed
  engine as *"Canvas' order within a sorting layer"* (`Canvas.sortingOrder`, UnityEngine.UIModule.xml,
  Unity 6000.3.9f1). A second root canvas rather than a panel of the camp's, because the camp's draw
  order is its hierarchy order and the board's cells, the piece rectangles and the combo lines are all
  created at runtime *after* everything authored.
- **Nothing about it can be clicked**: the canvas carries no `GraphicRaycaster` and the panel's
  background has `raycastTarget` off. Its background is alpha **0.95** (≥ the 0.92 the stage asks for).
- **It follows the pointer and never leaves the screen**: `CampTooltipPlacement.Place` (pure function,
  `CursorOffset` 18, `Margin` 8) flips left at the right edge and down at the top, and pins a panel
  larger than the canvas to the margin. New EditMode tests sweep 0/25/50/75/100 % of both axes at both
  sizes (25 placements per size) and require the whole panel to stay inside, plus the flip and the
  pointer-not-covered cases.
- **Five lines, one label each**: ชื่อ, ระดับ, สถานะ, คำอธิบาย, คอมโบที่ทำงานอยู่ — and a line with
  nothing to say is hidden rather than drawn empty (a bag has no คำอธิบาย). The PlayMode test asserts
  each of the five labels exists exactly once, is a different object from the others, that
  `activeSelf == !string.IsNullOrEmpty(text)`, that the first line is the hovered piece's own name and
  that ระดับ/สถานะ are non-empty for an item.

**How "on top" is proved in the picture, not asserted.** `UiCaptureTool` gained a third state
(`tooltip`) and a pixel proof: the same shot is rendered twice, with the overlay switched off and on,
and the five sampled points inside the overlay's own rectangle must land
**closer to the overlay's background colour with it on** — alpha compositing can only pull a pixel
toward the colour painted over it. Measured, both sizes: the centre and three quadrants move
**0.016**, the bottom-left quadrant **0.188** (it sits over a button). The first version of this check
used a plain "the pixel changed by ≥ 0.05" threshold and **failed** — the tooltip is a dark panel over
a dark panel and the real change is 0.024 — which is why the check is a direction, not a magnitude.
A tooltip painted under the board would move nothing and fail.

## 3. Synergy lines (item 3)

- **A line means one thing only**: the same-line rule from a ตำรา to each อาวุธอาคม it empowers. The
  pairs are Core's (`SynergyEvaluator.Active` — `ActiveSynergy` carries the rule id and both instance
  ids) and `CampPresenter.DrawnLinks` only decides the *drawing*, from the rule's own `synergy.kind`
  (`SameRow`/`SameColumn` → `CampLinkStyle.Line`, `BagArea` → `Badge`, `AdjacentPair` → nothing
  permanent). No evaluator, rule or `data/` row was changed.
- **Never a line from a bag to an item**: the area rule marks the *receiving* piece with a small badge
  on its corner (`PlaceBadge`), because a line from a bag would say the bag is a partner on the board.
- **The other three rules are hover-only**: ตะกรุด↔ยันต์, คุณไสย↔พระเครื่อง and น้ำแดง↔ภูติ draw no
  permanent mark; they are shown by the existing highlight (the piece plus everything the run says it
  is in a working relation with) while the pointer is on one of them, and during a drag.
- **Lines do not run through text**: the marks are drawn under the pieces and under their names.
- **A pair that stops qualifying loses its mark on the same render**: the view hides every mark the
  model no longer lists. `CampPresenterTests.Only_the_tome_to_weapon_rule_is_drawn_as_a_line_and_the_bag_bonus_is_not`
  walks a real run to the camp that offers the tome (seed searched from the tables, no item id written
  into the test), places it on the weapon's row, checks a line appears and reaches the weapon, then
  moves it off that row and checks the line is gone.

## 4. What was run, and what it gave

| Command | Result |
| --- | --- |
| `ProjectSurvive/Stage 09/5. Create Camp scene` (ProjectSetup, in the Editor) | Camp.unity regenerated: `_runSeed: 20261015`, TooltipPanel on its own canvas (order 100, no raycaster), Board/ChestPanel 0.92 |
| `ProjectSurvive/Tools/1. Capture UI (Camp + Battle, 1920×1080 + 1280×720)` | **PASS**, 0 failures, 2110 ms, **8 images** → `artifacts/ui/` + `ui-report.md` / `ui-report.json` |
| Unity Test Runner, **EditMode** | **75 / 75 passed** (4.01 s); was 65 tests, +10 this stage |
| Unity Test Runner, **PlayMode**, `--filter CampBagDragPlayModeTests` | **11 / 11 passed** — includes the 3 new tests |
| Unity Test Runner, **PlayMode**, whole suite | **12 passed / 15 failed**, every failure an ambient log-assert in the Input System package (see §5) |

New tests: `CampTooltipPlacementTests` (4), `UiLayoutCheckTests` +4 (overlay never excused, overlay
does not excuse a real collision, the opacity floor and its exemptions), `CampPresenterTests` +2, and
`CampBagDragPlayModeTests` +3 (tooltip above the board with real pointer events, the two-name rule, the
line/badge counters against the run's own list).

## 5. What is not tested, and why

- **The three evidence states at both sizes are partly NOT RUN.** `artifacts/ui/` holds, at 1920×1080
  *and* 1280×720: the camp normal (`camp-*`), the camp closed (`camp-closed-*`) and the tooltip
  (`camp-tooltip-*`) — all from the Editor capture command, which renders the **authored** scene.
  The two states that need live pieces — *tooltip open over an item that sits under a bag* and
  *ตำรากับอาวุธวางเข้าเงื่อนไข, lines drawn* — are **NOT RUN as images**: in the Editor the board's
  pieces do not exist until the runtime builds them (`CampBagDragPlayModeTests` measures that layout in
  play mode, not the capture command), and the instruction for this stage forbids a WebGL build.
  Driving a live play-mode capture from outside the game was tried and abandoned: injected pointer
  events reached the scene as real clicks and drags (the run rented two pieces and recycled one), the
  tooltip never appeared from an injected hover, and the image landed inside `UnityProject/Assets/`
  (the empty folder that attempt left there was removed; nothing stray is in the asset tree).
  **The claims those two images would show are not untested** — they are asserted in PlayMode (tooltip
  over the board and inside the screen, names disjoint, line/badge counts) and measured in the Editor
  capture (rectangles, opacity, the pixel proof). What is missing is the picture.
- **No WebGL build or browser run**: by instruction (the stage says not to build Web while working).
- **The bulk PlayMode suite still fails as a suite** — 12/15 on 2026-10-16, with **no failed
  assertion**: 11 `IndexOutOfRangeException` inside `InputSystem.InputManagerStateMonitors` via
  `InputSystemUIInputModule.OnEnable` and 4 `[Error] Cached unprocessed value unexpectedly became
  outdated`, all ambient-message log asserts in the Input System package. The same Camp fixture passes
  **11/11** on its own with different per-test durations, and the failing tests include ones this stage
  never touched. It is the pre-existing, unsolved item in `llmwiki/stage-log.md` and
  [`../testing/test-suites.md`](../testing/test-suites.md) §7 — reported, not tidied.
- **Unrelated files left alone** (AGENTS rule 1): untracked `BattleUI.cs`, `你好东西.cs`,
  `背包启动.cs` and `UnityProject/Assets/_Recovery/` were already in the working tree and are untouched.

## 6. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| 1 | Everything inside the canvas at 1920×1080 and 1280×720, measured from shots taken at those sizes | **PASS** — §1, 8 shots, 0 violations |
| 1a | กลับ / ไปชั้นถัดไป do not cover the run's status text, and no status text is duplicated | **PASS** — §1 (geometric: `Stats` disjoint from `StatusBar`; the bar is the only place the numbers are drawn) |
| 1b | The altar does not cover ออกเดินทาง | **PASS** — §1 (disjoint columns at both sizes) |
| 1c | Board and chest opaque enough (alpha ≥ 0.9) | **PASS** — 0.92 + `FindOpacityViolations` tested 4 ways |
| 1d | A bag's name and an item's name never overlap | **PASS** — §1 (hide-when-covered rule + a PlayMode test over real pieces) |
| 2 | Tooltip on a canvas above the board, chest, altar and HUD, from the installed engine's own docs | **PASS** — §2 (sorting order 100 vs 0, no raycaster, alpha 0.95, pixel proof in the shot) |
| 2a | Raycast off, follows the mouse, always on screen with edge flipping, opaque background | **PASS** — §2 (4 EditMode tests, 25 placements per canvas size; PlayMode corners) |
| 2b | Content as separate lines: name, tier, status, description, active combos | **PASS** — §2 (five labels, hidden when empty, PlayMode) |
| 3 | Lines only for a real อาวุธอาคม↔ตำรา pair, drawn from the tome to each weapon it reaches | **PASS** — §3 (real run walked to the tome's camp; `ActiveSynergy` used unchanged) |
| 3b | No line from a bag to an item — a border/badge on the cell instead | **PASS** — §3 (`CampLinkStyle.Badge`; PlayMode asserts a badge's source is always a bag) |
| 3c | Other rules highlight only on hover/drag | **PASS** — §3 (no permanent mark for `AdjacentPair`) |
| 3d | Lines never over text, and gone the moment the pair stops qualifying | **PASS** — §3 (marks drawn under the pieces; the same-row move-away case) |
| 4 | Evidence images at both sizes, three states | **PARTIAL** — 5 of 6 states delivered (normal, closed, tooltip × 2 sizes); the two runtime states are **NOT RUN as images**, with the reason and the covering tests in §5 |

## 7. Risks

- The **runtime states have no picture** (§5). If the pictures are required, the honest route is a
  PlayMode test that renders the two sizes into a `RenderTexture` and writes the PNGs — the technique
  `UiCaptureTool` already uses — because that is the only place the scene has pieces without a WebGL
  build.
- The Editor capture judges the **authored** layout: a label that is empty in the scene can still be too
  narrow once Thai text arrives (this stage's `Stats`/label geometry is checked in play mode for
  exactly that reason, and Stage 09d measured the 22 px floor at 720p as ×0.667).
- The tooltip's rectangle is measured as an overlay, i.e. the overlap rule never applies to it by
  design; a tooltip that covered the panel's own controls *while a drag is in flight* is not checked
  (the placement follows the pointer and no rule forbids it).
- `run_tests` can answer with a previous run's payload (documented in `docs/testing/test-suites.md`
  §7): the counts above were read from the per-test payload of a run whose durations differ from the
  previous one, not from the summary alone.
