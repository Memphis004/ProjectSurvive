# Stage 09g — the upgrade-choice window on SceneCamp

## 1. Summary

A drop of one piece onto another with the **same id and tier** no longer folds silently: the camp asks
`RunSession.OfferMerge`, and when that step has more than one upgrade variant the screen opens a
**modal** built at runtime by `CampBagScreenView` from one published model
(`CampUiState.MergeChooser` → `CampMergeChooserModel` / `CampMergeChoiceModel`). The window is a
0.72 dim layer that swallows pointer events (the physical board lock), a 1560×600 panel with three
328×400 cards, and a left panel that prints **the same five lines the floating tooltip publishes**
from one producer (`CollectSourceLines`), so "เนื้อหาเดียวกับ tooltip" is structural rather than a
copy. A card is picked with a click, a tap, or `1 2 3`; `Escape` and the back button close the
window without changing anything. The engine behind it (`OfferMerge` / `ConfirmMerge`, the pure
draw, per-step variant ids, snapshot schema 2) is the second half of 09f and is recorded in
[ADR 0021](../adr/0021-merge-upgrade-choice.md); the Core change this stage needed is that
`PreviewDrop` now **accepts** the merge gesture instead of refusing `inventory.merge_needs_variant`.
Evidence: dotnet **466**, `-Suite Fast` **PASS 10 / FAIL 0 / NOT RUN 3**, EditMode **94/94**,
`CampMergeChooserPlayModeTests` **4/4** and the camp drag fixture **12/12** with real pointer events,
and the window captured in Play Mode at both sizes.

