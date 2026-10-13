# UnityProject — SETUP

> **Created in Stage 03.** The project is Unity **`6000.3.9f1`** (URP, WebGL2
> target) and references the rules package `lib/` by local path. Everything below
> was read off the installed packages and this machine — nothing is aspirational.

## 1. What exists now

| Piece | Where | Created by |
| --- | --- | --- |
| Unity project | `UnityProject/` (Unity `6000.3.9f1`, URP) | Unity Hub / Editor, Stage 03 |
| Rules package link | `Packages/manifest.json` → `"com.projectsurvive.core": "file:../../lib"` | manifest, Stage 03 |
| Render pipeline asset | `Assets/Main/Settings/ProjectSurviveURP.asset` | `ProjectSurvive/Stage 03/1. Configure project` |
| Font assets | `Assets/Main/Fonts/Sarabun-Regular SDF.asset` (TMP), `Sarabun-Regular FontAsset.asset` (TextCore) | same menu item; baked from `Sarabun-Regular.ttf` |
| Config tables | `Assets/Main/Resources/Config/tb*.bytes` | `tools/gen.ps1` (Stage 02) |
| Scenes | `Assets/Main/Scenes/{Boot,Title,ThaiTextSpike}.unity` | `ProjectSurvive/Stage 03/2. Create scenes` — Editor API only, never hand-written YAML (AGENTS rule 11) |
| Web player output | `UnityProject/Builds/Web/` (git-ignored) | `ProjectSurvive/Stage 03/3. Build Web (Development)` or `tools/build-web.ps1` |

## 2. Resolved package versions (verified on this machine)

Read from `Packages/manifest.json` and `Library/PackageCache/` after resolution.

| Package | Version | Source | Why |
| --- | --- | --- | --- |
| `com.unity.render-pipelines.universal` | `17.3.0` | Unity registry | renderer |
| `com.unity.inputsystem` | `1.20.1` | Unity registry | WASD / pointer input |
| `com.unity.test-framework` | `1.6.0` | Unity registry | EditMode / PlayMode |
| `com.unity.ugui` | `2.0.0` | Unity registry | uGUI + TextMeshPro (ADR 0008) |
| `jp.hadashikick.vcontainer` | `1.19.0` | OpenUPM | composition root / lifetimes |
| `com.cysharp.r3` | `1.3.1` | OpenUPM | UI-level reactive state (ADR 0002) |
| `com.cysharp.unitask` | `2.5.11` | OpenUPM | async + cancellation |
| `Google.Protobuf` | `3.33.6` | NuGet (NuGetForUnity) | external contracts |
| `MessagePack` (+`.Annotations`, `MessagePackAnalyzer`) | `3.1.12` | NuGet (NuGetForUnity) | checkpoint DTOs, **source generator only** |
| `Microsoft.Bcl.TimeProvider` | `8.0.0` | NuGet (NuGetForUnity) | R3's netstandard dependency |
| `com.projectsurvive.core` | `0.1.0` | local path `file:../../lib` | the rules package |

Scoped registry: OpenUPM, scopes `com.cysharp`, `jp.hadashikick.vcontainer`.

`com.unity.pipeline` (`0.8.0-exp.1`) is present because this machine drives the
Editor through it; it is **not** a game dependency and the player does not use it
(its one build warning is explained in §6).

## 3. `Assets/csc.rsp` — and why it has exactly one line

The rule engine in `lib/Runtime` is compiled twice: by `dotnet` for `lib/Tests~`
(where `Directory.Build.props` sets `<Nullable>enable</Nullable>`) and by Unity as
the local package `com.projectsurvive.core`. Unity's script compilation has no
nullable control of its own, so the C# compiler reported **CS8632** for every
`string?` annotation in the rule engine — twelve warnings per compile.

`Assets/csc.rsp` therefore contains one flag:

```
-nullable:annotations
```

`annotations` (not `enable`) is deliberate: it makes `string?` legal and keeps the
*warning* context off, so no new CS86xx warning can appear in the Unity-only
assemblies. A response file is a raw argument list — **comments in it are parsed as
file names** (`error CS2001: Source file '`string?`' could not be found`), which is
why the explanation lives here rather than inside the file.

## 4. Commands

### 4.1 Editor-only setup (idempotent, run in this order)

```
ProjectSurvive/Stage 03/1. Configure project        (URP asset, player settings, fonts)
ProjectSurvive/Stage 03/2. Create scenes            (Boot, Title, ThaiTextSpike + build settings)
ProjectSurvive/Stage 03/3. Build Web (Development)  (writes UnityProject/Builds/Web)
```

