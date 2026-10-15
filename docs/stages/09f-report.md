# Stage 09f — a bag move or turn carries the items standing on it

## 1. Summary

A bag is no longer a cell provider that may not move while an item stands on it: on the board a
bag **carries** what stands on it. `FootprintRotation.Rotate` now publishes the translation its
re-normalisation removed, `Backpack.CollectRiders` maps every rider cell through the *same*
transform as the bag's own shape (so a pure move is the zero-turn case of one function and the two
cannot disagree), and `BackpackEditor.CanPlace` fills the carry plan as it answers — the accepted
command applies exactly that plan, so a preview and a command cannot land an item in two places.
The refusal rule narrowed to its two real cases: taking a bag **off** the board with items on it
(`inventory.item_stranded`) and an item lying across two bags that would lose its cover
(`inventory.item_spans_bags`, new, localized in both languages). The screen draws a ghost per rider
at the cell it will land on, from the run's plan (the stage's own words: *"แสดงเงาของไอเทมที่ติดไป
ที่ตำแหน่งใหม่"*), and a 2 000-move drag allocates 0 bytes. Sixteen new tests carry the rule from
the domain to a real pointer gesture; [ADR 0020](../adr/0020-bag-moves-carry-the-items-on-them.md)
records the decision and supersedes ADR 0014 §5.

## 2. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| 1 | A bag move on the board takes every item standing on it, and the items keep their place in the bag's frame | **PASS** — `Moving_a_bag_carries_the_item_standing_on_it`, `The_carried_plan_is_where_the_items_actually_end_up` (Core), `A_bag_drag_carries_the_items_on_it_and_the_plan_says_where_they_land` (EditMode), and the live gesture: `artifacts/stage09f/probe.txt` → `dropped placed=1 dragging=False … BAG Bag#1 zone=Board at 5,4 \| ITEM Item#2 zone=Board at 5,4` |
| 2 | A bag **turn** carries them too — move and turn are one transform, rigid for an asymmetric bag | **PASS** — `Rotating_a_bag_carries_the_items_standing_on_it`, `Four_quarter_turns_return_a_bag_and_an_asymmetric_item_on_it` (the chiral L bag; the naive "translate by the bag's delta" implementation fails this) |
| 3 | An item lying across two bags does not ride, and the command is refused whole when it would lose its cover | **PASS** — `A_bag_move_that_would_leave_a_spanning_item_uncovered_is_refused_whole`, `A_bag_move_is_refused_whole_when_it_would_leave_a_two_bag_item_uncovered`, `A_bag_move_carries_the_items_on_it_and_a_store_that_would_strand_one_is_refused` |
| 4 | Taking a bag **off** the board (chest / altar) with an item on it is still refused — an item never enters the chest | **PASS** — same `…a_store_that_would_strand_one_is_refused` case; `inventory.item_stranded` narrowed to exactly this (ADR 0020 §4) |
| 5 | A new, separately localized refusal code for the spanning case | **PASS** — `InventoryError.ItemSpansBags` + `error.inventory.item_spans_bags` in both languages (`data/localization/localization.csv`; TH: *"ไอเทมนี้พาดอยู่บนย่ามสองใบ ย้ายไอเทมไปหีบหรือจัดใหม่ก่อน"*), 145 → 146 rows, `ConfigLoadTests` pin updated |
| 6 | The preview says where each rider lands, and the screen draws a ghost per rider at that cell | **PASS** — `Dragging_a_bag_publishes_a_ghost_for_every_item_that_rides_and_where_it_lands` (EditMode: one ghost per rider, at the landed cell and with the footprint it lands in), `Dragging_a_bag_carries_the_items_standing_on_it` (PlayMode, real pointer). A live probe read mid-gesture in the same session reported `carry=1 … ghosts=1`, but only the post-drop line was kept in `probe.txt` |
| 7 | No per-pointer-move allocation while a bag is in the air (AGENTS 8) | **PASS (measured)** — `A_pointer_move_inside_one_cell_allocates_nothing_while_a_bag_is_in_the_air`: 2 000 same-cell `DragMove` calls, **0 bytes** on the thread's allocation counter, plan carried |
| 8 | One gesture = one revision = one undo step; combos recomputed from the new layout | **PASS** — `Undo_brings_a_bag_and_every_item_on_it_back_in_one_step`, `A_bag_turn_takes_the_combo_line_with_the_items_that_ride` |
| 9 | 10 000 mixed commands earlier 0 invariant violations | **PASS** — `Ten_thousand_bag_gestures_carry_the_items_that_ride_and_keep_every_invariant` |
| 10 | A drag that is refused moves **nothing** — neither the bag nor its rider | **PASS** — `A_refused_drop_leaves_the_piece_where_it_was` (PlayMode, real pointer, far-corner destination) |
| 11 | Evidence images of a bag with an item on it, before and after the move | **PASS** — `artifacts/ui/stage09f-before.png`, `artifacts/ui/stage09f-after.png`, measured (§4) |
| 12 | A picture of the rider ghosts mid-drag | **NOT RUN** — see §5 |
| 13 | The camp still works in the real WebGL player (IL2CPP), not just in the Editor | **PASS** — `web: Playwright` 6/8 then 5/6 on a rerun: the **battle and camp specs pass** in the built player, whose own self-check reports 7/7 probes; the one repeatable failure is the Thai-spike *URL route*, broken by an unrelated pre-existing `Boot.unity` edit (§5, §6) |

