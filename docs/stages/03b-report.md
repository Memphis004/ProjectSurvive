# Stage 03b report — WebGL payload budget, size baseline, per-assembly attribution

- **Date:** 2026-10-14
- **Machine:** Intel Core i5-14400F (10C/16T), 31.8 GiB RAM, NVIDIA RTX 5060 Ti,
  Windows 11 `10.0.26300`
- **Engine:** Unity **`6000.3.9f1`**, URP `17.3.0`, WebGL Build Support present
- **Browser for the one browser claim below:** Google Chrome **`154.0.8037.98`**
  (`tools/versions.lock.json`), driven by Playwright `1.64.0`
- **Decision:** [ADR 0011](../adr/0011-webgl-payload-budget-and-size-measurement.md) — what the
  payload is, the three bases a size may be quoted in, and why the budget is on a *compressed*
  payload
- **Measured data:** [`docs/build/size-baseline.md`](../build/size-baseline.md)

## 1. What was accomplished

1. **The size gate now has a definition, and against that definition it was already met — now on
   a configuration that is recorded rather than remembered.** Stage 03's report listed "the release
   path (compression, stripping, dropping the `Resources/` leftovers) has not been measured" as an
   open risk and read its own artifact as *48.81 MiB*. Measured on the basis ADR 0011 fixes (the
   five files the loader fetches), the project's own configuration is **18.04 MiB gzip -9** and
   **12.41 MiB Brotli q11** — inside the `< 30 MiB` gate and the `< 15 MiB` target with 6.9 % and
   17 % to spare. The alarming number was an uncompressed reading; ADR 0011 §3 makes raw bytes a
   reported comparison, never a verdict.
2. **The "before" column turned out not to be reproducible, and that became the finding.** The
   first Release player this stage measured (`Builds/Web-before`, 48.81 MiB) was built before the
   profile machinery existed and **records no settings at all**. Re-building the configuration the
   project actually has — read out of a clean editor with the new
   `WebSizeProfiles.DumpSettings`, not assumed: IL2CPP, Release C++, engine code stripped, managed
   stripping **Low**, IL2CPP code generation **OptimizeSpeed**, exceptions explicitly thrown only —
   produces a player **12.2 MiB of `.wasm` larger** than that artifact and a `.data` that matches it
   to 0.03 %. Something about `Web-before` was smaller than the project's own defaults and nothing
   on disk says what. It is therefore retired from every table (kept as context) and replaced by a
   measured, reported baseline. A number nobody can reproduce is not a baseline, which is the whole
   reason `-SizeProfile` and `build-report.json` exist.
3. **The build configuration became a value instead of Editor state.**
   `WebSizeProfiles` defines four cumulative profiles (`baseline`, `strip-high`, `optimize-size`,
   `minimal`), each differing from the previous by exactly one setting, and
   `WebBuildPipeline.BuildWebForSize` applies the profile **in the same process as the build**
   and writes both the requested profile and the settings that were *actually* in effect into
   `build-report.json` next to the player. `tools/build-web.ps1 -SizeProfile <name>` is the
   reader-facing form. A size number whose configuration lives in someone's Editor session is not
   reproducible, and before this stage that is exactly where the WebGL settings lived —
   `ProjectSettings.asset` still said `managedStrippingLevel: Low` while a build in progress was
   running High.
4. **Two levers measured, one deliberately refused.** Cumulative deltas on the player total
   (full table in the size doc):

   | Step | Raw | gzip -9 | Brotli q11 |
   | --- | ---: | ---: | ---: |
   | `baseline` → `strip-high` (managed stripping Low → High, code generation pinned) | −16,957,374 B (−26.7 %) | −2.51 MiB | −1.54 MiB |
   | `strip-high` → `optimize-size` (IL2CPP OptimizeSpeed → OptimizeSize) | −11,052,719 B (−23.8 %) | −2.39 MiB | −1.17 MiB |
   | **`baseline` → `optimize-size`** | **−28,010,093 B (−44.2 %)** | **−4.91 MiB (−27.2 %)** | **−2.71 MiB (−21.8 %)** |

   The `minimal` profile (`WebGLExceptionSupport.None`) was **not built**: it removes `try`/`catch`
   support itself and shipping code catches around config load
   (`lib/Runtime/Infrastructure/Config/LubanGameConfig.cs:349`), so the saving would be bought
   with behaviour. No number is claimed for it.
