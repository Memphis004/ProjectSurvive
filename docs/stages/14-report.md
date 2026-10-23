# Stage 14 — Automated verification and CI

## Summary

The verification story is now two commands: `tools/test.ps1 -Suite Fast|Full` is the working loop, and
`tools/ci.ps1` is the gate (`Validate → Test → Sim → Unity → Build → Web`, licence jobs kept strictly
one Unity process at a time). A development-only harness (`HarnessBridge`) lets a browser test start a
run from a named seed and advance a floor by a known tick count **through the same use cases the
screens use**, and it is compiled out of a Release player — `tests/web/release.spec.ts` reads that
absence out of the build that would ship. Playwright now waits for each screen's published handshake
instead of sleeping, and every spec fails on a `console.error` or a `pageerror`.

The PlayMode suite's 20-visit heap case failed in every environment at the time (5.07 MB against a
3 MB limit) and a bulk run still dies on Unity's ambient log asserts. Both are classified below with
evidence, not waved past — and the heap case turned out not to be a leak at all: it was **fixed on
2026-10-23** by authoring the game's `AudioListener` into the Boot scene instead of adding it at
runtime (the game had been silent, and Unity's per-frame "no audio listeners" warning — kept by the
PlayMode runner — was what the case measured).

## Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `ci.ps1` exits ≠ 0 when a required check fails | **PASS** | A `sim` run forced outside its band exited **1** with `RESULT: FAILED` ([log](../../artifacts/stage14/ci-fail-path.log), [summary](../../artifacts/stage14/ci-fail-summary.md)); a blocked gate (port 8080 held by a stale server) exited **2** with `RESULT: BLOCKED`, never as a pass |
| 2 | The report separates PASS / FAIL / NOT RUN | **PASS** | `summary.md` counts and lists the three separately ([Fast](../../artifacts/test-summary.md) `PASS 13 / FAIL 0 / NOT RUN 3`; [failure run](../../artifacts/stage14/ci-fail-summary.md) `PASS 0 / FAIL 1 / NOT RUN 0`; [release web](../../artifacts/stage14/ci-web-release-summary.md) `PASS 1 / FAIL 0 / NOT RUN 0`) |
| 3 | A browser test does not pass merely because a canvas exists | **PASS** | Every spec waits on a document the screen publishes (`__projectSurviveSmoke`, `__projectSurviveScene`, `__projectSurviveBattle`…), asserts its content, and fails on `console.error`/`pageerror`; `release.spec.ts` first proves the player booted, so "no harness global" cannot be true of a blank page ([spec](../../tests/web/release.spec.ts)) |

### The seven work items

| Item | Verdict | Where |
| --- | --- | --- |
| 1 `test.ps1` composes dotnet / EditMode / PlayMode / Web and summarizes | **PASS** | Fast suite: 13 steps PASS, 3 Full-only steps reported NOT RUN with the reason, `artifacts/test-summary.md` + `.json` |
| 2 Development-only WebBridge: ready handshake, seeded start, snapshot, test input, tick advance, diagnostics, all through real use cases | **PASS** | `HarnessBridge.cs` (`HarnessProtocol` opcodes 1–4), `BattleSimulationRunner.Harness.cs`, `RunScopeOwner.Harness.cs`; **no bypass command exists**, so nothing here counts as combat coverage |
| 3 Playwright: smoke, persistence, run-loop; handshake not sleeps; console errors; bridge tests kept apart | **PASS** | `tests/web/harness.spec.ts` is the only bridge-driven file (3 cases: a named seed starts the real run, N ticks play a floor, a command it does not own is refused); the other eight specs press drawn controls |
| 4 screenshots, traces, logs, XML/JSON and build metadata under `artifacts/` | **PASS** | `artifacts/ci/`: `summary.md|json`, per-step logs, `editmode.xml`, `playmode-<fixture>.xml`, `ci-tests.trx`, `balance-50.csv`, `build-info.json`, `playwright.json`, `web-test-results/`, `web-report/` |
| 5 `ci.ps1` = gen → validate → verify-generated → test → sim (50 seeds, band) → build → web | **PASS** | [Validate](../../artifacts/stage14/ci-sim-50.log), [Web release](../../artifacts/stage14/ci-web-release.log); sim refused to leave a win rate outside 0.30–0.60 |
| 6 One Unity process per project; licence jobs apart from pure validation | **PASS** | `-Job Validate` needs no licence; `Unity`/`Build`/`Web` refuse to start while an Editor is connected or `Temp/UnityLockfile` exists (observed: exit 2), and remove the lockfile only when no `Unity` process is alive |
| 7 A Release build has no harness API | **PASS** | `release.spec.ts`: `__projectSurviveHarness`, `…Send`, `…Snapshot` and `__projectSurviveDevMenu` are all `undefined` in the shipped artefacts ([result](../../artifacts/stage14/playwright-release.json), [build metadata](../../artifacts/stage14/build-info-release.json) `development: false`) |