## 3. Commands run

```pwsh
pwsh -NoProfile -File tools/gen.ps1                                    # after the localization row
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                       # 2 runs: the first FAILED the stale pin (432/433), the second OK
pwsh -NoProfile -File tools/test.ps1 -Suite Full                       # Editor open: PASS 10 / FAIL 1 / NOT RUN 2
pwsh -NoProfile -File tools/test.ps1 -Suite Full                       # Editor closed: WebGL + Playwright
unity command run_tests --mode playmode --async_tests ...              # Camp fixture alone: 12/12

cd tests/web; node ../../artifacts/stage09f/probe-scene.mjs "http://127.0.0.1:8080/?scene=ThaiTextSpike"
cd tests/web; npx playwright test smoke.spec.ts camp.spec.ts           # rerun against the same build
```

## 4. Evidence

- `artifacts/stage09f/probe.txt` — read out of the live Editor after an injected pointer drag:
  `dropped placed=1 dragging=False ghosts=0 | BAG Bag#1 zone=Board at 5,4 | ITEM Item#2 zone=Board at 5,4`
  (the same probe, read while the drag was still in the air, reported `carry=1 … ghosts=1`; that line was
  overwritten by the final one, so the ghost counts are asserted by the EditMode and PlayMode tests).
- `artifacts/ui/stage09f-before.png` / `-after.png` — the two states at 1920×1080, measured with
  `System.Drawing.GetPixel` rather than eyeballed:
  - the item's own art colour `#bba386` forms a **70×74 px blob — one board cell (pitch ≈ 82 px)** at
    `(42,142)–(112,216)`, centroid `(80,168)` = board cell **(0,0)**, before; and the same-sized blob at
    `(452,470)–(522,544)`, centroid `(490,497)` = cell **(5,4)**, after. Same blob, same size (rigid, no
    rotation), translated by `(410,329) px` = the 5,4 cell delta.
  - the bag's old cell `(2,0)`@`(243,179)` goes `#5a845a` (the bag's green) → `#1c212d` (board), and its
    new selected cell `(7,6)`@`(571,589)` goes `#1b212d` → `#f2d94b` (the selection yellow).
  - every differing pixel lies inside `(40,134)–(690,708)` — the board. No panel, HUD, label or status
    pixel changed, so the gesture moved the assembly and nothing else.
- `artifacts/stage09f/fast-suite-after-label-fix.log` — dotnet 433 passed, the four data gates, EditMode
  **79/79** (75 at Stage 09e).
- `artifacts/dotnet/unity-playmode-status.json` — the bulk PlayMode payload (see §5).
- Suite totals: **16 new test methods** across `lib/Tests~/Core.Tests/**` and
  `UnityProject/Assets/Main/Tests/{EditMode,PlayMode}`.
