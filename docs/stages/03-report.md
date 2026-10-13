# Stage 03 report — Unity shell, first Web build, Thai text spike

- **Date:** 2026-10-14
- **Machine:** Intel Core i5-14400F (10C/16T), 31.8 GiB RAM, NVIDIA RTX 5060 Ti
  (driver `32.0.16.1692`), Windows 11 `10.0.26300`
- **Engine:** Unity **`6000.3.9f1`** (revision `7a9955a4f2fa`), URP `17.3.0`, WebGL Build
  Support present
- **Browser for every Web number below:** Google Chrome **`154.0.8037.98`**
  (the browser recorded in `tools/versions.lock.json`), driven by Playwright `1.64.0`
- **Decision:** [ADR 0008](../adr/0008-thai-text-ui-stack.md) — uGUI + TextMeshPro,
  **Static** baked font assets

## 1. What was accomplished

1. **The Unity project exists and is reproducible from code.** `UnityProject/`
   (Unity `6000.3.9f1`, URP) references the rules package as a local package
   (`com.projectsurvive.core` → `file:../../lib`). Everything Stage 03 adds —
   URP asset, player settings, font atlases, runtime theme, three scenes, build
   settings — is produced by `ProjectSurvive > Stage 03 > 1/2/3` menu items
   (`Assets/Main/Scripts/Editor/ProjectSetup.cs`, `WebBuildPipeline.cs`), never by
   hand-written YAML (AGENTS rule 11). Both setup entry points are idempotent.
2. **A real gameplay-free shell runs**: `RootLifetimeScope` (composition root,
   factory registrations) → `SceneBoot` runs a **7-probe smoke suite** → publishes the
   verdict → hands over to the `Title` scene, which renders `ui.title` from
   `tblocalization` through a thin presenter/view pair.
3. **The build is testable from a browser.** `WebGlBridge` + a 40-line
   `ProjectSurviveWeb.jslib` publish three globals; the smoke suite, the scene on
   screen and the Thai spike numbers are all asserted by Playwright against the
   **served WebGL player** — not against the Editor.
4. **Thai text is settled with measurements** (ADR 0008): both candidate stacks
   render all spike strings with **0 missing glyphs** in the Editor *and* in the
   browser; the shipped font assets are **Static** with a self-verifying bake of
   **182 characters** (printable ASCII + all 87 assigned Thai code points).
5. **Three real defects were found and fixed**, each with a test that now guards it:
   - the JSON report writer emitted `[...}{...]` (no separator between array
     elements), so the page's `JSON.parse` rejected the whole report;
   - VContainer's reflected-constructor path is stripped by IL2CPP
     (`Type does not found injectable constructor, type: SystemClock`) — the
     container never built in the player, only in the Editor;
   - TMP's **pre-build processor empties Dynamic font assets' tables on the project
     asset**, so the first shipped build could not look up a single Thai character
     even though the Editor reported perfect coverage.
6. **Tooling**: `tools/build-web.ps1` (build with exit-code contract),
   `tools/serve-web.mjs` (correct WebGL content types), `tests/web/` (Playwright +
   a console-relay diagnostic script), `UnityProject/SETUP.md` (versions + commands),
   and `Assets/csc.rsp` (one line, to stop 12 `CS8632` warnings per compile).

## 2. Important files that changed

| File | What |
| --- | --- |
| `UnityProject/Assets/Main/Scripts/Presentation/RootLifetimeScope.cs` | composition root; every registration is an explicit factory |
| `.../Presentation/SceneBoot.cs` | runs the smoke suite, publishes, hands over; `?scene=` allow-list |
| `.../Presentation/Smoke/SmokeRunner.cs`, `SmokeReport.cs` | the 7 probes and the report |
| `.../Presentation/Web/WebGlBridge.cs`, `JsonReportWriter.cs` | page globals + JSON writer (comma bug fixed) |
| `.../Plugins/WebGL/ProjectSurviveWeb.jslib` | native→page bridge |
| `.../Presentation/Thai/ThaiSpike*.cs`, `ThaiTextSpike.cs` | the spike: two stacks, quoted measurement |
| `.../Presentation/{TitlePresenter,TitleView,UiState}.cs` | thin MVP title screen |
| `.../Scripts/Editor/ProjectSetup.cs` | URP/settings/theme/scenes + **self-verifying font bake** |
| `.../Scripts/Editor/WebBuildPipeline.cs` | Development/Release build + `build-info.json` |
| `.../Assets/Main/Fonts/*.asset` | both atlases baked (182 chars) and frozen to Static |
| `.../Assets/Main/Scenes/{Boot,Title,ThaiTextSpike}.unity` | generated through the Editor API |
| `.../Assets/csc.rsp` | `-nullable:annotations` |
| `.../Tests/EditMode/ShellSmokeTests.cs`, `.../Tests/PlayMode/ShellPlayModeTests.cs` | 12 + 5 tests |
| `tools/build-web.ps1`, `tools/serve-web.mjs`, `tests/web/**` | build → serve → browser test |
| `docs/adr/0008-thai-text-ui-stack.md` | the Thai decision |
| `llmwiki/systems/unity-shell.md`, `llmwiki/gotchas/{il2cpp-di-and-reflection,tmp-font-assets-in-builds,tmp-tryaddcharacters-lies}.md` | notes |
| `UnityProject/SETUP.md`, `docs/architecture/{overview.md,dependencies.md}`, `knowledge.md`, `llmwiki/{index.md,stage-log.md}`, `tools/versions.lock.json` | docs/versions updated |

