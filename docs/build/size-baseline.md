# WebGL payload size baseline (Stage 03b)

What a WebGL player of ProjectSurvive weighs, what each size lever bought, and what the
remaining bytes are. Every number here came from a build on this machine
(Intel Core i5-14400F, 31.8 GiB RAM, RTX 5060 Ti, Windows 11 `10.0.26300`; Unity
**`6000.3.9f1`**); byte counts are not timing numbers, so they do not depend on it.

The unit of measurement, the three bases and why the budget is on a *compressed* payload are
fixed by [ADR 0011](../adr/0011-webgl-payload-budget-and-size-measurement.md). Stage 03 left
this open — its report's §6 lists "the release path (compression, stripping, dropping the
`Resources/` leftovers) has not been measured" as an open risk. This document is that
measurement.

## 1. Reproducing a number

A size figure belongs to a *configuration*, and every configuration here is a value passed to
one command, not Editor state:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile baseline      -OutputDirectory UnityProject/Builds/Web-baseline-repro
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile strip-high    -OutputDirectory UnityProject/Builds/Web-strip-high
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile optimize-size -OutputDirectory UnityProject/Builds/Web-optimize-size
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile minimal       -OutputDirectory UnityProject/Builds/Web-minimal

# measure a build directory (uncompressed + gzip -9 + Brotli q11, per file and total)
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web-optimize-size --label optimize-size
# attribute .wasm bytes to assemblies (reads IL2CPP's generated C++ from the Library tree)
node tools/measure-il2cpp-size.mjs --artifacts UnityProject/Library/Bee/artifacts/WebGL --label optimize-size
```

`WebBuildPipeline.BuildWebForSize` applies the profile **in the same process as the build**
(`WebSizeProfiles.ApplyAndPersist`) and writes the settings that were actually in effect into
`build-report.json` next to the player, so a reported number can always be traced back to the
configuration that produced it. `-SizeProfile` switches the script to Release: only a Release
build's size is a fact about the game rather than about debug symbols (ADR 0003).

Do not produce a size number from the Editor GUI. A plain **Build** click in the Build Settings
window applies the active platform profile and rewrites unrelated player settings — during this
stage it reset `webGLNameFilesAsHashes` (0 → 1), `webGLCompressionFormat` (2 → 0) and
`m_LightsUseLinearIntensity` (1 → 0) in `ProjectSettings/`. See §7.

## 2. The configurations measured

Cumulative, one setting per step, which is what makes the deltas below explicable:

| Profile | Managed stripping | IL2CPP code generation | Exceptions | What it is |
| --- | --- | --- | --- | --- |
| `baseline` | **Low** | OptimizeSpeed | Explicitly thrown only | the project's own configuration — every value is a WebGL default, not a choice |
| `strip-high` | **High** | OptimizeSpeed | Explicitly thrown only | step 1: stripping only |
| `optimize-size` | High | **OptimizeSize** | Explicitly thrown only | step 2: code generation only — recommended |
| `minimal` | High | OptimizeSize | **None** | step 3 — *not run*, see §6 |

The `baseline` column is a *measured* configuration, not a remembered one: the WebGL defaults were
read out of the editor on a checkout whose `ProjectSettings.asset` carries no WebGL overrides
(`il2cppCodeGeneration: {}`, `managedStrippingLevel: {WebGL: 1}`, …) with

```powershell
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode -quit `
  -projectPath UnityProject `
  -executeMethod ProjectSurvive.Game.Editor.WebSizeProfiles.DumpSettings `
  -logFile artifacts/engine-defaults.log
```

which reported: IL2CPP, `il2cppCompilerConfiguration = Release`, `stripEngineCode = true`,
`debugSymbolMode = Off`, `managedStrippingLevel = Low`, **`il2cppCodeGeneration = OptimizeSpeed`**,
`exceptionSupport = ExplicitlyThrownExceptionsOnly`, `wasmArithmeticExceptions = Throw`.
`baseline` pins exactly those values, so step 1 and step 2 are each one setting wide and neither of
them is silently also a code-generation change.

### An earlier "before" player that is not used here

`UnityProject/Builds/Web-before` (48.81 MiB raw, 16.93 MiB gzip, 11.79 MiB Brotli) was built early in
this stage — before the profile machinery existed — and **carries no record of its settings**: no
`build-info.json`, no report, nothing. It is 12.2 MiB of `.wasm` *smaller* than the recorded default
configuration with a `.data` that matches it to 0.03 %, which is the signature of a build that ran
with size-optimised code generation; but nothing establishes that, and an artifact without its
configuration is not a baseline. It is kept on disk as context and excluded from every table below.
That gap is the reason this stage built `-SizeProfile` + `build-report.json` at all.

