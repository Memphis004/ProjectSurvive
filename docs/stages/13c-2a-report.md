# Stage 13c-2a — the audio settings panel

## 1. Summary

The title's settings panel now has five rows instead of three: master, **music**, **effects**,
language and starting speed. The two new levels are persisted, applied live to the audio device, and
reachable by a browser test that opens the panel, presses a row and reads both the panel's own
document and the storage verdict. Because the two new keys are volumes — and 0 is a legal volume —
the settings file's schema version went to **2**, with a declared read path for schema 1 that fills
the new levels from the defaults ([ADR 0034](../adr/0034-settings-schema-and-per-bus-volume.md)). The
first browser capture of the new panel then found two visual defects that no assertion had covered:
the title text showed through the 96 %-opaque panel, and the `-`/`+` controls were blank rectangles.
Both are fixed and re-captured.

## 2. Acceptance

| # | Item | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Music and effects levels exist in the settings model, clamp to 0..1 and survive a save/load round trip | **PASS** | `GameSettings.SetMusicVolume/SetSfxVolume`; xUnit `Settings_round_trip_and_invalid_values_are_refused` |
| 2 | A settings file written by the previous build keeps the default mix instead of muting the game | **PASS** | xUnit `Settings_written_before_the_buses_existed_keep_the_default_mix` (schema 1 payload → defaults, and an explicit 0 stays 0 once the keys exist) |
| 3 | An out-of-range bus volume is refused, not clamped by the decoder | **PASS** | xUnit `An_out_of_range_bus_volume_is_refused` |
| 4 | Moving one bus leaves the other and the master alone, and the row's label follows | **PASS** | EditMode `Settings_move_each_bus_on_its_own_and_persist_both` |
| 5 | Both levels persist | **PASS** | same EditMode test reloads through a fresh `SettingsService`; browser spec polls `__projectSurvivePersistence` |
| 6 | The audio device plays at the new level, not at its own hard-coded one | **PASS** | `AudioDirector.SetMusicLevel/SetSfxLevel`; browser spec compares `__projectSurviveAudio.musicVolume` with the panel's |
| 7 | The panel can be opened, pressed and closed through the real UI in the built player, and its rows are reachable | **PASS** | `tests/web/title.spec.ts` — 10 published controls, click through the report's own points, close hides them again |
| 8 | No player-facing text is hard-coded, and every label fits | **PASS** | four new localization rows; `Title: 11 labels fit, 10 of them Thai, 0 missing glyphs` in the player |
| 9 | The panel does not let the screen behind it show through, and its controls say what they do | **PASS** | [screenshot](../artifacts/stage13c-2a/web-title-settings.png) — opaque surface, `-`/`+` legible, `เพลง 45% / เอฟเฟกต์ 90%` after the spec's presses |
| 10 | PlayMode (the pause-adjacent runner changes and the new device levels) | **NOT RUN** | The Editor bridge is still unavailable this session; a batch-mode PlayMode run records no tests. See §3. |

## 3. What was not tested, and the risks

- **PlayMode did not run again.** The same blocker as 13c-1: the MCP Editor bridge is gone, and
  `-runTests -testPlatform PlayMode` in batch mode writes a results file with `total: 0`. The
  EditMode suite and the built player cover this stage's behaviour, but a PlayMode re-run is still
  owed before 13c is closed.
- **The mix was not listened to.** The numbers are proven to reach the device; whether 45 % music
  against 90 % effects sounds right is a human judgement, and part of the Stage 13 playtest that
  remains **NOT RUN**.
- **Two visual defects were found only by looking.** The capture is why the stage has a screenshot
  step at all: nothing asserted panel opacity, and no test can say a blank button is unreadable.
  Both are now fixed in the builder, not by editing the scene file.
- **Schema 1 files are the only older ones handled.** There is no migration path for schema 0
  because no schema 0 was ever written (`SettingsService` shipped its version with the first save).
- **The panel grew 220 px.** At the 1080 reference height it is 76 % of the screen; it still fits at
  960×600 (410 of 600 px), which is the size the browser fixture uses.

## 4. Commands actually run

```powershell
tools/gen.ps1                                   # tables + localization, configHash FFA6F44F…
Unity.exe -batchmode -executeMethod ProjectSurvive.Game.Editor.TitleSceneBuilder.Create
Unity.exe -batchmode -runTests -testPlatform EditMode -testResults artifacts/stage13c-2a-editmode.xml
dotnet test lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj          # 603/603
tools/build-web.ps1 -Force                     # Development, 0 C# errors, 122 266 548 B
node tools/serve-web.mjs
cd tests/web; npx playwright test              # 17 passed, 13 skipped, 0 failed (3.8 m)
tools/verify-wiki.ps1                          # 5 PASS
```