## 3. Commands that were actually run

```bash
# Unity work (Editor open → its pipeline; batch mode needs the Editor closed)
unity command recompile                      # + recompile_status until "completed"
unity command menu "ProjectSurvive/Stage 03/1. Configure project (URP + WebGL)"
unity command menu "ProjectSurvive/Stage 03/2. Create scenes (Boot, Title, ThaiTextSpike)"
unity command run_tests EditMode
unity command run_tests PlayMode "" testName false true 600   # async; poll test_status
unity command run_tests PlayMode Thai_spike_scene_measures_both_stacks_without_missing_glyphs
unity command console                        # build/bake log + probe details

# Web build (detached job: a WebGL build takes minutes)
unity command eval 'ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment(); return "done";' --detach
unity job wait <jobId>

# Browser verification
node tools/serve-web.mjs --port 8080
cd tests/web && npm install && npx playwright test
node tests/web/scripts/probe-boot.mjs         # console relay, used to diagnose the player
```

Login-free, `-NoProfile` PowerShell was **not** needed this stage (the batch-mode
scripts were blocked by the open Editor; see §5).

## 4. Results and where the artifacts live

| Check | Result | Artifact |
| --- | --- | --- |
| C# compile (Editor assembly set) | 0 errors, 1 warning (empty `Simulation` asmdef, deliberate) | pipeline `recompile_status` |
| Unity **EditMode** suite | **12 / 12 passed** | `artifacts/editmode-results.json` |
| Unity **PlayMode** suite | **5 / 5 passed** (4 in the aggregated response + the Thai case run separately, because the pipeline's aggregate drops one case) | `artifacts/playmode-status.json`, `artifacts/playmode-thai-status.json` |
| Font bake | TMP `added=True missing=0 table=182 coversAll=true mode=Static`; TextCore `table=182 coversAll=true mode=Static` | `artifacts/thai-font-bake.json` |
| Baked tables on disk | 182 character entries + 182 glyphs in each atlas, `m_AtlasPopulationMode: 0` (Static) | read back out of the `.asset` files |
| WebGL build | `Succeeded - 117,503,533 bytes, 0 errors, 2 warnings, 75.5s` (Development; the 2 warnings are the empty `Simulation` asmdef and `com.unity.pipeline`'s runtime config) | `UnityProject/Builds/Web/build-info.json` |
| Served player | `index.html` 200, `Web.wasm` 200 `application/wasm`, `Web.framework.js` 200 `text/javascript` | `artifacts/serve-web.log` |
| **Playwright, smoke** | **passed** — `platform=WebGLPlayer`, `passed=true`, `failedCount=0`, all 7 probes named and passing, scene reaches `Title` | `artifacts/playwright-run-4.log` |
| **Playwright, Thai spike** | **passed** — 6 rows, `totalMissingGlyphs=0`, `coversEveryGlyph=true`, `stacksMeasureEqually=true` | same log + `tests/web/test-results/web-thai-spike.png` |
| Screenshots | title screen + Thai spike scene captured by the suite | `tests/web/test-results/*.png` |
| Player console after the fixes | no `VContainerException`, no missing-glyph reports | `artifacts/browser-probe-boot.log` |
| `tools/build-web.ps1` guard | checks the locked Editor, then exits **2** with a readable message while the project is open elsewhere (its documented "cannot run" contract) | `artifacts/build-web-guard.log` |

Probe details as published by the **player** (`__projectSurviveSmoke`):

```
di.resolution        resolved LubanGameConfig, LocalizationService, SystemClock, UiState
config.tables        hash=B706ED57 generated=B706ED57 localization=36 items=9 enemies=4
r3.subscribe         received 6, then stayed at 6 after dispose
unitask.cancellation await threw OperationCanceledException as expected
messagepack.roundtrip generated formatter, 26 bytes round-tripped
protobuf.roundtrip   AppInfo 87 bytes round-tripped
localization.thai    ui.title = พ่อหมอปราบผี (12 Thai code points)
```

Thai spike as measured **in the browser** (Chrome 154, same build; both stacks at
nominal size 48):

| Stack | `ui.title` | `ui.spike.thai` | `ui.spike.thai.marks` | Missing |
| --- | --- | --- | --- | --- |
| uGUI + TMP | 289.69 × 62.41 px | 829.67 × 62.41 px | 704.79 × 62.41 px | 0 |
| UI Toolkit | 290 × 63 px | 830 × 63 px | 705 × 63 px | 0 |

The same run in the Editor produced 289.7 / 829.7 / 704.8 × 62.4 and 290 / 830 / 705 × 63 —
i.e. the two environments agree, which is the point of measuring in both.

An earlier Playwright run against the *first* build is kept as the counter-evidence:
`artifacts/playwright-run-1.log` shows `{"platform":"BootFailure", …}`, and
`artifacts/playwright-run-2.log` shows `totalMissingGlyphs: 70`. Those two failures are
what the fixes in §1.5 address; they were caught by this stage's own gate.

## 5. What was not tested, and why

- **Release build / frame timing.** Only a **Development** build was produced. Per
  ADR 0003 any timing claim needs a Release build on the recorded machine, so **no
  performance number is claimed** anywhere in this report. The spike measured
  *coverage and layout*, not frame time.
- **`tools/build-web.ps1` (the build itself), `unity test`, `unity build` (batch mode)**
  — **NOT RUN**. All three spawn a batch-mode Editor, and this machine keeps an
  interactive Editor on the project (it is the environment's own tooling); batch mode
  refuses to share a project. The same code paths were exercised through the pipeline
  instead (`unity command run_tests`, `unity command eval … BuildWebDevelopment --detach`).
  What *was* exercised is the script's guard: with the Editor open it verifies the locked
  Editor, reports the lock and exits **2** (`artifacts/build-web-guard.log`). The build
  itself is therefore proven in the pipeline, not through this script.
- **Firefox / Edge.** Firefox is not installed and Edge was not run: the GDD's
  cross-browser pass is Stage 14. This stage's numbers are Chrome-only, as ADR 0003
  requires them to be.
- **Non-headless GPU validation.** The Playwright launch args keep the default ANGLE
  path with software fallback allowed; three hidden/diagnostic URP shaders log "not
  supported on this GPU". No visible-pass impact was observed, but a run on a
  different GPU stack has not been done.
- **Editor UI clicking / human review of the scenes.** The scenes were created and
  inspected through the Editor API and by loading them in tests; nobody clicked
  through them.
- **Thai shaping beyond the spike's three strings.** Coverage is proven for the
  strings the spike measures plus (by construction) every assigned Thai code point in
  the baked set. Thai has no complex reordering outside the marks measured, but
  paragraph-level line breaking in the browser is not asserted.

## 5b. Repository hygiene decisions taken while committing

- **`lib/**/*.meta` is now ignored.** Importing `lib/` as a local package made Unity
  write `.meta` files next to the rule-engine sources (129 of them). The tracked
  `.meta` files that matter are the config `.bytes` in `UnityProject`; nothing
  references a `lib/` asset by GUID (the project's asmdefs reference the layers by
  **assembly name**), and Stage 02 had already ruled `.meta` out of lib's generated
  output. The rule and its reason live in `.gitignore`.
- **Unity test-runner debris is ignored**: `UnityProject/Assets/InitTestScene*.unity`
  (the PlayMode runner's temporary init scene, which it does not always delete).
- **This commit also carries the Stage 03 work that predates the report**: the
  MessagePack checkpoint probe in `lib/Runtime/Contracts/Save` (which the
  `messagepack.roundtrip` smoke probe asserts), `LocalizationService` +
  `LocalizationServiceTests`, the regenerated `ConfigBuildMetadata` config hash, the
  new localization keys in `data/localization/localization.csv`, and the Contracts
  layer's MessagePack wiring. They were already in the working tree; they are listed
  here because the stage's own tests and the built player depend on them, and they were
  committed together rather than silently absorbed. The GDD file that is also modified
  in the working tree was **left untouched** — it is the project owner's document.

- **`Assets/Resources/Fonts/THSarabunPSK SDF.asset` (4.0 mb, 3.6 % of the build) and
  the TMP examples (~2 mb) ship because they live under `Resources/`.** They are
  pre-existing project/template assets that Stage 03 does not use and did not add, so
  they were recorded in `SETUP.md` §8 rather than deleted. The THSarabunPSK font has
  **no license file in the repo** (unlike Sarabun, which ships `OFL.txt`) — someone
  who owns that asset should either document its licence or move it out of
  `Resources/` before a release build.
- **The Editor pipeline's test reporting is unreliable**: the aggregated PlayMode
  response reports `total: 0` and omits one case, and the NUnit XML it writes
  (`%LOCALAPPDATA%Low/ProjectSurvive/ProjectSurvive/TestResults.xml`) has no
  `<test-case>` rows. Results in this report come from the per-run response and a
  per-filter re-run. Fixing that (or moving the suites to batch mode in CI) belongs
  to Stage 14.
- **Reading Thai from the Unity console through the pipeline mangles the text**
  (the relay goes through an ANSI boundary: `พ่อ` → `à¸žà¹ˆà¸­`). The JSON report
  escapes every non-ASCII code point, so this affects log reading only, never the
  player or the tests.
- **Glyph coverage is now a build-time responsibility.** A code point outside the
  baked set renders as a missing-glyph box in the player. Extending the set is
  `ProjectSetup.BuildBakedCharacterSet()` + re-run the setup menu item; the bake
  throws if it cannot cover the set it was asked for.
- **`com.unity.pipeline` (0.8.0-exp.1)** is in `manifest.json` because this machine
  drives the Editor through it. It is not a game dependency, but it does add a build
  warning and an experimental package to the project; it should be removed from any
  CI/clean checkout story that does not use the pipeline.
- **Unbalanced build weight**: a 112 MB Development build (`Web.wasm` 103 MB) is
  expected for Development/uncompressed, but the release path (compression,
  stripping, and dropping the `Resources/` leftovers) has not been measured.

## 7. Acceptance criteria

| # | Criterion | Verdict |
| --- | --- | --- |
| 1 | Unity `6000.3.9f1` project exists, URP, references `lib/` as a local package | **PASS** |
| 2 | Scenes/prefabs/assets created through the Editor API, never hand-written YAML | **PASS** — `ProjectSetup` menu items; three scenes generated |
| 3 | Project compiles with no new errors/warnings left unexplained | **PASS** — 0 errors; the 2 build warnings and the 1 compile warning are each explained in `SETUP.md` §6; 12 `CS8632` warnings removed by `Assets/csc.rsp` |
| 4 | Automated tests for the changed rules, run and green | **PASS** — EditMode 12/12, PlayMode 5/5 (incl. the new `ParseQueryValue` and JSON-validity tests) |
| 5 | A Web (WebGL2) build is produced and served | **PASS** — Development build, 117,503,533 bytes, served with correct content types |
| 6 | The build is verified in a browser, not just built | **PASS** — Playwright 2/2 on the served player, asserting the player's own published verdicts |
| 7 | Thai text renders correctly in the browser with the shipped font | **PASS** — `totalMissingGlyphs=0`, `coversEveryGlyph=true`, Player log free of missing-glyph reports; screenshots captured |
| 8 | The Thai text UI decision is recorded with measured evidence | **PASS** — ADR 0008 (Editor + browser numbers for both stacks) |
| 9 | Layer rules still hold (`Domain`/`Application`/`Simulation` free of Unity/R3/VContainer/serializers) | **PASS** — asmdefs unchanged; `dotnet` guard tests still run in `tools/test.ps1` |
| 10 | Docs and test commands updated; results recorded in this report | **PASS** — `SETUP.md`, `dependencies.md`, `overview.md`, `knowledge.md`, `llmwiki/*`, this report |
| 11 | Release-build performance numbers, if any are claimed | **NOT RUN** — no timing claim is made; a Release build is required first (ADR 0003) |
| 12 | Batch-mode tooling (`tools/build-web.ps1`, `unity test`) exercised | **NOT RUN** — blocked by the interactive Editor; the same code paths ran through the pipeline (see §5) |
| 13 | Cross-browser (Firefox/Edge) run | **NOT RUN** — Stage 14 |
| 14 | Commit with a `stage-03:` message | **PASS** — see the commit that carries this report |

Gate that matters most for the next stage: **criterion 6**, because it is the only one
that runs the artifact the player will actually get.
