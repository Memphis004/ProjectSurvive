# Stage 13c-2b — the battle pause overlay

## 1. Summary

The arena can now be held still. **หยุด** is a control in the floor panel; it opens a card on its own
canvas above the HUD, and the runner's fixed step stops entirely — no ticks accumulate, so the time a
player spends reading the menu is not replayed at them on เล่นต่อ. The overlay has two ways out:
**เล่นต่อ**, which resumes the same floor, and **กลับศาลา**, which leaves without settling it. The
retreat is deliberately *not* a surrender: `RunSession.AbandonFloor(currentHp)` is a new Application
command that carries the damage the floor did into the run, keeps the purse, the floor cursor and the
shop's offers, unlocks the bag and returns to the camp the run departed from, so the abandoned floor
can be departed for again. Two defects were found only by looking rather than by assert: the card was
authored in the HUD's near-black and read as text floating on the frozen arena, and the overlay's
five strings were first published before the scene's director attached the floor, so the browser read
an empty document. Both are fixed and re-verified.

## 2. Acceptance

| # | Item | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | หยุด opens the overlay and stops the floor | **PASS** | xUnit/EditMode `Pausing_holds_the_floor_and_resuming_lets_it_run_again`; browser spec reads `paused: true` |
| 2 | The clock really stops, and nothing is owed on resume | **PASS** | browser spec: the overlay's `tick` is identical across 1.5 s, then the battle document's `stepsRun` passes the frozen tick after เล่นต่อ |
| 3 | เล่นต่อ resumes the same floor | **PASS** | same spec; `open` goes false and the runner steps again |
| 4 | กลับศาลา leaves the floor without ending the run | **PASS** | xUnit `Abandoning_a_floor_returns_to_the_same_camp_and_keeps_the_damage_taken`; browser spec: `flow` Camp, `floor` **1**, `canDepart` true |
| 5 | The damage taken is carried back (the menu is not a free heal) | **PASS** | `AbandonFloor` clamps `currentHp`; xUnit asserts `Hp == before − 37` and the bag is unlocked; browser spec compares the camp's `hp` with the overlay's last `hp` |
| 6 | A retreat is not a clear and pays nothing | **PASS** | xUnit: `FloorsCleared == 0`, purse and floor cursor unchanged, the same floor can be departed for again |
| 7 | The overlay's layout holds at the authored sizes | **PASS** | EditMode `The_overlay_cannot_be_read_through_and_its_controls_stay_inside_the_card` (card alpha 0.98, no control escapes it) and `The_HUDs_pause_control_is_drawn_inside_the_floor_panel` |
| 8 | No player-facing text is hard-coded | **PASS** | five localization rows (`ui.pause.*`); the player reports `Battle: 35 labels fit, 23 of them Thai, 0 missing glyphs` |
| 9 | The overlay reads as a menu over the frozen arena | **PASS** | [screenshot](../artifacts/stage13c-2b/web-battle-pause.png) — visible card edge, heading, warning and both captions; after the first capture prompted a lighter card |
| 10 | PlayMode (the runner's new pause flag) | **NOT RUN** | No Editor bridge this session, and a batch-mode PlayMode run records zero tests. See §3. |

## 3. What was not tested, and the risks

- **PlayMode did not run.** The MCP Editor bridge is still unavailable, and `-runTests -testPlatform
  PlayMode` in batch mode writes a results file with `total: 0`. The runner's pause flag, the
  director's leave retry and the overlay's Start-order handling are covered by EditMode and the built
  player, but a PlayMode pass over `SceneFlowPlayModeTests` is still owed before 13c closes.
- **The HP carry was not exercised in the browser.** The standing player finished the measured floor
  at full HP, so the browser's equality check was 100 = 100; the damage case is proved by xUnit.
- **Escape was not driven by a browser test.** The key path exists (Input System, same adapter family
  as WASD) and is why the HUD control also exists, but only the pointer path is asserted.
- **No PlayMode frame was profiled.** Pausing skips the runner's whole `Update`, which is strictly
  less work than running; no timing claim is made.
- **A pre-existing failure was found and fixed.** `ConfigLoadTests` pinned the localization row count
  at 228 while `data/` held 232 since Stage 13c-2a's four settings rows, so the Fast suite failed at
  `HEAD` before this stage touched it. The pin is now 237 with the history written into the comment.

## 4. Commands actually run

```powershell
tools/gen.ps1                                   # localization 237 rows, configHash 10CF3726…
Unity.exe -batchmode -executeMethod ProjectSurvive.Game.Editor.BattleSceneBuilder.Create
Unity.exe -batchmode -runTests -testPlatform EditMode -testResults artifacts/stage13c-2b/editmode.xml
dotnet test lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj          # 604/604
tools/build-web.ps1 -Force                     # Development, 0 C# errors, 122 292 971 B
node tools/serve-web.mjs
cd tests/web; npx playwright test              # 18 passed, 13 skipped, 0 failed (4.2 m)
tools/gen-wiki.ps1; tools/verify-wiki.ps1      # 5 PASS, 0 FAIL
```