- The Fast pin did its job: the first run of this stage **failed** `dotnet test` 432/433, because
  `ConfigLoadTests` pinned 145 localization rows and the new `error.inventory.item_spans_bags` line makes
  146. The row was added to `data/`, `tools/gen.ps1` was re-run (which also moved `ConfigBuildMetadata`'s
  `ConfigHash` and the built player's `configHash` to `5E60F3A8…`), and the pin was corrected to 146.

## 5. Not tested, and risks

- **No image of the rider ghost mid-drag.** The two captures above are the landed state (before/after);
  the mid-gesture frame could not be captured separately — the Editor capture path returned the same
  frame twice (identical bytes), so no picture of the ghosts exists. The ghosts are not untested: the
  PlayMode test asserts a ghost is drawn for every rider **at the cell the item really lands on**, the
  EditMode test asserts the published plan (`Carry`) matches it, and the live probe recorded
  `ghosts=1` while the drag was in the air. What is missing is the picture.
- **The bulk PlayMode suite fails as a suite, with no failed assertion of ours** — 12 passed / 16 not
  of 28: 12 `IndexOutOfRangeException` inside `InputSystem.InputManagerStateMonitors` (via
  `InputSystemUIInputModule.OnEnable`) and 4 `[Error] Cached unprocessed value unexpectedly became
  outdated`, i.e. the ambient-message log asserts of the Input System package. The Camp fixture passes
  **12/12** on its own in 3.71 s, including `Dragging_a_bag_carries_the_items_standing_on_it`. This is
  the pre-existing, still-unsolved item in `llmwiki/stage-log.md` and `docs/testing/test-suites.md` §7 —
  reported, not tidied, and unchanged by this stage (09e measured 15 of 27 the same way).
- **The WebGL player boots and the camp works in it, but one browser spec fails for a reason that is not
  this stage's**: the 8 Playwright tests ran 6/8, and a re-run of the same build (`smoke.spec.ts` +
  `camp.spec.ts`) gave **5 passed / 1 failed** — the camp canvas-resize test that failed in the first
  pass was green on the retry, and the one repeatable failure is
  `smoke.spec.ts renders the Thai text spike with full glyph coverage`. Cause, proven rather than
  guessed: `?scene=ThaiTextSpike` now loads **Title**, because `Boot.unity`'s `_urlSceneOverrides` no
  longer lists `ThaiTextSpike` — the working tree has that one line deleted, and it was already deleted
  **before this stage started** (the session's opening `git` summary lists `Boot.unity` as modified, and
  this stage never touches `SceneBoot`, the Boot scene or the allow-list). A direct probe of the built
  player says so: `{"url":"…?scene=ThaiTextSpike","scene":"Title","hasThaiSpike":false,"hasSmoke":true}`
  (`artifacts/stage09f/probe-scene.mjs`), i.e. `SceneBoot.IsAllowListed` falls back to `_titleSceneName`.
  The code's own source of truth still lists the scene
  (`ProjectSetup.UrlSceneAllowList = { "ThaiTextSpike", "Battle", "Camp" }`), so the honest read is a
  stale scene file, not a withdrawn route: the fix is that one line restored in `Boot.unity` (or
  `ProjectSetup` re-applying the allow-list) and a rebuild — left alone here as unrelated work
  (AGENTS rule 1), which is why the `web:` steps below read as they do.
- **No performance claim beyond the drag path.** The 0-byte measurement covers same-cell pointer moves;
  a publish that changes the plan allocates the published array (one per changed plan, pooled ghosts in
  the view), and no tick/frame budget was measured this stage.
- **Unrelated working-tree files left alone** (AGENTS rule 1): the pre-existing `Boot.unity` edit and the
  untracked `UnityProject/Assets/_Recovery/` are untouched and not part of this commit.

## 6. Web pass (Editor closed)

`artifacts/stage09f/full-suite-editor-closed.log` — `PASS 10 | FAIL 1 | NOT RUN 2`, 587.4 s:

| Step | Result |
| --- | --- |
| dotnet build + test, the four data gates | **PASS** — 433 passed; regeneration reproduces the tracked output |
| `unity: EditMode` / `unity: PlayMode` | **NOT RUN** — no Editor connected (the Editor has to be closed for the build step; these two ran green in the Editor-open pass above) |
| `web: WebGL build (Development)` | **PASS** — 241.1 s → `UnityProject/Builds/Web`; the built player's own self-check reports `passed: true`, `probeCount: 7`, `failedCount: 0`, `configHash 5E60F3A8…` matching the regenerated tables and `localization=146`, and the 7 probes include the Thai-text one |
| `web: Playwright (smoke + battle + camp)` | **FAIL** — 6 passed / 2 failed in 285.7 s; a rerun of `smoke.spec.ts` + `camp.spec.ts` against the same build gave **5 passed / 1 failed** (`artifacts/stage09f/playwright-rerun.log`). One failure is flake (the camp canvas-resize test, green on the rerun); the other is the Thai-spike route (§5), which fails identically twice and is not this stage's |

So: the **camp and battle specs pass in the real WebGL player**, including the panel, the drag counters and
the published geometry; the single repeatable failure is the spike scene's URL route, whose allow-list the
working tree has lost.