## Measured results

| Suite | Result |
| --- | --- |
| xUnit (`dotnet test`) | **604 / 604** |
| `contract: FakeBattleSession`, `contract: BattleSimulation` | **24 / 24** each |
| EditMode | **205 / 205** (17.8 s step) |
| Sim, 50 seeds | **22 / 50 boss wins = 44.0 %** inside 0.30–0.60, 250 floor rows |
| WebGL, Release | `development: false`, 66,043,566 B, 338.9 s |
| WebGL, Development | `development: true`, 122,308,949 B, 384.4 s |
| Playwright, Release | **19 passed / 1 skipped / 0 failed** of 20 ([json](../../artifacts/stage14/playwright-release.json)); the skipped case is `result.spec.ts`'s lost-floor run, which needs the dev menu a Release build must not have |
| Playwright, Development | **21 passed / 0 failed** — the same 19 UI cases (the lost-floor one included) plus `harness.spec.ts`'s 3 ([json](../../artifacts/stage14/playwright-harness.json), [summary](../../artifacts/stage14/ci-dev-summary.md)) |
| Firefox | The Release player boots in Playwright's bundled Firefox (`ms-playwright/firefox-1555`; no system Firefox is installed): `WebGL 2.0`, smoke passed, 0 errors, harness `undefined` — [probe](../../artifacts/stage14/firefox-probe.json). The full suite is still Chrome-only |

### PlayMode, one fixture per process ([details](../../artifacts/stage14/unity-playmode-per-class.md))

| Fixture | Batch | Open Editor |
| --- | --- | --- |
| Battle / MergeChooser / ShelfTooltip / CampWorld / ResultScene / TitleScene | 6+4+5+9+3+5 — all pass | not re-run |
| CampBagDrag | 24 / 25 — the layout case reads a 640×480 Game View | **25 / 25** |
| CampSynergyLine | 8 / 9 — same cause | **9 / 9** |
| SceneFlow | **4 / 5** then — 20-visit heap case, 17,764,352 B batch / 5,066,752 B open, limit 3,145,728 B; **6 / 6** after the listener fix (2026-10-23, 49.8 s) | **4 / 5** then, **6 / 6** now |
| Shell | 0 cases reported (the framework quirk §7 has recorded since Stage 10) | 0 |

The heap case is **not** this stage's: with the harness install commented out of `SceneBoot` the heap
still grew by 5,865,472 B. It belongs to the stage that owns the camp/battle scene lifetimes.

### The heap case, resolved (2026-10-23)

There was no scene-lifetime leak left to find: the camp/battle loop already left one scope and one run
scope per visit, and the session was disposed. The heap was growing from **log lines the runner keeps**:

- The game had **no `AudioListener`**. `AudioDirector` added one from `Awake`; the component never
  appeared, so Unity logged "There are no audio listeners in the scene" once per frame for every
  playing source — ~1,750 retained lines per camp/battle visit, i.e. a megabyte of strings.
