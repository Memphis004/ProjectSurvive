# Stage 08i — the mini ย่าม and the cooldown plates come back, behind F12

## Summary

Stage 08g-2 met its 15 % HUD budget by deleting the mini ย่าม, and with it the only surface that showed
`IBattleFrame.Cooldowns`; the player could no longer see which item was ready and which was still
recharging. This stage restores the bag as a fifth panel at the bottom-right corner (288 × 216 at
1080p, its panel more transparent than the other four), leaves the four panels 08g cut exactly where
they were, and moves the ceiling to a named **18 %** — measured **16.95 %** at 1920 × 1080 and at
1280 × 720, five disjoint rectangles. The dev menu's key moves from F1 to **F12**, still starting shut,
with the key a single constant in `DevMenuView`.

## A. Diagnosis (from the repository, not from memory)

**What 08g (`1747be2`, parent `0382aa0`) did to the bag.** Deleted
`BattleMiniBagLayout.cs`, `BattleMiniBagView.cs` and `BattleMiniBagTests.cs` (with their `.meta`s —
`BattleMiniBagLayout.cs` −514, `BattleMiniBagView.cs` −622, `BattleMiniBagTests.cs` −650 lines); and
edited the places that referenced them: `BattleViewSettings` (the `MiniBag` field −3), `BattleViewCaps`
(the cell/piece pool sizes −17), `BattleViewScene` (the `MiniBag` property −22), `BattleSimulationRunner`
(one line: the per-frame `ApplyCooldowns` call), `BattleHudView`/`BattleHudState`/`BattleHudPresenter`
(the caption and the wiring), `BattleSceneBuilder` (−202 net, the panel and its builder method),
`UiCaptureTool` (−88, the region and the "bag" shot), `UiLayoutCheck` (−110, `MiniBag*` regions and the
stacking rule), `UiLayoutCheckTests` (−65), `BattlePlayModeTests` (−215, three cases), and the docs
(`08g-report.md`, `test-suites.md`, `knowledge.md`, `llmwiki/systems/battle-presentation.md`,
`llmwiki/index.md`, `stage-log.md`).

**What the bag was (`0382aa0`), and what is therefore kept.** `BattleMiniBagView.Build(Backpack,
CombatLoadoutSnapshot, IGameConfig)` read the run's own board and its resolved loadout — never the
frame — and `ApplyCooldowns(ReadOnlySpan<LoadoutCooldown>)` was handed the list the runner copied out of
`IBattleFrame.Cooldowns`, keyed by board instance id. Readiness was drawn three ways, all from that one
list: a plate only on a piece that **can cast** (`Loadout`-resolved, so plate and cooldown entry cannot
disagree), its tone `None` when ready, `Counting` (dark, covering the remaining `RemainingTicks /
PeriodTicks`) while recharging, and `ManaShort` (faint blue) when ready but the pool cannot pay. Pieces
and cells were created once at floor start from the board's own maximum (9 × 7), and the grid hugged
the content area's top-left under the "ย่าม" caption (Stage 08d).

**The dev menu before this stage.** `DevMenuView` (`ToggleKey = Key.F1`), `_toggledOpen = false`, so
`Start → Bind` left the panel shut; `Update` read `Keyboard.current[ToggleKey].wasPressedThisFrame`.
F1 was named in: `ResultScenePlayModeTests` (which had to open the panel before the report exists — it
calls `TogglePanel`, the same path the key takes), `tests/web/result.spec.ts` (pressed F1),
`docs/testing/test-suites.md`, `knowledge.md`, `llmwiki/systems/battle-presentation.md`, the stage plan
document, and the "dev: on (Development build or Editor)" status string in `DevMenuPresenter`.

## B. What changed

- **Files restored from `0382aa0`** (`git checkout 0382aa0 -- …`, `.meta`s included so the GUIDs
  survive): `BattleMiniBagLayout.cs`, `BattleMiniBagView.cs`, `BattleMiniBagTests.cs`. Then adapted:
  `PanelWidth = 288f` / `PanelHeight = 216f` as named ceilings, `MiniBagPalette.Backdrop` at **0.72**
  alpha against the other panels' 0.92, `BattleHudArea.Budget = 0.18f`, and the scene/HUD wiring.
- **Scene** (`BattleSceneBuilder`): the bag is anchored to the screen's right and bottom edges with the
  same `UiMargin = 24` the status panel uses (`1608 → 1896` × `24 → 240` at 1080p); the dev menu keeps
  its 440 × 440 and now starts 16 px above the bag (`MiniBagGap`), so pressing F12 can never cover it.
