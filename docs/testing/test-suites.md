# Test suites — Fast, Full, and every test's home

`tools/test.ps1` runs one of two suites. This file is the contract between the two: which test lives
where, what each step really costs on this machine, and what has to be missing before a step is
allowed to report NOT RUN.

## 0. Why the split

Every Stage from 07 to 09d paid `dotnet` + Unity EditMode + PlayMode + a WebGL build + Playwright for
every check of every change, and then wrote it up. The cost was in the *build* and the *browser*: a
WebGL Development build measured 258 s on 2026-10-15 (Unity's own `buildSeconds`) and the browser suite
another 2 min, none of which can see a change in a rule engine that `dotnet test` answers in 2.3 s.
Nothing about the checks changed here — the same tests run — they are now divided by how often they
need to run, and the Web build moved behind the Full suite.

## 1. Which suite runs what

| Step | Suite | What it covers |
| --- | --- | --- |
| `toolchain: .NET SDK` | Fast | an SDK ≥ 8 is present |
| `dotnet build` | Fast | every `lib/` layer, the xUnit project and the Sim compile |
| `dotnet test (xUnit)` | Fast | 425 tests: the rule engine, the architecture guards, the contract suites, the Sim |
| `sim: determinism` | Fast | the same seed prints the same `determinismDigest` twice, a different seed does not |
| `sim: stream separation` | Fast | shop / battle / visual digests differ, `ticksProduced=300` |
| `sim: bot smoke run` | Fast | the kiting bot plays 2 seeds and writes balance rows |
| `data: generated tables validate` | Fast | `tools/validate-data.ps1` over the real tables |
| `data: generated code reproducible` | Fast | `tools/verify-generated.ps1` — `data/` + `proto/` reproduce the tracked output |
| `data: broken fixtures rejected` | Fast | `tools/check-fixtures.ps1` — 17 broken data sets rejected for the right reason |
| `unity: EditMode` | Fast | 65 tests in `ProjectSurvive.Game.Tests.EditMode` |
| `unity: PlayMode` | **Full** | 24 tests in `ProjectSurvive.Game.Tests.PlayMode` (scenes, pooling, pointer drags, walking) |
| `web: WebGL build (Development)` | **Full** | `tools/build-web.ps1 -Configuration Development -Force` |
| `web: Playwright (smoke + battle + camp)` | **Full** | 8 browser tests against that build, in Chrome |

`-Release` moves the dotnet build and the WebGL build to Release. It is for a Stage gate or a
measurement — a Development build's numbers may not be quoted ([ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md)).

Commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1              # Fast (default)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full  # Full
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full -Release
```

Rules around the two suites, from `AGENTS.md`: the working loop is Fast; Full is for closing a Stage
or committing one; a Stage's own gate (stages 10, 12, 14, 15) may name Full with `-Release`. A layout
change is not a reason to build Web — `ProjectSurvive/Tools/1. Capture UI` renders the scene in the
Editor at both sizes instead (§6).

## 2. Measured timings

Reference machine (the one [ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md) names,
re-quoted here for these runs): Intel Core i5-14400F (16 logical cores), 31.8 GiB RAM, Windows 11 Home
10.0.26300, NVIDIA GeForce RTX 5060 Ti, WD Blue SN5000 1 TB NVMe SSD. Unity 6000.3.9f1 (locked),
`dotnet` SDK 9.0.311, Node/`@playwright/test` 1.64.0 on the Chrome channel.

**Fast**, 2026-10-15, `artifacts/proc10/fast-run1.log` (cold: first run in a fresh shell and after a
clean `lib/` output tree) and `artifacts/proc10/fast-run2.log` (warm, immediately after):

| Step | Cold (s) | Warm (s) |
| --- | --- | --- |
| `toolchain: .NET SDK` | 0.1 | 0.0 |
| `dotnet build` | 10.2 | 1.4 |
| `dotnet test (xUnit)` | 2.7 | 2.5 |
| `sim: determinism` | 36.5 | 1.3 |
| `sim: stream separation` | 0.1 | 0.1 |
| `sim: bot smoke run` | 49.0 | 2.6 |
| `data: generated tables validate` | 62.7 | 3.9 |
| `data: generated code reproducible` | 4.7 | 3.4 |
| `data: broken fixtures rejected` | 52.3 | 48.2 |
| `unity: EditMode` | 10.8 | 13.6 |
| **total of steps** | **229.1** | **77.0** |

The cold column is what a machine pays once per boot (first JIT, first assembly load, first file read
of the tables); the warm column is the loop a developer actually feels. Nothing in the cold column is
a check doing more work — the same 17 fixtures are rejected in both.

**Full only** (2026-10-15, Editor closed, `artifacts/proc10/full-editor-closed.log` and the
`test-summary.md` it wrote):

| Step | Seconds | Note |
| --- | --- | --- |
| `unity: PlayMode` | 42.7 | 24 tests; **12 failed** in this run — see §7 |
| `web: WebGL build (Development)` | 269.3 cold / **16.5** warm | cold: a fresh IL2CPP translation (Unity's own `buildSeconds: 258.3`, 119,647,484 B, 5 scenes). warm: the second build minutes later reused the IL2CPP C++ cache (Unity: `buildSeconds: 6.7`, 119,647,478 B) |
| `web: Playwright (smoke + battle + camp)` | 121.4 | 8 tests, 1 worker, Chrome channel |

`unity: EditMode` measured 9.2–13.6 s across four runs and `dotnet test` 2.2–2.7 s; the numbers above
are the runs of record, not averages.

## 3. Every test, and the suite it belongs to

Nothing was dropped when the suites were split: this table lists every test that existed before the
split and where it runs now. "Fast" means the working loop, "Full" the closing one; a test cannot be in
neither.

### dotnet — `lib/Tests~/ProjectSurvive.Core.sln` (425 tests) → **Fast**

Run by `dotnet test` in the `dotnet test (xUnit)` step. The project is the *same source* as the `lib/`
asmdefs ([ADR 0004](../adr/0004-dotnet-layer-enforcement.md)), so these are the rule-engine tests.

| Where | What it covers |
| --- | --- |
| `ArchitectureTests.cs` | layer references, `Domain`/`Simulation` free of Unity/R3/VContainer/serializers |
| `RepoLayout.cs` | the repo's own layout contract |
| `Xoshiro128Tests.cs`, `RandomStreamsTests.cs` | the RNG and the three streams |
| `FixedTickerTests.cs`, `ResultTests.cs`, `InfrastructureTests.cs` | ticker, `Result`/`Error`, adapters |
| `ConfigLoadTests.cs`, `ConfigRulesTests.cs`, `ConfigEnumParityTests.cs`, `ConfigFixture.cs` | the generated tables, their rules and pinned counts |
| `LocalizationServiceTests.cs` | localization lookup |
| `ProbeSerializationTests.cs`, `EcsLiteSmokeTests.cs` | MessagePack/protobuf round trips, the vendored ECS |
| `Battle/*.cs` | the battle contract suite over the fake and the real ECS session, the frame buffer, the event ring |
| `Inventory/*.cs` | Board/Backpack/BackpackEditor/Merge/Recycle/Rotation/Synergy/Loadout + the randomized bag tests |
| `Run/*.cs` | RunSession economy and flow, shop stock, the drop preview, replay |

### Unity EditMode — `ProjectSurvive.Game.Tests.EditMode` (65 tests) → **Fast**

| Class | Tests | What it covers |
| --- | --- | --- |
| `ShellSmokeTests` | 12 | the 7-probe smoke suite, config hash, R3/UniTask/serialization in the Editor |
| `BattlePresentationTests` | 15 | the battle HUD/state/presenter seam without a scene |
| `CampPresenterTests` | 9 | every camp command through the presenter, the preview/command agreement, the 0-byte drag |
| `RunFlowEditModeTests` | 4 | the run flow against the shipped `Resources` tables |
| `WebSizeProfilesTests` | 8 | the size profiles and their recorded configuration |
| `BackpackRulesTests` | 9 | the bag rules as the Editor sees them |
| `UiLayoutCheckTests` | 8 | the overlap rule the UI capture command applies, including the deliberate-overlap case |

### Unity PlayMode — `ProjectSurvive.Game.Tests.PlayMode` (24 tests) → **Full**

| Class | Tests | What it covers |
| --- | --- | --- |
| `BattlePlayModeTests` | 4 | the battle scene starts, ticks, re-enters and disposes |
| `CampBagDragPlayModeTests` | 8 | the จัดย่าม screen driven by real pointer events |
| `CampWorldPlayModeTests` | 8 | WASD walking, the walk clamp, `E` at a shelf, clicking without walking |
| `ShellPlayModeTests` | 4 | scene loading, the title, UniTask cancellation in play mode |

### Playwright — `tests/web` (8 gate tests) → **Full**

| Spec | Tests | What it covers |
| --- | --- | --- |
| `smoke.spec.ts` | 2 | the player boots, passes its own 7-probe self-check, hands over to Title; the Thai spike's glyph coverage |
| `battle.spec.ts` | 2 | floor 1 played to a win or a loss in a real browser; a mid-floor report |
| `camp.spec.ts` | 4 | the จัดย่าม flow end-to-end, the sala's controls, WASD + `E`, the layout at 1280×720 and 1920×1080 |
| `screens.spec.ts` | 13 | **not a gate**: the screenshot generator, opt-in with `SCREENS=1` (`test.skip(process.env.SCREENS !== "1")`), skipped in a plain run |

### Process-level checks inside the harness → **Fast**

`sim: determinism`, `sim: stream separation` and `sim: bot smoke run` are not test-framework tests: they
run `ProjectSurvive.Sim` as a process and compare its output, which is the only place determinism across
*processes* is observable at all. They stay in Fast because they are cheap (52.3 s cold, 4.0 s warm).

## 4. NOT RUN, and what makes it happen

NOT RUN is never a pass and never silent: `artifacts/test-summary.md` carries the reason per step.

| Situation | Steps affected | Reported |
| --- | --- | --- |
| No Editor connected to this project (`unity status` has no row for `UnityProject`) | `unity: EditMode`, `unity: PlayMode` | NOT RUN with the connection requirement |
| A Unity Editor holds the project lock | `web: WebGL build`, `web: Playwright` | NOT RUN: batch mode cannot share a project |
| `-Suite Fast` | `unity: PlayMode` and both web steps | NOT RUN: "the Full suite was not requested" |
| The build step failed or reported NOT RUN | `web: Playwright` | NOT RUN: there is no player to test |
| A Unity test run produced no result within `-UnityTimeoutSeconds` (default 900) | that Unity step | NOT RUN with the timeout named |
| `node`/`npx` missing, or `tests/web/node_modules` absent | `web: Playwright` | NOT RUN with the missing prerequisite |
| `tools/build-web.ps1` exits 2 (no Editor install, locked project) | `web: WebGL build` | NOT RUN with the script's own error line |

Because the Unity steps need the Editor open and the WebGL build needs it closed, **one Full run cannot
report all three of PlayMode, the build and Playwright.** Two runs do, and both write their own
`test-summary.md`; keep them apart by copying the file (the run of record for this change is
`artifacts/proc10/full-editor-open-summary.md` and `…-closed-summary.md`).

## 5. The slow steps, and the honest way to shorten them

Measured above, in order of what they cost:

1. **`web: WebGL build` — 269 s cold, 16.5 s warm.** The cost is IL2CPP's C++ translation, not the
   build itself: the second Development build of the same code, minutes later in the same cache,
   finished in 16.5 s. Nothing in the working loop needs the player at all, which is why the step is in
   Full; a Release-with-`OptimizeSize` profile is a *different* configuration (not a like-for-like
   swap) and is what `-SizeProfile` builds are for.
2. **`data: broken fixtures rejected` — 48.2 s warm, 52.3 s cold.** The step is stable but expensive:
   it validates 17 broken fixture sets, one process each, so it is 17 × (process start + table load).
   A single process that walks all 17 would remove most of it — the tool already has the loop; it
   spawns per fixture for isolation. This is the first thing to fix if Fast must get shorter.
3. **`sim: bot smoke run` and `sim: determinism` — 49.0 s / 36.5 s cold, 2.6 s / 1.3 s warm.** Cold-only
   cost (JIT + first config load); nothing to do.
4. **`unity: EditMode` — 9–14 s.** Already a bridge call into the running Editor, no batch spawn.
5. **`unity: PlayMode` — 42.7 s** for 24 scene tests. Fine.
6. **`web: Playwright` — 121.4 s** for 8 tests, one worker (2.0 min of browser time, several player
   boots). Parallel workers would break the single-player-at-a-time assumption of the server, and the
   tests drive one canvas.

Cold total for Fast is 229 s and warm is 77 s, so "a few minutes" holds for both.

## 6. The Editor UI capture (no WebGL build)

`ProjectSurvive/Tools/1. Capture UI (Camp + Battle, 1920x1080 + 1280x720)` — or
`-executeMethod ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit` — opens Camp and Battle in
the Editor, renders each at both sizes into `artifacts/ui/`, measures every named panel and fails when
two that must stand apart share pixels. Measured: 0.9–1.1 s for six images through the menu item, and
9 s wall clock for the batch `-executeMethod` form (the Unity launch dominates) — both were run, and
they produced **byte-identical images** and the same rectangles.

- Coverage: `artifacts/ui/{camp,battle}-{1920x1080,1280x720}.png` plus the closed camp
  (`camp-closed-*`) and `ui-report.md` / `ui-report.json` with every rectangle in pixels.
- The rectangles are the screen's own: at 1280×720 the board measures
  `{x: 26.667, y: 213.333, width: 493.333, height: 386.667}`, which is what the browser reported for the
  same screen in Stage 09 — the Editor shot and the player agree.
- What the images do **not** contain: text. Every label in `Camp.unity` is empty until the runtime fills
  it from the localization tables, and the board's cells are built at runtime; `ui-report.md` says so,
  and the PlayMode suite is where that runtime layout is measured.
- Proof the check can fail: `ProjectSurvive/Tools/2. Capture UI self-test (expected to FAIL)` runs the
  same command with one invented region dropped on the board and writes `ui-report-selftest.md`, which
  reported 4 failures (the pair named at both sizes). Its rule is also unit-tested 8 ways
  (`UiLayoutCheckTests`, in Fast).

## 7. Known flake: a bulk PlayMode run

Twelve of the 24 PlayMode tests failed in every bulk run measured on 2026-10-15 (`unity: PlayMode
12/24`), with **no failed assertion**: the failures are Unity's own ambient-message log asserts
(`[Exception] IndexOutOfRangeException` inside `InputSystem.InputManagerStateMonitors` via
`InputSystemUIInputModule.OnEnable`, and `[Error] Cached unprocessed value unexpectedly became
outdated`). The same tests pass individually and Stage 09 recorded 24/24 on the same code — this is the
unsolved item in [`llmwiki/stage-log.md`](../../llmwiki/stage-log.md) ("The bulk PlayMode suite fails on
ambient Input System log-asserts").

The harness reports it as **FAIL**, because that is what it is: the suite, run as a suite, does not pass.
Do not read it as a pass, and do not read it as a regression in whatever was just changed either — check
the artifact (`artifacts/dotnet/unity-playmode-status.json`, which carries every test's status, duration
and message) and, for a specific class, run it on its own:

```powershell
unity command run_tests --mode playmode --async_tests --filter CampBagDragPlayModeTests
unity command test_status --result-only
```

The same note also records that `run_tests` can answer with the **previous** run's payload (identical
per-test durations). The harness guards against it: it polls `test_status` and only accepts a result that
either reported `running` first or differs from the payload captured before the submit, and it writes the
raw payload to `artifacts/dotnet/unity-<mode>-status.json` so the durations can be checked.