## 3. Initial payload, per file and per step

Every figure is the payload ADR 0011 §1 defines: the five files the loader fetches before the
game runs. `TemplateData/*` is out. The `baseline` row is `Builds/Web-baseline-repro`: "repro"
because it is the *second* baseline build — the first one, `Web-before`, is the artifact §2 retired
because its configuration was never recorded.

| Build | `index.html` | `.loader.js` | `.framework.js` | `.wasm` | `.data` | **player total** |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `baseline` raw | 5,646 | 26,982 | 430,536 | 49,976,699 | 12,958,956 | **63,398,819 (60.46 MiB)** |
| `strip-high` raw | 5,630 | 26,982 | 409,045 | 34,892,423 | 11,107,365 | **46,441,445 (44.29 MiB)** |
| `optimize-size` raw | 5,642 | 26,982 | 404,114 | 23,884,342 | 11,067,646 | **35,388,726 (33.75 MiB)** |
| `baseline` gzip -9 | 2,148 | 9,326 | 86,334 | 13,177,401 | 5,640,719 | **18,915,928 (18.04 MiB)** |
| `strip-high` gzip -9 | 2,145 | 9,326 | 85,469 | 11,140,516 | 5,043,180 | **16,280,636 (15.53 MiB)** |
| `optimize-size` gzip -9 | 2,147 | 9,326 | 85,084 | 8,646,096 | 5,027,987 | **13,770,640 (13.13 MiB)** |
| `baseline` brotli q11 | 1,591 | 8,161 | 72,252 | 8,581,029 | 4,353,642 | **13,016,675 (12.41 MiB)** |
| `strip-high` brotli q11 | 1,586 | 8,131 | 71,935 | 7,427,339 | 3,893,984 | **11,402,975 (10.87 MiB)** |
| `optimize-size` brotli q11 | 1,590 | 8,137 | 71,712 | 6,215,967 | 3,876,965 | **10,174,371 (9.70 MiB)** |

Per-change deltas on the player total:

| Step | Raw | gzip -9 | Brotli q11 | Where it came from |
| --- | ---: | ---: | ---: | --- |
| `baseline` → `strip-high` (stripping only) | −16,957,374 B (−26.7 %) | −2.51 MiB (−13.9 %) | −1.54 MiB (−12.4 %) | managed stripping removes 1,851,591 B (−14.3 %) of `.data` (managed metadata) **and** 15,084,276 B (−30.2 %) of `.wasm` (unreferenced engine code) |
| `strip-high` → `optimize-size` (code generation only) | −11,052,719 B (−23.8 %) | −2.39 MiB (−15.4 %) | −1.17 MiB (−10.8 %) | size-optimised C++ compilation of the same generated code: `.wasm` 34,892,423 → 23,884,342 B (−31.5 %) |
| **`baseline` → `optimize-size`** | **−28,010,093 B (−44.2 %)** | **−4.91 MiB (−27.2 %)** | **−2.71 MiB (−21.8 %)** | both levers |

`.data` is 10.55 MiB raw / 3.70 MiB Brotli of the recommended build — **31 % of the raw payload** —
so from here on assets are a size lever with the same standing as code (§5).

## 4. Verdict against the gate and the target

| Basis | Gate / target (ADR 0011 §3) | `baseline` | `strip-high` | `optimize-size` |
| --- | --- | --- | --- | --- |
| gzip -9 | **gate: < 30 MiB** | 18.04 MiB — PASS | 15.53 MiB — PASS | **13.13 MiB — PASS** |
| Brotli q11 | **target: < 15 MiB** | 12.41 MiB — PASS | 10.87 MiB — PASS | **9.70 MiB — PASS** |

**The gate was already met before this stage**, on a configuration that is now recorded rather than
remembered: the project's own player is 18.04 MiB gzipped and 12.41 MiB Brotli. Reading a WebGL
player's *raw* size as its size is what made Stage 03's report call its artifact "unbalanced" —
ADR 0011 §3 makes raw bytes a reported comparison, never a verdict.

What the work bought is therefore not "passing" but **headroom**: the recommended build leaves
**5.30 MiB (35 %) of the Brotli target** unspent for the content of Stage 04 onward, against
2.59 MiB (17 %) on the project's default configuration, and it moves 44.2 % of the uncompressed
payload and 21.8 % of the Brotli payload out of the build.

The recommended configuration is browser-verified, not just measured: the `optimize-size`
player passes both Playwright tests on the served build — all 7 self-check probes green
(`messagepack.roundtrip` and `protobuf.roundtrip` included, i.e. High stripping did not remove
the source-generated formatters) and the Thai spike at `totalMissingGlyphs=0`
(`artifacts/playwright-03b-size.log`).