- **HUD ledger** (`BattleHudArea`): 0.15 → **0.18**, with the reasoning in
  [ADR 0040](../adr/0040-the-hud-budget-moves-to-eighteen-percent.md). The four old panels are
  unchanged: floor 380 × 72, resources 513.6 × 216, status 380 × 212, timeline 1468 × 48 (13.95 %).
- **Dev menu**: `ToggleKey = Key.F12`, one constant; F1 is not read anywhere. The status string now ends
  "— F12". Everything that named F1 was updated, including the Playwright spec (renamed key only;
  **NOT RUN**), `test-suites.md` and the wiki.
- **Capture tool**: the Battle states now photograph the bag (`hud-begin`, `mid-fight`, `lbags`), and
  `UiLayoutCheck` carries the `MiniBagPanel` / `MiniBagContent` / `BagCaption` regions and the stacking
  rule again.

### Numbers

| thing | value | where |
| --- | --- | --- |
| mini ย่าม panel | 288 × 216 = **3.00 %** of 1920 × 1080 | `ui-report.json` region `MiniBagPanel` |
| five HUD panels | **16.95 %** at 1920 × 1080 **and** at 1280 × 720 | `ui-report.json`; `BattleHudArea.Fraction` in the PlayMode test |
| largest bag in `data/tables/bag.csv` | 3 × 3 (`bag.raw_cotton_bag`), L-shape `bag.wicker_basket_l` is 2 × 3 | the table |
| cell size for that bag | **54 px** (content 268 × 162 at 1080p; floor 16 px, cap 64 px) | `MiniBagLayoutBuilder.Fit` |
| dev panel | 440 × 440, bottom edge 256 px up, bag top edge 240 px | capture report |
| overlap violations | **0** at both sizes, bag vs the four panels and vs the open dev panel | `ui-report.json` |

**The scene rebuild bit once, and the tests caught it.** Re-running
`ProjectSurvive/Stage 08/3. Create Battle scene` (which starts from `NewScene`) leaves the Battle scene
without the arena, which is added by a separate menu item; four `ArenaEnvironmentTests` cases failed
("the arena has no screen band at all"). The shipped scene is the pair **08/3 → 08e/1**, and after
running both, EditMode went 312/312 and the capture showed the arena again. Recorded here because the
`.unity` diff of a rebuilt scene is otherwise unreadable.

## Acceptance

| # | item | verdict | evidence |
| --- | --- | --- | --- |
| 1 | mini ย่าม back at the bottom-right, caption top-left, grid under it | **PASS** | `artifacts/ui/battle-begin-1920x1080.png`, `battle-lbag-1920x1080.png`, `battle-begin-1280x720.png` |
| 2 | cooldown plates follow `IBattleFrame.Cooldowns` | **PASS** | `artifacts/ui/battle-midfight-1920x1080-bagcrop.png` (plate at 1/3 of its period); `BattlePlayModeTests.The_mini_bags_plates_are_the_frames_cooldowns` |
| 2b | the cloth reads against its panel, its empty cells and the item boxes (second pass) | **PASS** | 1.38 → **1.80 : 1** measured on the capture, 2.14 / 2.05 / 1.64 in `The_bags_cloth_reads_against_its_panel_and_its_empty_cells` |
| 2c | an item is told apart by its category's mark, and its tier comes back as small pips in the box's corner (third and fourth passes) | **PASS** | `artifacts/ui/glyphs-sheet.png` (the six masks), `battle-begin-1920x1080-bagcrop.png` and `battle-begin-1280x720-bagcrop.png` (a dagger with its tier-1 dot at both sizes); `An_items_box_shows_its_categorys_mark_and_a_corner_tier`, `The_tier_pips_grow_one_dot_per_level_and_stop_at_the_runs_ceiling` and `Every_category_has_its_own_mark_and_none_of_them_is_blank` |
| 3 | built once; nothing created during play | **PASS** | `…Ten_seconds_of_ticks_raise_no_exception_and_bind_no_new_view` (`CreatedAfterBuild == 0`, `Pool.CreatedAfterWarmup == 0`, `PlateUpdates > 0`) |
| 4 | HUD ≤ 18 % at both sizes, panels disjoint | **PASS** | 16.95 % / 16.95 %; `…The_hud_covers_at_most_eighteen_percent_of_the_screen_at_both_sizes` |
| 5 | bag inside the screen, never over another panel or the open dev panel | **PASS** | `ui-report.json` `violations: []`, `UiLayoutCheckTests` stacking cases 23/23 |
| 6 | dev menu starts hidden, F12 shows, F12 again hides, F1 does nothing | **PASS** | `artifacts/ui/battle-devmenu-f12-1280x720.png`, `battle-devmenu-f12-closed-1280x720.png`; `…The_dev_menu_starts_hidden_and_toggles_on_F12_not_F1`; `ResultScenePlayModeTests` 3/3 |
| 7 | no exception in the Console during a floor | **PASS** | 12/12 PlayMode; `console get` clean of test-time errors after the runs |
| 8 | WebGL build + Playwright | **NOT RUN** | ordered NOT RUN by the stage brief; the browser reserves F12 (see risks) |
| 9 | PlayMode screenshot of F12 at 1920 × 1080 | **NOT RUN** | the Editor's Game View renders at 1280 × 720 on this machine; the 1080p proof is the capture tool's photograph (dev panel visible, `battle-begin-1920x1080.png`) plus the PlayMode test |