- `WebGlBridge.Publish` echoed every report document to the console. A camp or battle report is
  kilobytes and republishes on a timer.

The listener is now authored into the Boot scene on the `Audio` child of the persisting app root
(`BootSceneBuilder`), so it exists before the first frame and travels into `DontDestroyOnLoad`;
`Publish` logs `"<global> (<n> chars)"` instead of the document (the smoke report is still logged in
full — one small document per session). The fixture gained a direct case,
`The_game_has_one_audio_listener_that_survives_every_scene_change` (exactly one enabled listener, on
the audio device's own object, at the camp, in the arena and at the title), so the cause has its own
test rather than only a byte budget. Measured in the open Editor: `SceneFlowPlayModeTests` **6/6** in
49.9 s, the 20-visit case itself 41.9 s ([status](../../artifacts/stage14/sceneflow-6of6-status.json),
[Full log](../../artifacts/stage14/full-after-listener-fix.log)). Batch mode, which the first pass of this
fix could not reach, was measured after the web run below: `unity test UnityProject --mode PlayMode
--filter SceneFlowPlayModeTests` reports **6/6 in 46.0 s**, the 20-visit case 39.2 s, against a budget it
used to miss by 17.8 MB ([xml](../../artifacts/stage14/sceneflow-batch.xml)).

`-Suite Full` with the Editor open was then **PASS 21 | FAIL 2 | NOT RUN 2** in 527.3 s: the two NOT RUN
steps are the web ones (they refuse to share the project with a running Editor), and the two FAILs are
the camp fixtures' pre-existing pointer flake — `CampBagDragPlayModeTests` 24/25 and
`CampMergeChooserPlayModeTests` 3/4, one test each, `clicking จัดย่าม did not open the panel` with a
different victim per run. It reproduces at `HEAD` with this change stashed (24/25, same message;
[status](../../artifacts/stage14/head-campbagdrag-status.json)), so it was not a regression from this
fix — and it is now **closed** as well: the module drops every pointer while the Game View is unfocused
(`InputSystemUIInputModule.Process()`'s `!eventSystem.isFocused && !shouldIgnoreFocus` gate), which a run
started from a terminal always is. Both camp fixtures set
`InputSystem.settings.backgroundBehavior = IgnoreFocus` (restored in `TearDown`) and wait for
`IsPointerOverGameObject()` before clicking. Three runs of each fixture afterwards: **29/29 every run**
([drag](../../artifacts/stage14/pointer-fix-campbagdrag-status.json),
[chooser](../../artifacts/stage14/pointer-fix-campmergechooser-status.json)), and `-Suite Full` with the
Editor open is **PASS 23 | FAIL 0 | NOT RUN 2** in 526.6 s, exit 0
([log](../../artifacts/stage14/full-after-pointer-fix.log)).

## Not tested, and risks

- **`tools/test.ps1 -Suite Full` as one command: NOT RUN at this stage.** Its PlayMode step was the bulk
  run the ambient-log flake kills (76 discovered, 0 completed), and its Web step would overwrite the
  Release player with a Development one. The same ground was covered by `ci.ps1 -Job Unity` (per
  fixture), `-Job Build -Configuration Release` and `-Job Web -Configuration Release`.
  *Follow-up, 2026-10-23 — the first half is fixed:* the step now runs **one fixture per Editor test run**
  (`run_tests --filter <Class>`, §7 of [the suite doc](../testing/test-suites.md)), so the flake can no
  longer hide the suite's verdict. `-Suite Full` with the Editor open measured **PASS 22 | FAIL 1 |
  NOT RUN 2** in 525.2 s, exit 1, and the single FAIL is by name
  `SceneFlowPlayModeTests.Twenty_camp_and_battle_visits_leave_nothing_behind_and_do_not_grow_the_heap`
  (74/75 across ten fixtures) — the scene-lifetime leak below, not the flake
  ([log](../../artifacts/stage14/full-per-fixture.log),
  [per-fixture table](../../artifacts/dotnet/unity-playmode-per-fixture.md)). The leak that FAIL named is
  **closed** (see “The heap case, resolved”), the camp pointer flake is closed too
  ([gotcha](../../llmwiki/gotchas/ui-pointer-events-need-focus.md)), and `-Suite Full` with the Editor  open is **PASS 23 | FAIL 0 | NOT RUN 2** in 526.6 s, exit 0.

### The web half, with the Editor closed (2026-10-23)

`tools/test.ps1 -Suite Full` with no Editor connected: **PASS 13 | FAIL 0 | NOT RUN 2** in 550.7 s
([log](../../artifacts/stage14/full-editor-closed.log)). The web half of that run is green on the final
code — `web: WebGL build (Development)` **PASS** in 304.7 s (a rebuilt `UnityProject/Builds/Web`) — and
the two NOT RUN steps are the Unity ones, which need the Editor (`unity: EditMode` has no batch path of
its own, so it reports "no Unity Editor with this project is connected").

Its Playwright step reported NOT RUN because a stray `python` process had been holding port 8080 since
an earlier session, which `tools/serve-web.mjs` cannot bind. With that released, the same nine specs
run by hand against that build: **21 passed / 0 failed in 4.0 min**, `audio.spec.ts` and
`harness.spec.ts` included ([log](../../artifacts/stage14/playwright-editor-closed.log),
[json](../../artifacts/web/playwright-editor-closed.json)). The suite's own guard — a blocked gate is
reported as NOT RUN, never as a pass — is what made the stale port visible instead of silent.
 The Release/Playwright half still needs
  a run with the Editor closed.
- **PlayMode batch mode**: the two layout cases need a 1080p-or-larger Game View, which `unity test`
  cannot be given. They pass in the open Editor, which is where all earlier stages measured them.
- **Firefox full suite: NOT RUN** — installed and smoke-probed only.
- The harness answers commands from `Update()`; it never drives a frame the game would not have
  rendered, so a browser test cannot outrun the runner's own clock (that is why `advanceTicks` steps
  the session, not `Time`).
- Risk: the harness shares `SceneBoot`'s boot path, so a compile-gate mistake would be invisible until
  the Release browser gate. `HarnessBridgeTests` pins the `#if` and the opcodes in EditMode.

## Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Sim -Seeds 2 -MinBossWinRate 0.95 -MaxBossWinRate 0.99   # failure path, exit 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Sim
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Build,Web -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Build,Web -Configuration Development
unity test UnityProject --mode PlayMode --filter <each *PlayModeTests class>
unity command run_tests --mode editor --filter HarnessBridgeTests
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full   # after the listener fix
```

## Outcome

**DONE, gate 14 green on Chrome + Release.** The WebGL/Playwright gate returned 0. The PlayMode heap
failure that outlived the first pass was not a scene-lifetime leak: it was a missing `AudioListener`
whose per-frame warning the runner kept in memory, plus whole report documents echoed to the console.
Both are fixed, the listener has its own PlayMode case, and `SceneFlowPlayModeTests` is **6/6** in the
open Editor and in batch ([log](../../artifacts/stage14/full-after-listener-fix.log),
[xml](../../artifacts/stage14/sceneflow-batch.xml)). The camp fixtures' pointer flake is closed too —
it was the UI module's focus gate, not a given-up hook
([gotcha](../../llmwiki/gotchas/ui-pointer-events-need-focus.md)) — so `-Suite Full` with the Editor open
is **23 PASS / 0 FAIL / 2 NOT RUN**, and the web half of the same suite with the Editor closed is a green
Development build plus **21/21** Playwright cases. The bulk-run ambient-log flake is the one item still
unchanged, and it is worked around by running one fixture per test run (§7 of the suite doc).