## 5. Where the remaining bytes are

### 5a. Assets (`.data`)

1,773 packed assets. Ranked by packed size (identical in every configuration — asset packing
does not depend on the size profile):

| # | Asset | Size | Note |
| ---: | --- | ---: | --- |
| 1 | `Assets/Resources/Fonts/THSarabunPSK SDF.asset` | 4,220,916 B (4.03 MiB) | the Thai font atlas Stage 03 baked |
| 2 | *Built-in* `Splash Screen Unity Logo` | 2,796,376 B (2.67 MiB) | engine splash, present unless the license drops it |
| 3 | `TextMesh Pro/…/LiberationSans SDF.asset` | 1,078,464 B | TMP's always-included default resources |
| 4 | `Assets/Main/Fonts/Sarabun-Regular SDF.asset` | 1,068,156 B | the Stage 03 spike's uGUI atlas |
| 5 | `Assets/Main/Fonts/Sarabun-Regular FontAsset.asset` | 1,067,840 B | **the same font as a UI Toolkit asset** |
| 6 | `TextMesh Pro/…/Unity SDF.asset` (Examples & Extras) | 1,053,192 B | imported TMP examples |
| 7 | `Assets/Main/UI/UnityDefaultRuntimeTheme.tss` | 675,146 B | UI Toolkit runtime theme — the spike's other arm |
| 8 | `Resources/unity_builtin_extra` | 554,244 B | built-in shader/material library |
| 9 | `TextMesh Pro/Fonts/LiberationSans.ttf` | 352,734 B | source font behind #3 |
| 10–15 | TMP Examples & Extras textures, `EmojiOne.png`, `Roboto-Bold.ttf` | ~1.4 MB together | none of it used by the game |

By object type: `Texture2D` 12,942,128 B, `Font` 1,364,666 B, `Cubemap` 524,492 B,
`Shader` 417,564 B, `MonoBehaviour` 310,948 B. (These sum to more than the 11.07 MB `.data`
file because the container itself is compressed.)