## The second pass on legibility (same stage, after the screenshots were reviewed)

The first cut of the restored bag was the 08c palette unchanged, and the photograph showed why that
was not good enough: the cloth (0.30, 0.28, 0.22 at 0.95) sat on a panel (0.07, 0.08, 0.11 at 0.72)
over a lit arena, and the measured WCAG ratio of the <b>composited pixels</b> was **1.38 : 1** cloth
against panel and **1.22 : 1** cloth against the floor — the panel under the bag was easier to see than
the bag. Three colours moved, each for a measured reason:

| colour | before | after | why |
| --- | --- | --- | --- |
| `MiniBagPalette.Backdrop` | (0.07, 0.08, 0.11, 0.72) | **(0.04, 0.05, 0.07, 0.72)** | at 0.72 alpha the panel contributes a third of the composite; a darker one is what the cloth is read against |
| `MiniBagPalette.Bag` | (0.30, 0.28, 0.22, 0.95) | **(0.38, 0.32, 0.22, 1.00)** | brightest surface in the panel, so the footprint is the first thing the eye finds |
| `MiniBagPalette.EmptyCell` | (0.15, 0.15, 0.18, 0.85) | **(0.13, 0.14, 0.17, 0.92)** | darker than the cloth, lighter than the panel: an L basket's bite reads as *a cell that is not cloth*, not as a hole |

Measured on the re-run capture (`artifacts/ui/battle-begin-1920x1080.png`): cloth **1.80 : 1** against
its panel (was 1.38), and in the model the test uses — palette composited over the arena's dusk and
night tones — cloth 2.14 / 2.05, cloth against empty cell 2.05, the weakest item box (the offering's
gold) 1.64 against the cloth, a cooldown plate 3.7 against the item under it. Lighter cloth was tried
and rejected: at 0.52 the offering's box falls to 1.02 : 1 against the cloth, i.e. one of the five
categories disappears. `BattleMiniBagTests.The_bags_cloth_reads_against_its_panel_and_its_empty_cells`
holds the three numbers, so a later tweak cannot quietly undo them.

The scene's own panel image carries the new backdrop (`m_Color` on `/UI/MiniBagPanel`, set through the
Editor API and saved), so what the capture photographs and what the runtime paints are the same value.

### The category marks replace the tier numbers (third pass)

The box's colour already says which kind of item a piece is, but the five category colours are mid-tones:
a colour-blind player, a night floor or a 1280 × 720 screenshot softens them into each other. So an
**item's box now draws its category's mark instead of its tier number**: a dagger (arcane weapon), a
diamond ring (charm — a folded ยันต์), an open book (spell tome), a chalice (offering) and an ember
(familiar), plus an empty frame for an item the tables do not name. The marks are 32 × 32 masks drawn in
code (`MiniBagGlyphs`), tinted with one dark ink, generated once at the floor's first build and destroyed
with the view — no new texture asset, nothing to license, and nothing created while the floor runs.

Two measurements shaped it: white ink on the offering's gold falls to **1.7 : 1**, while the ink used here
clears **3.5 : 1** on all five category colours; and a two-pixel edge disappears at 720p, so the dagger's
blade is six pixels wide (its opaque share is 0.17 of the mask, the lowest of the six). **A bag keeps its
tier number** — it has no category, and its cloth cells are already its shape.

### The tier comes back as corner pips (fourth pass)