5. **`MessagePack` / `Google.Protobuf` / `R3` / `VContainer` / `UniTask` were measured, not
   ablated.** Every one is load-bearing in shipping code, so "remove one and rebuild" would mean
   deleting or stubbing shipped code to obtain a number — a trade no stage may make. Instead
   `tools/measure-il2cpp-size.mjs` reads IL2CPP's own generated C++ per assembly and the
   post-strip managed assemblies beside it. Result: of 550,715,351 B of generated C++,
   **67.91 % is not attributable to any one assembly** (shared generic instantiations 207.10 MiB,
   IL2CPP runtime tables and stubs 92.78 MiB, shared generic methods 56.77 MiB). The largest
   attributable row is **`UnityEngine.UIElementsModule` at 7.17 %** — UI Toolkit, which the game
   does not use; the whole game code base (`Assembly-CSharp`) is **0.23 %**. This is the answer to
   "what makes the `.wasm` big", and it says package removal is the wrong lever.
6. **The recommended build was verified in a browser, not only measured.** The `optimize-size`
   player was served by `tools/serve-web.mjs` and exercised by the Stage 03 Playwright suite:
   **2/2 passed** — all 7 self-check probes green, including `messagepack.roundtrip` and
   `protobuf.roundtrip` (i.e. High stripping did *not* remove the source-generated formatters,
   the failure mode Stage 03 was built to catch), and the Thai spike at `totalMissingGlyphs=0`.
   A size figure for a build nobody ran would be a number about a file, not about the game.
7. **Two process findings and one honesty fix.** (a) A build started from the Editor's **Build
   Settings** window at 11:48 rewrote unrelated player settings (`webGLNameFilesAsHashes` 0 → 1,
   `webGLCompressionFormat` 2 → 0, `m_LightsUseLinearIntensity` 1 → 0) and regenerated the shared
   IL2CPP tree under `Library/Bee`; both ProjectSettings files were restored to their committed
   state and its artifact is excluded from every number here. Only the CLI pipeline produces
   evidence. (b) `BuildWeb` used to stamp `"sizeProfile": "baseline"` into builds that applied no
   profile at all; it now writes `"sizeProfile": "none"` with `"sizeProfilePlan": null` so a build
   nobody configured cannot be read as a measured data point.
8. **Two tool defects surfaced by using the tools rather than by review.**
   `tools/build-web.ps1` failed a *successful* single-`.wasm` build under `Set-StrictMode`
   (a scalar `FileInfo` has no `.Count`), and `tools/measure-web-size.mjs` printed its "wrote …"
   progress line to stdout, corrupting `--json > file`.
9. **Tests:** EditMode **20/20 passed** (12 Stage 03 + 8 size-profile tests), no compile errors,
   run in batch mode against the real Editor.

## 2. Important files that changed

**New**

| File | Role |
| --- | --- |
| `UnityProject/Assets/Main/Scripts/Editor/WebSizeProfiles.cs` | the four profiles, their applied values, their documented plan, `ReadBack()`, `ToJson()` for the build report, `None` for profile-less builds |
| `UnityProject/Assets/Main/Tests/EditMode/WebSizeProfilesTests.cs` | 8 tests: cumulative order, the `baseline` configuration being the project's own defaults, one-setting-per-step, invariant IL2CPP release settings, unknown-profile rejection, apply-matches-plan (with settings restored in a `finally`), and the `none` labelling |
| `tools/measure-web-size.mjs` | per-file uncompressed / gzip -9 / Brotli q11 for a build directory, JSON + Markdown |
| `tools/measure-il2cpp-size.mjs` | generated C++ and stripped-assembly bytes per assembly, from IL2CPP's own output tree |
| `docs/build/size-baseline.md` | the measured baseline: per-step table, asset ranking, per-assembly attribution, levers not taken |
| `docs/adr/0011-webgl-payload-budget-and-size-measurement.md` | the payload unit, the three bases, the gate/target, Release-only measurement, and why ablation was rejected |