**5.3 MB of the payload is Stage 03 spike material and TMP samples** — the duplicate Thai font
asset (#4 vs #5, 2.14 MB for one font), the UI Toolkit theme (#7), and TMP's Examples & Extras
import (#6, #9, #10–15). That is **15 % of the recommended build's raw payload** (24 % with the
built-in splash #2, which is a licence decision rather than a content one). It is the cheapest
next lever and it is not taken here because both font stacks are still the Stage 03 evidence
behind the shipped `uGUI + TMP` decision — deleting the losing arm settles the UI-stack question
with a size commit, which is the wrong order.

### 5b. Code (`.wasm`)

Measured, not inferred: `tools/measure-il2cpp-size.mjs` reads IL2CPP's own generated C++ per
assembly (`Library/Bee/artifacts/WebGL/il2cppOutput/cpp/<assembly>/`) and the post-strip managed
assemblies next to it. Taken from the `strip-high` build's tree; OptimizeSize changes how that
C++ is **compiled**, not what IL2CPP **generates**, so the ranking holds for both.

Generated C++ total: 550,715,351 B (525.20 MiB) across 230 files, 71 assemblies.

| # | Assembly | Generated C++ | % of all | Top-20 total |
| ---: | --- | ---: | ---: | --- |
| 1 | `UnityEngine.UIElementsModule` | 37.67 MiB | 7.17 % | |
| 2 | `mscorlib` | 21.18 MiB | 4.03 % | |
| 3 | `Unity.InputSystem` | 15.52 MiB | 2.95 % | |
| 4 | `Unity.RenderPipelines.Core.Runtime` | 11.31 MiB | 2.15 % | |
| 5 | `Unity.RenderPipelines.Universal.Runtime` | 10.04 MiB | 1.91 % | |
| 6 | `UnityEngine.CoreModule` | 9.26 MiB | 1.76 % | |
| 7 | `Unity.TextMeshPro` | 8.24 MiB | 1.57 % | |
| 8 | `Google.Protobuf` | 6.44 MiB | 1.23 % | |
| 9 | `System.Core` | 6.14 MiB | 1.17 % | |
| 10 | `MessagePack` | 5.84 MiB | 1.11 % | |
| 11–20 | `UnityEngine.TextCoreTextEngineModule`, `UnityEngine.UI`, `Unity.RenderPipelines.GPUDriven.Runtime`, `System`, `UnityEngine.PropertiesModule`, `UnityEngine.IMGUIModule`, `UniTask`, `UnityEngine.Physics2DModule`, `VContainer`, `Assembly-CSharp` | 27.02 MiB together | 5.14 % | |
| — | **not attributable to one assembly** | **356.65 MiB** | **67.91 %** | shared generic instantiations 207.10 MiB, IL2CPP runtime tables and stubs 92.78 MiB, shared generic methods 56.77 MiB |

Two things follow, and they are the reason a package-removal ablation was not run (§6):

1. **The `.wasm` is dominated by generic sharing and the IL2CPP runtime, not by our code.** Every
   game assembly in the top 20 together is under 2 % of the generated C++; `Assembly-CSharp` is
   `1.20 MiB / 0.23 %`. Dropping one package cannot move the module by more than the package's own
   share, and the eight vendored/third-party rows above sum to ~19 %.
2. **The largest single attributable row is `UnityEngine.UIElementsModule` (7.17 %)** — UI Toolkit,
   which the game does not use; it is present because the package/module ships with the engine and
   Stage 03's spike imported `UnityDefaultRuntimeTheme.tss`. Removing the module is the one
   code-side lever with a visible share, and it belongs to the stage that closes the UI-stack
   question (ADR 0008), not here.

Caveat this tool cannot answer: generated-C++ bytes are a **proxy** for `.wasm` bytes. It ranks
assemblies against each other; it does not apportion the final module, so nothing here should be
quoted as "assembly X costs Y kilobytes of `.wasm`".

## 6. Levers deliberately not taken

| Lever | Why not |
| --- | --- |
| `minimal` profile (`WebGLExceptionSupport.None`, arithmetic exceptions ignored) — step 3 | It removes `try`/`catch` support itself, and shipping code uses it: `lib/Runtime/Infrastructure/Config/LubanGameConfig.cs:349` catches `ConfigLoadException` and a general `Exception` around config load. A build that cannot catch turns a load failure into an abort, so the saving (if any) is bought with behaviour. **NOT RUN** — no number is claimed for it. |
| Removing `MessagePack` / `Google.Protobuf` / `R3` / `VContainer` / `UniTask` one at a time to price them | Every one is load-bearing in shipping code (`MessagePack` in `ProjectSurvive.Core.Contracts`, `Google.Protobuf` in `ProjectSurvive.Core.Generated.Proto`, the other three across `ProjectSurvive.Game.Presentation`). An ablation would mean deleting or stubbing shipped code to obtain a measurement. Replaced by §5b, which measures the same thing without touching the build (ADR 0011 §5). |
| Removing the `UIElements` module, the TMP Examples & Extras import, the duplicate Thai font asset, the UI Toolkit theme | Real levers (§5), but each is a content/UI-stack decision rather than a build-setting one. Recorded as follow-ups, not done here. |
| Pre-compressing inside Unity (`WebGLCompressionFormat`) | ADR 0011 §2: it decides whether Unity also emits `.br`/`.gz` siblings, not what the payload costs. Compression is measured from the bytes by `tools/measure-web-size.mjs`, one artifact per configuration instead of one per encoding. |
| Code stripping below High / link.xml tuning | High is the lowest stripping level Unity offers; below it is not a setting. |

## 7. Two process findings this stage produced

1. **A GUI Build click is not a neutral action.** On 2026-10-14 at 11:48 a build started from the
   Editor's Build Settings window (not this pipeline: `UnityEditor.BuildPlayerWindow:CallBuildMethods`
   in `Editor.log`, output to `Builds/Web` with content-hashed file names). It applied platform
   profile defaults over the project's WebGL settings — `webGLNameFilesAsHashes` 0 → 1,
   `webGLCompressionFormat` 2 → 0, `m_LightsUseLinearIntensity` 1 → 0 — and regenerated the shared
   IL2CPP artefacts under `Library/Bee`, which is what `tools/measure-il2cpp-size.mjs` reads. Both
   `ProjectSettings.asset` and `GraphicsSettings.asset` were restored to their committed values, and
   the artifact it wrote is not part of any number above. The lesson is not "don't touch the
   Editor": it is that **only the CLI pipeline produces evidence**, and `build-report.json` recording
   the settings each build ran with is what makes such contamination detectable at all.
2. **A profile-less build is no longer labelled as one.** `BuildWeb` used to stamp
   `"sizeProfile": "baseline"` into every report, including builds that applied no profile and ran
   on whatever the project happened to contain. It now reports `"sizeProfile": "none"` with
   `"sizeProfilePlan": null`, and the settings it *actually* ran with — a build nobody configured
   must not look like one that was measured. Asserted by
   `WebSizeProfilesTests.A_build_with_no_profile_is_not_labelled_as_one`.