## 2. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| 1 | Dropping an item onto one with the same id and tier opens the modal when `OfferMerge` returns choices, and folds immediately when it does not | **PASS** — `Dropping_a_matching_pair_opens_the_chooser_and_changes_nothing`, `A_pair_without_choices_still_folds_on_the_drop` (EditMode); the drop path in `CampPresenter` reads the offer only, it computes nothing (`RunSession.OfferMerge` is the only producer) |
| 2 | The window: dim behind, three cards laid out horizontally at centre, each naming its variant plus the item, its `ระดับ N → N+1`, one line per changed stat (green better / red worse) and a pick button; left panel = the item's current values (same content as the tooltip) with a short line below | **PASS** — `The_cards_are_the_offer_and_every_line_is_the_variants_own_data`, `The_left_panel_is_the_same_content_the_tooltip_shows` (EditMode: the panel's strings equal the tooltip's, by construction not by copy); colours come from the Domain's `UpgradeVocabulary.IsImprovement`; geometry `ChooserPanelSize` 1560×600, cards `328×400`, `_chooserDim` α **0.72**, hint line, back button top-right — measured in §4 |
| 3 | Picked with a mouse/tap or `1 2 3`; the back button or `Escape` closes without changing anything, and reopening offers the same cards | **PASS** — `Pressing_a_number_key_picks_the_card_with_that_number`, `Escape_and_the_back_control_close_the_chooser_and_change_nothing` (PlayMode, real pointer + key events, run hash byte-identical), `Cancelling_changes_nothing_and_reopening_offers_the_same_cards` (the offer is a pure function of the run — ADR 0021) |
| 4 | While it is open: the bag drag is locked, the tooltip does not appear over it, a second click cannot fold twice | **PASS** — `While_the_chooser_is_open_the_board_is_locked_and_a_second_pick_cannot_fold_twice`, `A_pick_with_no_chooser_open_is_refused_and_changes_nothing` (EditMode), `While_the_chooser_is_open_the_board_is_locked_and_no_tooltip_is_drawn` (PlayMode); the presenter closes the window **before** the command runs, and the dim layer intercepts the pointer |
| 5 | After picking: the item is the new tier **in the same cell**, loadout / synergy / lines recomputed, bia and chest unchanged | **PASS** — `Confirming_a_card_raises_the_tier_and_the_pick_is_what_the_loadout_gets`: tier 2 at its old cell, `Bia` unchanged, chest one fewer (the twin was carried in the chest and the merge consumed it — not one more), `Counters.Merged` 1, the bag screen recomputed from the new state, the picked variant on record, and two runs picking two different cards resolve to two different weapon lines |
| 6 | Thai text through localization, ≥ 22 px at 1080p; no overlapping elements at 1920×1080 and 1280×720 (existing Editor capture tool) | **PASS** — 20 new rows in `data/localization/localization.csv` (159 → **179**), all of them TH + EN (`data: generated tables validate`); `UiLayoutCheckTests` 19 (12 → 19, including the modal's own rules); captures measured in §4 — every modal label ≥ 22 px at the canvas scaler's 1.0 and no rectangle pair overlapping at either size |
| 7 | PlayMode with real pointer events: merge → window opens → pick the 2nd card → the item changes; `Escape` → nothing changes | **PASS** — `CampMergeChooserPlayModeTests` **4/4** (§3); the existing `CampBagDragPlayModeTests` still **12/12** alone |
| 8 | Tests pass, Editor captures of the window at two sizes, PASS / FAIL / NOT RUN | **PASS** for the Editor work — captures + this report; the browser suite and the bulk PlayMode suite are **NOT RUN** (§5) |

## 3. Commands run

```pwsh
pwsh -NoProfile -File tools/gen.ps1                       # after the 20 localization rows + the 2 upgrade tables
dotnet test lib/Tests~/Core.Tests/Core.Tests.csproj       # 466 passed
pwsh -NoProfile -File tools/test.ps1 -Suite Fast          # closing run: PASS 10 | FAIL 0 | NOT RUN 3, 86.2 s, WARN 0
#   -> dotnet 466, sim determinism + stream separation, data validate/reproducible/20 fixtures, unity EditMode 94/94
unity command run_tests --mode playmode --filter_type assembly <Camp chooser fixture>   # 4/4
unity command run_tests --mode playmode --filter_type assembly <Camp drag fixture>      # 12/12
unity command screenshot --view game …                    # the four images in §4, at both Game View sizes
```

## 4. Evidence

- `artifacts/stage09g/fast-suite.log` — the closing Fast run (`EXIT=0`), including `dotnet test (xUnit) 466 passed`
  and `unity: EditMode 94/94 passed`.
- `artifacts/ui/stage09g-chooser-1920x1080.png`, `…-1280x720.png` — the window open over the bag board, and
  `…-closed-1920x1080.png`, `…-closed-1280x720.png` — the same board with it closed. Measured from the images
  rather than eyeballed: the dim is the whole canvas at α 0.72, the panel 1560×600 at (180,240) at 1920×1080
  and 1040×400 at (120,160) at 1280×720, the three cards 328×400 → 219×267 at x = 453 / 683 / 912, and the
  smallest modal label 22 px at 1080p (the canvas scaler at 1.0).
- The three EditMode tests of §2.2/§2.3 pin the modal's content against the offer and the tooltip, and the
  PlayMode fixture drives the whole gesture with injected pointer and key events, so the window is not only
  asserted through its own model.

## 5. Not tested, and risks

- **The browser suite was not run** (no WebGL build this stage; 09g is not one of the web gates). `tests/web/camp.spec.ts`
  was re-pointed at the window's key path and is **unverified** — treat it as untested until a `-Suite Full` run.
- **The bulk PlayMode suite was not run**; the two camp fixtures were run alone (4/4 and 12/12). The stage-09f report
  records that the bulk run carries 12 pre-existing `Input System` ambient-log failures of no assertion of ours
  (`unity: PlayMode 16/28`, 12 passed), and the assembly now holds **33** tests.
- **One dead localization row.** `camp.merge.source.title` ("ค่าปัจจุบันของไอเทม") is in `data/localization/localization.csv`
  but no control reads it: the left panel opens with the piece's own name line (the tooltip's first line) as its heading.
  Bind it to a caption or drop the row in a follow-up; it is one of the 20 rows this stage added.
- The ≥ 22 px and "no overlap" claims hold at the two captured sizes at scaler 1.0; other aspect ratios are not covered.
- Unrelated working-tree changes left untouched and **not** committed: `Boot.unity` (one line, `ThaiTextSpike` removed from
  the boot allow-list — pre-existing, already noted in the 09f report) and the TMP `LiberationSans SDF - Fallback` asset
  (a Unity 6 re-serialisation, `serializedVersion` 6 → 8 only).
