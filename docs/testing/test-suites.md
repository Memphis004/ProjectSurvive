# Test suites — Fast, Full, and every test's home

`tools/test.ps1` runs one of two suites. This file is the contract between the two:
which test lives where, what each step costs on the reference machine, and what has
to be missing before a step may report NOT RUN.

## 0. Why the split

The build and the browser are the expensive steps: a WebGL build is tens of seconds
to minutes and a browser suite boots the player several times, while a rule-engine
test answers in seconds under `dotnet test`. The tests are therefore divided by how
often they need to run. Nothing is dropped — §3 lists every test and its suite.

## 1. Which suite runs what

| Step | Suite | What it covers |
| --- | --- | --- |
| `toolchain: .NET SDK` | Fast | an SDK ≥ 8 is present |
| `dotnet build` | Fast | every `lib/` layer, the xUnit project and the Sim compile |
| `dotnet test (xUnit)` | Fast | the shared primitives, the config tables, the data rules, the architecture guards, the run skeleton, the serialization probes |
| `sim: determinism` | Fast | the same seed prints the same `determinismDigest` twice, a different seed does not |
| `sim: stream separation` | Fast | run / encounter / visual digests differ |
| `data: generated tables validate` | Fast | `tools/validate-data.ps1` over the real tables |
| `data: generated code reproducible` | Fast | `tools/verify-generated.ps1` — `data/` + `proto/` reproduce the tracked output |
| `data: broken fixtures rejected` | Fast | `tools/check-fixtures.ps1` — six fixtures, each rejected for the reason its `fixture.json` names: `blank-text`, `missing-note-key` and `missing-language` by the validator, `out-of-range`, `missing-ref` and `duplicate-id` by Luban |
| `unity: EditMode` | Fast | `ProjectSurvive.Game.Tests.EditMode` |
| `unity: PlayMode` | **Full** | `ProjectSurvive.Game.Tests.PlayMode` — scenes, the scopes, the loop |
| `web: WebGL build (Development)` | **Full** | `tools/build-web.ps1 -Configuration Development` |
| `web: Playwright (smoke + title + loop)` | **Full** | the browser tests against that build, in Chrome |