**Changed**

| File | Change |
| --- | --- |
| `UnityProject/Assets/Main/Scripts/Editor/WebBuildPipeline.cs` | `BuildWebForSize` (`-sizeProfile` / `-outputDir` from the command line, profile applied in-process), `WriteSizeReport` (`build-report.json`: files, packed assets, object types, requested + effective settings), `sizeProfile = None` default |
| `tools/build-web.ps1` | `-SizeProfile` (and `-Profile` as an alias for `-Configuration`), the StrictMode `.Count` fix |
| `Assets/Main/Tests/EditMode/ProjectSurvive.Game.Tests.EditMode.asmdef` | references `ProjectSurvive.Game.Editor` so the profile rules are testable |
| `knowledge.md`, `llmwiki/index.md`, `llmwiki/stage-log.md` | the new commands, the size doc, the gotcha |

`UnityProject/ProjectSettings/{ProjectSettings,GraphicsSettings}.asset` are **not** in the change
list on purpose: §6 (a) restored them.

## 3. Commands that were actually run

```powershell
# what configuration does the project already have? (reads the editor, builds nothing, ~2 min)
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode -quit `
  -projectPath UnityProject `
  -executeMethod ProjectSurvive.Game.Editor.WebSizeProfiles.DumpSettings `
  -logFile artifacts/engine-defaults.log

# the measurable data points (each ~9-35 min, one invocation = one configuration)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile baseline      -OutputDirectory UnityProject/Builds/Web-baseline-repro
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile strip-high    -OutputDirectory UnityProject/Builds/Web-strip-high
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile optimize-size -OutputDirectory UnityProject/Builds/Web-optimize-size

# measurement (add --json/--markdown to store a copy)
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web-optimize-size --label optimize-size
node tools/measure-il2cpp-size.mjs --artifacts UnityProject/Library/Bee/artifacts/WebGL --label optimize-size

# the artifact is verified in a real browser, not just built
node tools/serve-web.mjs --root UnityProject/Builds/Web-optimize-size --port 8091
cd tests/web && BASE_URL=http://127.0.0.1:8091 npx playwright test

# EditMode suite in batch mode (the Editor must be closed)
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode -runTests `
  -projectPath UnityProject -testPlatform EditMode `
  -testResults artifacts/editmode-03b.xml -logFile artifacts/unity-editmode-03b.log
