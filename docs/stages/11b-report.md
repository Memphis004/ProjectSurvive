# Stage 11b — Krasue and Nang Tani, with telegraphs (Simulation + Presentation)

**Date:** 2026-10-20; verified and re-run 2026-10-21

## 1. Summary

Krasue now runs her own cycle — warn a line, dash it, warn an aura, drain, retreat — and Nang Tani
stands still and alternates a ring around herself with a vine lash along a line. Every strong hit is
preceded by a `TelegraphStarted` event that carries the footprint the damage really uses (shape,
origin, radius, length, angle), so the floor decal cannot warn about a smaller area than the attack
hits; `TelegraphEnded` closes it in the tick the stage ends. All fifteen numbers live on the `enemy`
row and are validated; one FSM serves both ghosts, with steering stepping aside while a ghost is not
chasing. `BattleEvent` gained three additive fields and the enum was **not** extended.

The 2026-10-21 pass re-ran every check below from the uncommitted tree, reproduced the capture byte
for byte (tick 272, one warning), and corrected two things it found: the `BattleEventType` doc
comments still called a telegraph's `DefId` a *skill* def (it is the enemy def), and this report's
browser numbers described the pre-retry attempt instead of the green closed-Editor run.

## 2. Acceptance

| # | Item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | A small per-kind FSM in Simulation: Krasue dash → drain → retreat; Nang Tani stands, rings and lashes | **PASS** | `EnemyAttackSystem` stages + `EnemyAttackBehaviorTests.Krasue_walks_telegraph_dash_drain_retreat_in_that_order` |
| 2 | Telegraph (shape, warning time) travels as an event and Presentation draws the floor warning; every strong attack is telegraphed | **PASS** | `TelegraphPool` (`Vfx`), `BattlePresentationTests.A_telegraph_event_becomes_a_pooled_floor_decal`, the shared contract case `BattleSessionContractTests.A_telegraph_that_goes_up_ends_with_a_defined_footprint`, `artifacts/ui/stage11b-telegraph-1920x1080.png` |
| 3 | All parameters in `data/` (`enemy`), with gen + validation + fixture | **PASS** | 15 new columns + `EnemyAttackPattern`; `tools/gen.ps1` OK; `validate-data` clean; `check-fixtures` 27/27 (the one `enemy.csv` fixture updated) |
| 4 | The two ghosts are told apart by behaviour, not by looks | **PASS** | line dash + drain + retreat vs stationary ring + vine line; different ranges (9 / 8), timings (0.6 / 1.0 s) and footprints (6 × 0.9 line / 4.5 ring / 7 × 0.55 line) |
| 5 | No new event type: the enum is untouched and `battle-contract.md` + the contract test are updated | **PASS** | `TelegraphStarted = 11` / `TelegraphEnded = 12` unchanged; contract §5.1; shared contract case green for both factories |
| 6 | Tests: per-behaviour Core tests (stage order, telegraph time before the hit), both-factory contract suite, no allocation in a tick | **PASS** | dotnet **555** (8 behaviour tests + 5 validator rules + 1 contract case); `A_tick_that_runs_a_telegraph_allocates_nothing_after_warm_up` = 0 bytes |
| 7 | Editor capture of the warning at 1920 × 1080 | **PASS (needs the user's eyes)** | `artifacts/ui/stage11b-telegraph-1920x1080.png` + `.json` (floor 3, tick 272, 1 warning, 1920×1080). Re-run 2026-10-21: same tick, same warning; the decal measures 236 × 70 px (aspect 3.4 against the row's 6 × 1.8 line), i.e. the picture carries Krasue's dash footprint |

Gates: `-Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (126 s, re-run 2026-10-21) and
Unity EditMode **159/159**. `-Suite Full` with the Editor open ran dotnet + data + wiki + EditMode
green and PlayMode **22/48**, whose 26 failures are the documented ambient Input System log-assert
flake (12 `IndexOutOfRangeException` + 9 `Cached unprocessed`, the same counts every stage since 09d)
rather than a failed assertion of ours; that run is `artifacts/stage11b-full.log`. The closed-Editor
run then **PASS**ed everything it could (`artifacts/stage11b-full-closed.log`): WebGL build OK
(`artifacts/stage11b-web-build.log`, configHash `2B378882…` = the new tables, 120 609 739 B) and
Playwright **10 passed in Chrome** on the real player — including `camp.spec.ts`'s canvas-click layout
case, which had needed its retry in the earlier open-Editor attempt
(`npx playwright test camp.spec.ts:1021` → 1 passed 14.7 s on its own). 11b is not a WebGL gate
stage; the browser run was the `-Suite Full` closing check, not an acceptance item.

## 3. Not tested, and the risks

- **The picture is an arena render, not a browser shot.** It comes from the shipped
  `BattleViewScene` driven by a real floor-3 session (menu `ProjectSurvive/Stage 11b/Capture
  telegraph`), rendered into a 1920×1080 target; the HUD is a separate overlay canvas and is not in
  it. The browser run under `-Suite Full` is Playwright's own loop, not a shot of a warning: no
  browser screenshot of a telegraph was taken.
- **Balance is untuned.** 9 damage for a dash and 12 for a ring are first values; nothing here
  measured how a floor plays with them (`tools/sim.ps1` was not re-run).
- The drain's damage window has the circle warning *before* it, not during it: a player who stays
  inside while the aura ticks is told when it starts, not per tick. If that reads badly in play, an
  aura decal during the window is a presentation change (the simulation already publishes both ends
  of the stage).
- Pob (the boss) still walks and touches: its phases and corpse eating belong to Stage 11c/11d.
- The EditMode suite had one red test from the uncommitted Stage 11a work
  (`CampPresenterTests.The_tooltip_shows_a_charms_and_an_offerings_effect`): a hover published the
  tooltip **before** rebuilding the board cache, so a piece whose placement came from the run
  directly was not in the cache and the tooltip drew nothing. `CampPresenter.Hover` now publishes the
  board first (one-line swap, cause documented in place); that test and the suite are green.

## 4. Commands actually run

```powershell
powershell -NoProfile -File tools/gen.ps1                       # 41 .cs, 15 .bytes, RESULT: OK
dotnet build lib\Tests~\ProjectSurvive.Core.sln                   # 0 warnings, 0 errors
dotnet test  lib\Tests~\ProjectSurvive.Core.sln                   # 555 passed
powershell -NoProfile -File tools/validate-data.ps1 -DataDir lib\Tests~\data   # OK, 0 findings
powershell -NoProfile -File tools/check-fixtures.ps1             # PASS 27 | FAIL 0
powershell -NoProfile -File tools/gen-wiki.ps1                   # 8 pages
unity recompile                                                  # clean
unity command run_tests --mode editor --async_tests              # 159/159
unity command menu "ProjectSurvive/Stage 11b/Capture telegraph (1920x1080)"
powershell -NoProfile -File tools/test.ps1 -Suite Fast       # PASS 11 | FAIL 0 | NOT RUN 3
powershell -NoProfile -File tools/test.ps1 -Suite Full       # Editor-open: PlayMode flake (see above)
powershell -NoProfile -File artifacts/close-unity-editor.ps1  # for the Web gate
powershell -NoProfile -File tools/build-web.ps1 -Configuration Development -Force   # RESULT: OK
node tools/serve-web.mjs --root UnityProject/Builds/Web --port 8080
cd tests/web && npx playwright test --reporter=line
#   open-Editor attempt: 9 passed / 1 failed on camp's canvas click (passed alone afterwards)
#   closed-Editor Full run: 10 passed in Chrome (the log at artifacts/stage11b-full-closed.log)
```
