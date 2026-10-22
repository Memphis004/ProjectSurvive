# Stage 13b — the first-run checklist

**Date:** 2026-10-22 · **Gate:** Stage 13's UX half (no WebGL/Playwright gate of its own; both were
run anyway, see §3)

## 1. Summary

The camp now teaches itself. A profile that has never finished the camp's five-step checklist sees
it in the instruction block of the จัดย่าม panel — rent → place → rotate → read the tooltip → depart
— with `[>]` on the step to do, `[x]` on the ones done and `[ ]` on the rest, and the status bar
naming the next step (`ขั้นที่ N/5: …`). The steps tick only from a command that **succeeded**
(`Rent`, a board drop of an item, a turn, a piece's tooltip, a departure), in any order, and nothing
in the game consults them: a player who ignores the checklist plays exactly the same game. Finishing
it writes one flag into the settings file (`SettingsDto` key 4), and every later visit gets the
static instructions back. The checklist lives in `CampTutorial` (Presentation, no UnityEngine), the
labels are the five existing instruction rows, and the only new state is that one flag.

## 2. Acceptance

| # | Claim | Result | Evidence |
| --- | --- | --- | --- |
| 1 | A new profile sees the five-step checklist; step 1 is the rent | **PASS** | `artifacts/stage13b/checklist.png` (real WebGL player), `camp.spec.ts` "a new profile is walked through…" (`tutorialActive/tutorialStep/tutorialDone`), `CampTutorialTests.A_new_profile_gets_the_checklist_and_the_rows_tick_as_commands_land` |
| 2 | Steps tick from the real commands, out of order, only on success | **PASS** | `CampTutorialTests.Steps_tick_independently_and_in_any_order`, `…A_refused_command_ticks_no_step`, `…Finishing_every_step…` (rent → drag-drop → right-click → hover → depart through the presenter) |
| 3 | Finishing it restores the static instructions and never comes back | **PASS** | `CampTutorialTests.Finishing_every_step…`, `…A_profile_that_finished_the_checklist_before_sees_the_static_instructions`; the flag survives a settings round trip (`CheckpointTests.Settings_round_trip…`, xUnit) and reaches storage in the browser (`camp.spec.ts` asserts `__projectSurvivePersistence.tutorialCompleted`) |
| 4 | Text is localized and fits, in the shipped font | **PASS** | 10 new `camp.tutorial.*` rows (table 228 rows); `camp.spec.ts` prints five rows and `expectTextFits` measures **36 labels, 0 overflow, 0 missing glyphs** in the real player |
| 5 | No player-facing text in code | **PASS** | `ArchitectureTests.Runtime_sources_embed_no_player_facing_text`, `…Every_localization_key_the_runtime_asks_for_exists` |
| 6 | **A new player starts the first battle unassisted (≥ 1 real person, recorded)** | **NOT RUN** | needs a person at a keyboard. Everything a person would be shown exists and is screenshot-checked; the human playtest itself has not happened, so Stage 13's headline acceptance is **still open** |

## 3. What was run

```
tools/gen.ps1                                   data → tables (configHash 5D808C05…)
dotnet test lib/Tests~/Core.Tests               601 passed
tools/test.ps1 -Suite Fast                      10 PASS, 0 FAIL (EditMode NOT RUN: no Editor connected at that moment)
Unity EditMode (whole assembly)                 185/185 passed
Unity PlayMode (per class, the §7 protocol)     75/75 passed — 10 classes
tools/build-web.ps1 -Force                      PASS, configHash 5D808C05, 121 784 635 B
Playwright: camp.spec (5) + smoke/title/result/persistence (7) + battle (2)   14/14 passed
```

`tools/test.ps1 -Suite Full` (run once at the close of the stage) reported **PASS 11 | FAIL 1 | NOT RUN
2**: xUnit, the sims, the data validator, the wiki check and EditMode (185/185) all passed; the Unity
PlayMode step reported **31/75** and the WebGL step could not run because the Editor held the project
lock. Both are the suite's known shape, not this stage's regressions:

- the bulk PlayMode run is the ambient-log flake of `docs/testing/test-suites.md` §7 (the same
  `Cached unprocessed value` messages as every stage since 09). Per class, **75/75** pass, which is
  the evidence that section accepts;
- the WebGL build cannot share the project with an open Editor, so it is run after the Editor closes.
  The build and the whole Playwright suite were run that way against this exact tree
  (configHash `5D808C05…`, **14/14** browser tests).

## 4. Not tested / risks

- **The human playtest (acceptance 6).** Nothing here can stand in for it. It is the one gate 13b
  exists for, and it is open.
- The second-visit path is proven by the settings round trip and by the camp scope reading the flag,
  not by reloading the page after a finished run: after the departure the run is in the battle, so a
  reload lands in the battle. A hand check (finish the checklist, close the tab, reopen the camp) is
  cheap and was not done.
- The checklist's `tutorialStep` is `Current + 1`, so a finished checklist reports `5`; a reader that
  needs "no step" must use `tutorialComplete`, which is what the browser test does.
- Settings written before this stage have no key 4 and decode as "not finished", so an existing
  player meets the checklist once. That is intended, and stated in `SettingsDto`.
- **One transient xUnit failure** (1 of 601) appeared in the first `-Suite Fast` run of this stage;
  the suite's summary does not name it, and it did not reproduce: a direct `dotnet test` (601/601)
  and a second `-Suite Fast` (601/601, RESULT: OK) both passed afterwards. Recorded rather than
  explained away.
- Rebuilding `Camp.unity` rewrites every `fileID` (the builders are deterministic in content, not in
  ids), so the scene diff is the whole file. The four other scenes were not rebuilt.