```

## 4. Results and where the artifacts live

| What | Result | Evidence (`artifacts/`, git-ignored) |
| --- | --- | --- |
| What the project's configuration actually is | Low stripping, OptimizeSpeed codegen, Release C++, exceptions explicitly thrown only | `artifacts/engine-defaults.log` |
| `baseline` build | Release, 49,976,699 B `.wasm`, build 581.6 s, player 60.46 MiB raw / 18.04 MiB gzip / 12.41 MiB Brotli | `UnityProject/Builds/Web-baseline-repro/`, `artifacts/size/baseline-repro/web-size.{json,md}` |
| `strip-high` build | Release, 34,892,423 B `.wasm`, build 2060.5 s | `UnityProject/Builds/Web-strip-high/`, `artifacts/size/strip-high/{web-size,il2cpp}.{json,md}`, `build-report.json` |
| `optimize-size` build | Release, 23,884,342 B `.wasm`, build 533.1 s | `UnityProject/Builds/Web-optimize-size/`, `artifacts/size/optimize-size/{web-size,build-report}.json` |
| Retired "before" player | 48.81 MiB raw / 16.93 MiB gzip / 11.79 MiB Brotli, **no recorded configuration** | `artifacts/size-release-baseline.{json,md}` — context only, in no table |
| Size measurement | 3 configurations × 5 files × 3 bases | `artifacts/size/*/web-size.*` |
| Per-assembly attribution | 550,715,351 B / 230 files / 71 assemblies; 67.91 % unattributable | `artifacts/size/strip-high/il2cpp.{json,md}` |
| Browser verification of the recommended build | **2/2 passed**, 7/7 probes, `totalMissingGlyphs=0` | `artifacts/playwright-03b-size.log`, `artifacts/serve-web-03b.log` |
| EditMode suite | **20/20 passed**, 0 failed | `artifacts/editmode-03b.xml`, `artifacts/unity-editmode-03b.log` |
| Gate / target verdict | gate **PASS** 13.13 MiB gzip, target **PASS** 9.70 MiB Brotli, on the recommended build | `docs/build/size-baseline.md` §4 |

## 5. What was not tested, and why

- **The `minimal` profile (exception support `None`) has no number.** It removes `try`/`catch`
  support, and shipping code catches around config load, so any figure would be a figure for a
  build whose behaviour changed. Recorded as a lever, deliberately not taken.
- **Package-removal ablation was not run** — every candidate package is load-bearing in shipping
  code (ADR 0011 §5). It is replaced by the per-assembly measurement in §1.4, which is honest
  about its own limit: generated-C++ bytes rank assemblies against each other but do **not**
  apportion the final `.wasm`, so "package X costs Y KB" is not a claim this stage can make.
- **Runtime speed of the optimised module was not measured.** `il2cppCodeGeneration = OptimizeSize`
  trades code size against speed, and this stage measured only size. No frame-time claim is made;
  ADR 0003 requires a named browser and version for timing, and `minimal`/`optimize-size` timing
  belongs with the performance stage that needs it.
- **The 5.3 MB asset lever was not taken.** Duplicate Thai font assets (2.14 MB for one font), TMP
  Examples & Extras, the UI Toolkit runtime theme and the built-in splash are 15 % of the raw
  payload (24 % with the splash), but removing them is a UI-stack and licence decision, not a size
  setting: the size doc records them as the next lever for the stage that closes ADR 0008.
- **Firefox / Edge, and any non-Windows machine.** Same limitation as Stage 03; the size table is
  machine-independent but engine-version-dependent, so a Unity patch bump invalidates it.
- **Why `Web-before` is smaller than the project's own default configuration is not established.**
  The artifact records nothing, and the 12.2 MiB of `.wasm` between it and the recorded `baseline`
  is consistent with size-optimised code generation but is not proof. Re-creating it would mean
  guessing at a configuration and calling the guess history, so the number is left unattributed
  instead.
- **The build script's own guard paths** (`-SizeProfile` with no Editor, locked project) were not
  re-exercised this stage; they are Stage 03's tests.

## 6. Blockers and risks

- **A GUI build can silently invalidate size evidence.** The 11:48 Build-Settings build rewrote
  player/GraphicsSettings and regenerated the single-slot IL2CPP tree in `Library/Bee` that
  `tools/measure-il2cpp-size.mjs` reads. Anything measured from that tree must be read immediately
  after the build it belongs to — a wrapper that runs the measurement as part of the build is worth
  having before CI relies on it (Stage 14). `build-report.json` recording effective settings is what
  made the contamination detectable.
- **`UnityProject/Builds/Web` currently holds that stray GUI build** (default settings, content-hashed
  file names, 48.2 MB `.wasm`). It was not deleted — it is not this stage's artifact — but it is not
  evidence either: regenerate it with `tools/build-web.ps1 -Profile Release`.
- **The EditMode suite dirties `ProjectSettings.asset`.** Applying profiles in a test marks the
  player settings dirty, and the batch Editor writes them on exit (values restored, keys added).
  The file was restored to its committed state after the run; a future stage should keep the suite
  from persisting at all.
- **The gate was met before the work, so the risk this stage actually reduces is headroom.** The
  recommended build leaves 5.30 MiB (35 %) of the Brotli target unspent where the project's own
  configuration leaves 2.59 MiB (17 %). Stage 04 has since landed 14 pieces, 28 recipes and 51
  localization rows, and Stage 08 brings art: nothing yet *enforces* the budget, so a CI size gate on
  `tools/measure-web-size.mjs` is the concrete follow-up.
- **The measured configuration is not the project's default.** `optimize-size` is applied per build
  by `-SizeProfile`; a plain `-Profile Release` build still uses the project's own settings (Low
  stripping, OptimizeSpeed). A later stage may make the recommended configuration the project
  default, but that changes what every non-size build produces and belongs with a decision about
  shipping, not with a measurement.
- **Unity's own splash (2.67 MiB) is in every build** and cannot be removed on a Personal licence.

## 7. Acceptance criteria

| # | Criterion | Verdict |
| --- | --- | --- |
| 1 | The payload unit, the compression bases and the gate/target are defined, and a verdict names its basis | **PASS** — ADR 0011; the same build is 44.29 MiB raw / 15.53 MiB gzip / 10.87 MiB Brotli |
| 2 | Gate: initial payload, gzip -9, `< 30 MiB` on the recommended build | **PASS** — 13.13 MiB |
| 3 | Target: initial payload, Brotli q11, `< 15 MiB` on the recommended build | **PASS** — 9.70 MiB (5.30 MiB / 35 % unspent) |
| 4 | `.wasm`, `.data`, `.framework.js`, `.loader.js` reported uncompressed and compressed | **PASS** — size doc §3, per file, three bases |
| 5 | Every configuration reproducible from the command line; one command = one data point | **PASS** — `-SizeProfile`, applied in-process, recorded in `build-report.json`; `DumpSettings` reads the project's own configuration out of the editor, which is how the unreproducible "before" player was caught |
| 6 | Each measured step differs from the previous by exactly one setting | **PASS** — enforced by `Each_step_changes_only_the_setting_it_is_named_after`, not by prose |
| 6a | The "before" column is a configuration the project actually has, not a remembered one | **PASS** — `baseline` was read out of a clean editor and then built: 63,398,819 B, and the old unattributed 48.81 MiB player is excluded from every table |
| 7 | The main cause of `.wasm` bytes is identified | **PASS** — 67.91 % generic sharing + IL2CPP runtime; largest attributable assembly 7.17 %; own game code 0.23 % |
| 8 | Package removal / ablation measured per package | **NOT RUN** — rejected with reason (ADR 0011 §5); replaced by per-assembly attribution |
| 9 | `minimal` profile measured | **NOT RUN** — removes `try`/`catch` support that shipping code uses |
| 10 | The recommended build runs in a browser, not just builds | **PASS** — Playwright 2/2, 7/7 probes, Thai spike 0 missing glyphs |
| 11 | Automated tests for the new rules, run green | **PASS** — EditMode 20/20 in batch mode, 8 new |
| 12 | No new errors or unexplained warnings | **PASS** — 0 `error CS` in both build logs; 2 build warnings, the same pair Stage 03 explained in `SETUP.md` §6 |
| 13 | Docs and test commands updated; results recorded in this report | **PASS** — `docs/build/size-baseline.md`, `knowledge.md`, `llmwiki/index.md`, `llmwiki/stage-log.md`, this report |
| 14 | Runtime speed of the size-optimised module | **NOT RUN** — no timing is claimed anywhere in this stage |
| 15 | Commit with a `stage-03b:` message | **PASS** — see the commit carrying this report |

The criterion that matters most for the next stage is **#11 together with #6**: the table in §1.3
is only meaningful while each profile step stays one setting wide, and that is now an assertion
rather than a convention.