The same code is reachable from the command line:
`ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment` (Development)
and `…BuildWebRelease` (Release).

### 4.2 Tests

```bash
# Unity EditMode + PlayMode (Editor must be closed; see §5)
unity test UnityProject --mode EditMode  --output artifacts/editmode-results.xml
unity test UnityProject --mode PlayMode  --output artifacts/playmode-results.xml

# or, with the Editor open, through its pipeline (the results come back in the
# response; PlayMode must be requested asynchronously)
unity command run_tests EditMode
unity command run_tests PlayMode "" testName false true 600
unity command test_status

# rule engine, no Unity involved
dotnet test lib/Tests~/ProjectSurvive.Core.Tests.sln
```

### 4.3 The Web build and the browser test

```bash
# 1. build (Editor closed; exit codes 0 / 1 / 2 as documented in the script header)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1

# 2. serve it (sets the WebGL content types; prints "serving <dir> at <url>")
node tools/serve-web.mjs --port 8080

# 3. Playwright, in a second shell
cd tests/web && npm install && npx playwright test
```

`tests/web/playwright.config.ts` defaults to `http://127.0.0.1:8080`; override with
`BASE_URL`. When a player fails before it publishes anything, relay its console with
`node tests/web/scripts/probe-boot.mjs` — it prints every console line, page error and
failed request, which is where a `VContainerException` or a shader error shows up. The suite runs the **installed Google Chrome** (`channel: "chrome"`),
not Playwright's bundled Chromium: ADR 0003 makes compatibility claims only for a
named browser, and `tools/versions.lock.json` records Chrome `154.0.8037.98` as
that browser.

The WebGL player also accepts `?scene=<name>` to boot straight into an allow-listed
diagnostic scene (`ThaiTextSpike` is the only entry, see
`SceneBoot._urlSceneOverrides`). That is how the browser test reaches the Thai text
spike, which only produces meaningful numbers inside a real player.

## 5. One Editor per project

Unity refuses to open a project that another Editor has open, so:

- `unity test` and `unity build` (which spawn a **batch-mode** Editor) only work
  when the interactive Editor is closed — `unity command quit` is the supported
  way out (`File/Exit` also works; the pipeline's own `quit` command currently
  fails with a `DontDestroyOnLoad` error, so do not use it).
- `tools/build-web.ps1` checks for `Temp/UnityLockfile` and exits `2` with a
  readable message instead of hanging.
- With the Editor open, drive the same operations through its pipeline instead:
  `unity command run_tests …`, and for a build
  `unity command eval 'ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment(); …' --detach`
  followed by `unity job wait <jobId>` — a WebGL build takes ~5 minutes, which is
  why it must be a detached job rather than one synchronous call.

## 6. Warnings that are expected (and why)

| Warning | Why it stays |
| --- | --- |
| `Assembly … ProjectSurvive.Core.Simulation.asmdef will not be compiled, because it has no scripts` | `lib/Runtime/Simulation` is deliberately empty until Stage 07 (EcsLite). The asmdef exists so Stage 07 only adds files. |
| `Pipeline: No RuntimePipelineConfig asset found … Pipeline will be disabled in Player builds` | Emitted by the `com.unity.pipeline` **tooling** package on every build. The player does not use it. |

Anything else should be treated as a real finding. The twelve `CS8632` warnings
that the first Web build reported are fixed by `Assets/csc.rsp` (§3).

## 7. Generated and ignored

- `UnityProject/Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` are
  git-ignored and must never be committed.
- `Packages/packages-lock.json` is ignored; `manifest.json` is the source of truth.
- `Assets/Main/Resources/Config/*.bytes` is generated by `tools/gen.ps1` and
  ignored; only the `.meta` files are tracked so Unity keeps stable GUIDs.
- Never edit `lib/Runtime/Generated/**` by hand — change `data/` or `proto/` and
  run `tools/gen.ps1` (AGENTS rule 4).

## 8. Assets that ship from `Resources/` (build-weight note)

`Assets/Resources/**` is always included in a build, whatever references it. The
current Development build measured `Assets/Main/Resources/Config/*.bytes` plus
`Assets/Resources/Fonts/THSarabunPSK SDF.asset` (**4.0 mb, 3.6 % of the build**)
and the TextMesh Pro examples/sprites imported with TMP essentials (~2 mb). None of
those are used by Stage 03 code. They are template leftovers, not Stage 03 work, so
they are recorded here instead of deleted; a release build should move them out of
`Resources/` or drop them.