`-Release` moves the dotnet build and the WebGL build to Release. It is for a Stage
gate or a measurement — a Development build's numbers may not be quoted
([ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md)).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1              # Fast (default)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full  # Full
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full -Release
```

Rules around the two suites, from `AGENTS.md`: the working loop is Fast; Full is for
closing a Stage or committing one. A UI layout change is checked from Play Mode
screenshots, not by building Web.

## 2. Measured timings

Reference machine ([ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md)):
Intel Core i5-14400F (16 logical cores), 31.8 GiB RAM, Windows 11 Home `10.0.26300`,
NVIDIA GeForce RTX 5060 Ti, WD Blue SN5000 1 TB NVMe SSD. Unity `6000.3.9f1` (locked),
`dotnet` SDK `9.0.311`, Node `v25.6.1`, `@playwright/test` `1.64.0` on the Chrome
channel.

**Full, 2026-10-18, Editor closed** (`artifacts/test-summary.md`): **PASS 10 | FAIL 0 |
NOT RUN 2 | WARN 0 — 139 s of steps**. The two NOT RUNs are the Unity steps, which
need a connected Editor.

| Step | Seconds |
| --- | --- |
| `toolchain: .NET SDK` | 0.0 |
| `dotnet build` | 1.5 |
| `dotnet test (xUnit)` | 1.7 — 124 passed |
| `sim: determinism` | 1.3 |
| `sim: stream separation` | 1.1 |
| `data: generated tables validate` | 5.3 |
| `data: generated code reproducible` | 10.5 |
| `data: broken fixtures rejected` | 19.2 — 6 fixtures |
| `unity: EditMode` | NOT RUN (no Editor connected) |
| `unity: PlayMode` | NOT RUN (no Editor connected) |
| `web: WebGL build (Development)` | 71.1 |
| `web: Playwright (smoke + title + loop)` | 25.0 — 4 passed in Chrome |

Fast is that same table without the two web steps: **≈ 41 s** of dotnet/Sim/data
steps on a warm machine, plus **≈ 10 s** for EditMode when an Editor is connected.
Cold (first run after a reboot, or after a clean output tree) the data steps and
`sim: determinism` pay JIT and first-read costs and Fast lands nearer two minutes;
budget **1–3 minutes** rather than the warm number.

Measured with the Editor open, the same day: `unity: EditMode` **30/30** and
`unity: PlayMode` **6/6**, with the web half NOT RUN because an Editor holding the
project lock cannot build (`docs/template/t2-report.md` §5).

## 3. Every test, and the suite it belongs to

### dotnet — `lib/Tests~/ProjectSurvive.Core.sln` (124 tests) → **Fast**

The projects compile the *same source* as the `lib/` asmdefs
([ADR 0004](../adr/0004-dotnet-layer-enforcement.md)), so these are the rule-engine
tests.

| Where | What it covers |
| --- | --- |
| `ArchitectureTests.cs` | layer references, `Domain`/`Application`/`Simulation` free of Unity/R3/VContainer/serializers, no direct `Infrastructure` use from Application |
| `RepoLayout.cs` | the repository's own layout contract (generated paths, data folders) |
| `Xoshiro128Tests.cs`, `RandomStreamsTests.cs` | the RNG, the reference implementation cross-check, the four streams |
| `FixedTickerTests.cs`, `ResultTests.cs`, `InfrastructureTests.cs` | the fixed-step accumulator, `Result`/`Error`, the clock and config adapters |
| `ConfigLoadTests.cs`, `ConfigRulesTests.cs`, `ConfigEnumParityTests.cs`, `ConfigFixture.cs` | the generated tables, the validator rules and the pinned row counts |
| `LocalizationServiceTests.cs` | localization lookup, both languages |
| `ProbeSerializationTests.cs` | the MessagePack **source-generated** formatter, protobuf round trips, enum wire values |
| `RunSkeletonTests.cs` | `RunSession` + `SceneFlow`: atomic commands, typed refusals, replay, the run's state hash |
| `Shared/*.cs` | `EventRing<T>` and `PrevCurBuffer<T>` — bounds, cursors, zero allocation |

### Unity EditMode — `ProjectSurvive.Game.Tests.EditMode` (30 tests) → **Fast**

| Class | What it covers |
| --- | --- |
| `ShellSmokeTests` | the 7-probe smoke suite in the Editor: DI, config hash + tables, R3, UniTask cancellation, MessagePack generated formatter, protobuf, Thai text |
| `LoopScreenTests` | the placeholder screens' presenters and their localized strings through the shipped tables |
| `WebGlBridgeTests` | the page-global names have one home per side (C# constants vs `tests/web/globals.mjs` vs the `.jslib`) |
| `WebSizeProfilesTests` | the size profiles and the configuration they record |

### Unity PlayMode — `ProjectSurvive.Game.Tests.PlayMode` (6 tests) → **Full**

| Class | What it covers |
| --- | --- |
| `LoopPlayModeTests` | Title → Hub → Encounter → Hub → Result → Title through the real scenes: one run, one scene scope, nothing retained after the title |
| `ShellPlayModeTests` | boot and handover, the scene marker, UniTask cancellation in play mode |

### Playwright — `tests/web` → **Full**

| Spec | Tests | What it covers |
| --- | --- | --- |
| `smoke.spec.ts` | 2 | the player boots, passes its own 7-probe self-check and hands over to Title; the Thai spike's glyph coverage in the browser |
| `title.spec.ts` | 1 | the title's start control in a built player: the two Thai strings and the build line are published, exactly one control is named inside the canvas, it sits at the canvas' centre at two sizes, and clicking it starts a run and reaches `Hub` |
| `loop.spec.ts` | 1 | the whole loop in a built player, every step a click at the point the screen published, every assertion the game's own answer |

### Process-level checks inside the harness → **Fast**

`sim: determinism` and `sim: stream separation` are not test-framework tests: they run
`ProjectSurvive.Sim` as a process and compare its output, which is the only place
determinism across *processes* is observable at all.

## 4. NOT RUN, and what makes it happen

NOT RUN is never a pass and never silent: `artifacts/test-summary.md` carries the
reason per step.

| Situation | Steps affected | Reported |
| --- | --- | --- |
| No Editor connected to this project | `unity: EditMode`, `unity: PlayMode` | NOT RUN with the connection requirement |
| A Unity Editor holds the project lock | `web: WebGL build`, `web: Playwright` | NOT RUN: batch mode cannot share a project |
| `-Suite Fast` | `unity: PlayMode` and both web steps | NOT RUN: "the Full suite was not requested" |
| The build step failed or reported NOT RUN | `web: Playwright` | NOT RUN: there is no player to test |
| A Unity test run produced no result within `-UnityTimeoutSeconds` | that Unity step | NOT RUN with the timeout named |
| `node`/`npx` missing, or `tests/web/node_modules` absent | `web: Playwright` | NOT RUN with the missing prerequisite |
| `tools/build-web.ps1` exits 2 (no Editor install, locked project) | `web: WebGL build` | NOT RUN with the script's own error line |

Because the Unity steps need the Editor open and the WebGL build needs it closed,
**one Full run cannot report all of EditMode, PlayMode, the build and Playwright.**
Two runs do, and both write their own `test-summary.md`; copy the file between runs
(the game-era reports kept them as `…-editor-open-summary.md` and
`…-closed-summary.md`).

## 5. Focused runs

```powershell
# one xUnit class
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter FullyQualifiedName~Xoshiro128

# the Sim, with a trace of the first ticks
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 12345 --trace 5

# one Unity class (Editor open)
unity command run_tests --mode playmode --async_tests --filter LoopPlayModeTests
unity command test_status --result-only
```

A Unity test-assembly compile error makes `run_tests` report the **previous**
assembly's results with `success: true`. Always confirm the test you just added is in
the result list ([`../llmwiki/gotchas/stale-test-assembly.md`](../../llmwiki/gotchas/stale-test-assembly.md)).

## 6. The browser suite

`tools/build-web.ps1` writes `UnityProject/Builds/Web`; `tools/serve-web.mjs` serves
it with the content types a player needs; `tests/web` drives Chrome at it.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Force   # Editor closed
node tools/serve-web.mjs --port 8080                                             # then npx playwright test
```

One server at a time: a stray `node` listening on 8080 makes the harness's own server
exit on `EADDRINUSE` and the step report NOT RUN.

## 7. PlayMode and the ambient-log flake

The game-era bulk PlayMode run failed on Unity's own ambient `LogAssert` messages
(Input System `IndexOutOfRangeException`, R3 *Cached unprocessed value*) while every
class passed on its own. The template's PlayMode assembly is small (two classes, six
tests) and has passed as a bulk run; if it ever fails that way, check whether any
failure is an assertion of ours before treating it as a regression, and run the class
on its own:

```powershell
unity command run_tests --mode playmode --async_tests --filter ShellPlayModeTests
```
