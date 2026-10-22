# Stage 13c-1 — sound

## 1. Summary

The game made no sound before this stage. `tools/gen-audio.py` now synthesises all 13 placeholder
clips (11 effects, two six-second loops) from its own oscillators, so the audio is project-owned and
reproducible rather than downloaded. `AudioDirector` sits on the App scope, owns eight pooled voices
and one music source, and is the game's only `AudioListener`; presenters ask for a cue on an R3
channel only after a command succeeded, and battle cues arrive on the event channel Stage 08 reserved.
The browser reads `__projectSurviveAudio` (plays, voice stealing, throttling, music state) and two
Playwright cases prove the arena and the camp each play their own track. The 20-visit memory test
caught the music leaking a decoder buffer per `Play`, which the import settings now avoid.

## 2. Acceptance

| # | Item | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Every cue is a project-owned, reproducible clip — 13 of them, mono, 22 050 Hz, non-silent, effects < 1.2 s, music ≥ 3 s | **PASS** | `AudioAssetTests` 7/7 (EditMode); `tools/gen-audio.py --check`; [`ATTRIBUTION.md`](../ATTRIBUTION.md) audio row |
| 2 | A cue is only asked for by a command that succeeded (never by a refusal-shaped preview, never by a view) | **PASS** | `CampPresenterTests.The_screen_asks_for_a_sound_only_when_a_command_landed`; `CampPresenter.Cue` call sites |
| 3 | One audio device: preallocated voices, no `Instantiate`/`PlayClipAtPoint` per cue, one `AudioListener` in the whole app | **PASS** | `AudioDirector.CreateVoices`; report `voices: 8`, `missingClips: 0` in the browser run |
| 4 | Music follows the scene and survives a transition instead of restarting | **PASS** | `audio.spec.ts` — `MusicBattle` in the arena, `MusicCamp` in the camp, `musicPlaying: true` after a gesture |
| 5 | Web autoplay respected: nothing plays before a user gesture, and Boot is silent by design | **PASS** | `AudioDirector.OnSceneLoaded` (no Boot case); the browser spec clicks the canvas before reading the report |
| 6 | Import settings are the ones the Web build needs | **PASS** | `AudioAssetTests.TheImportSettingsAreTheOnesTheWebBuildNeeds` (load type, format, mono, preserved rate) |
| 7 | The player reports no console error or warning it did not already have, and a source never plays with no listener | **PASS** | every web spec asserts its collected `console.error`/`pageerror` list is empty; `audio.spec.ts` also fails on the "no audio listeners" warning, and the rebuilt player produced none |
| 8 | Attribution records the audio as project-owned | **PASS** | [`ATTRIBUTION.md`](../ATTRIBUTION.md) player-facing assets table |
| 9 | The 20-visit memory budget holds with audio live (the `CompressedInMemory` leak) | **NOT RUN on the final tree** | `SceneFlowPlayModeTests` owns this assertion and was green per class earlier in the session. Re-running PlayMode after the last two edits needs the Editor bridge, which this session lost — see §3. |
| 10 | A human actually hears the sound | **NOT RUN** | Not automatable. The browser test proves the source started and the clip was found, not that a headphone produced it. `artifacts/stage13c-1/battle-audio.png` is the arena at the moment of the assertion, not a listening test. |

## 3. What was not tested, and the risks

- **Nobody listened.** Tonal balance, loudness between the two tracks and the 35 ms retrigger
  throttle are choices made from the numbers and the generator's own output, not from hearing it. A
  real listen is part of the Stage 13 human pass, which is still **NOT RUN**.
- **A compile break that no test could have caught was caught late.** `CampSceneDirector` needed
  `using R3;` for the cue subscription, which made `Result` ambiguous between `R3.Result` and
  `ProjectSurvive.Core.Shared.Result` — the working copy did not compile until the final batch-mode
  build. It is fixed by fully qualifying the type, and that build is the evidence.
- **Voice stealing is counted, not judged.** Eight voices were chosen as enough for a placeholder
  mix; `stolen` says how often a burst took a playing voice, and the browser run printed it — a
  saturated mix is visible, not fixed.
- **The browser proves a started source.** A WebGL player has no audible device under Playwright, and
  `AudioContext` stays suspended until the page is clicked; the spec clicks first for that reason.
- **Pause and the audio settings panel are 13c-2.** The master volume already exists from Stage 12
  (`AudioListener.volume`, written by `SettingsPresenter` and `GameSave`); `AudioDirector` applies
  only its own music/effects levels so the master is not squared.
- **PlayMode did not run on the final tree.** The MCP Editor bridge that drove `run_tests` all
  session dropped when the Editor was restarted for the post-build checks and never reconnected; a
  batch-mode `-runTests -testPlatform PlayMode` run in this project records **zero** tests (its XML
  reports `total: 0` while the scene loads it did run are visible in the log), so it is not a
  substitute. The PlayMode classes were green class-by-class earlier in the session, before the two
  final edits, both of which EditMode and the WebGL player do cover. **This stage is therefore not
  closed as PASS.**
- **`no audio listeners` is a batch-mode artefact, not a player defect.** The batch-mode PlayMode
  run logged it whenever a source played (no audio device in batch mode). The rebuilt WebGL player
  logs it neither as an error nor as a warning — `audio.spec.ts` now fails on that string, so the
  claim is tested rather than assumed — and the reminder is why `AudioDirector` adds the listener on
  its own persisted object instead of accepting one found in a scene.
- **One test-harness fix rode along**: `ShellPlayModeTests`' Boot test now clears the persisted
  checkpoint, because a run left by an earlier PlayMode fixture made Boot resume into the camp and
  the assertion looked at the wrong scene.

## 4. Commands actually run

```powershell
python tools/gen-audio.py                       # 13 clips, 688 KB of source WAV
python tools/gen-audio.py --check               # committed bytes still match the script
dotnet test lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj   # 601/601
powershell -File tools/verify-wiki.ps1          # 5 PASS, 0 FAIL
Unity.exe -batchmode -runTests -testPlatform EditMode -testResults artifacts/stage13c-1/editmode.xml
powershell -File tools/build-web.ps1 -Force     # Development, 0 C# errors, 122 232 529 bytes
node tools/serve-web.mjs                        # http://127.0.0.1:8080
cd tests/web; npx playwright test               # 16 passed, 13 skipped, 0 failed (3.4 m)
Unity.exe -batchmode -runTests -testPlatform PlayMode ...   # ran, recorded 0 tests — see §3
```

Before those, the Unity tests were driven through the Editor session (`run_tests`): EditMode
191→193, PlayMode green class by class (the bulk run is the known ambient flake of
`docs/testing/test-suites.md` §7). The final EditMode run is the batch-mode XML above.