Dropping the tier number left one gap: a player could not see that an item was a tier 1 or a tier 3 without
opening the camp. A **strip of dots in the box's bottom-right corner** fills it — one dot per tier, three
at the most (`data/tables/constant.csv` caps `MaxTier` at 3), drawn in the same dark ink as the mark.

The dots are generated, not a digit, for a measured reason: at a ~60 px box the project's 22 px type floor
is a third of the box, and a 22 px numeral in a corner either wears a light chip — which hides the category
mark — or has nowhere to sit. The strip is anchored to the corner, so it never takes the mark's centre:
one dot is 6 px of a 26 px sprite (`MiniBagGlyphs.PipStripWidth`), drawn at 27 px wide on a 1080p box and
16 px at 720p, where a 6/3 dot/gap still resolves into separate dots (measured by downscaling the mask:
1, 2 and 3 dots come back as 1, 2 and 3 runs at both sizes). On the shipped captures the tier-1 dot is
**4 × 5 px** at 1920 × 1080 and **3 × 3 px** at 1280 × 720. A bag keeps its number in the middle — it has no
category mark to make room for — and an item whose tier is above the run's ceiling is drawn with the
highest strip there is, never a strip that does not exist.

## Not tested, and the risks

- **F12 in a browser.** Chrome uses F12 for its own DevTools, so a WebGL player cannot rely on the key;
  the Playwright spec presses it but the browser may swallow it. The key is one constant
  (`DevMenuView.ToggleKey`) precisely so a WebGL-safe binding can be chosen later; the browser spec is
  NOT RUN, as ordered.
- **A 1920 × 1080 Play-mode screenshot of the F12 panel** could not be taken: the Editor's Game View is
  fixed at 1280 × 720 here and there is no public API to change it from a script. The 1080p evidence is
  the Edit-mode photograph, which draws the authored panel.
- **The Edit-mode capture always shows the dev panel**, because the panel is authored active and the
  view hides it in `Start` — the capture never runs Play Mode. It is evidence of layout, not of the
  key.
- **The largest bag is 3 × 3 today.** A 4 × 4 bag would fit at 40 px cells and still meet the 16 px
  floor, but `BattleViewCaps.MiniBagCells` (9 × 7) is what a bigger board would have to be re-checked
  against.
- **A player with twelve items** gets a grid of boxes with a category mark and a tier strip, not item
  icons — the placeholder art the camp's จัดย่าม uses. Unchanged from 08c; no art was added.
- **Tier 2 and 3 have no screenshot.** The starting items are tier 1 and no floor reached a merge during
  the capture, so the two- and three-dot strips are proved by the mask test and by downscaling the mask
  (1/2/3 runs at both sizes), not by a photograph. Recorded as measured-but-not-photographed.
- **One flaky PlayMode run.** After the palette change, the first whole-class run of
  `BattlePlayModeTests` reported 11/12; the same class was 12/12 immediately before it and 12/12 on the
  re-run, and the five cases this stage touches (floor start, ten ticks, HUD area, plates, dev menu)
  each passed on their own. The failing case could not be named: the pipeline reports counts, not
  names, and the Editor log carries no assertion text for it. Recorded as unexplained rather than
  passed.

## Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast   # EditMode step FAILED first (see below)
unity command test run --mode editor   --async_tests                              # 312/312
unity command test run --mode playmode --filter BattlePlayModeTests --filter_type testName   # 12/12
unity command test run --mode playmode --filter ResultScenePlayModeTests --filter_type testName  # 3/3
unity command editor invoke_menu --path "ProjectSurvive/Stage 08/3. Create Battle scene"
unity command editor invoke_menu --path "ProjectSurvive/Stage 08e/1. Build arena (theme + seed)"
unity command editor invoke_menu --path "ProjectSurvive/Tools/1. Capture UI (Title + Camp + Battle + Result, 1920x1080 + 1280x720)"

# fourth pass (the tier pips)
unity command test run --mode editor   --filter BattleMiniBag                     # 23/23
unity command test run --mode playmode --filter <test name> --filter_type testName --async_tests
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast    # PASS 13 | FAIL 0 | NOT RUN 3 | WARN 0
```

The first `-Suite Fast` run reported `unity: EditMode` **308/312**: four `ArenaEnvironmentTests` cases
failed because the scene had been rebuilt without the arena (the `08e/1` step above). That is the
failure the rebuilt scene produced, not a flake; after the arena was re-added the same run is 312/312.
The console also carried `ExecuteMenuItem failed … no menu named 'ProjectSurvive/Tools/1. Capture UI'`
from an earlier invocation with the short name; the menu's real title is the long one above.
