# System — Unity shell (Stage 03)

The `UnityProject/` half of the repository: the Unity `6000.3.9f1` project that
loads the rules package, shows the title screen, and produces the WebGL2 build the
browser tests run against. Setup and commands live in
[`../../UnityProject/SETUP.md`](../../UnityProject/SETUP.md); this note explains how
the pieces fit.

## What it is made of

| Piece | File | Job |
| --- | --- | --- |
| Composition root | `Assets/Main/Scripts/Presentation/RootLifetimeScope.cs` | the only place a port is bound to an adapter. One scope per scene (a run-level scope is a Stage 06 concern) |
| Boot | `Assets/Main/Scripts/Presentation/SceneBoot.cs` | runs the smoke suite, publishes the verdict, then loads Title |
| Smoke suite | `Assets/Main/Scripts/Presentation/Smoke/SmokeRunner.cs` | 7 probes (DI, config hash + tables, R3, UniTask cancellation, MessagePack generated formatter, Protobuf, Thai text) |
| Page bridge | `Assets/Main/Scripts/Presentation/Web/WebGlBridge.cs` + `Assets/Main/Plugins/WebGL/ProjectSurviveWeb.jslib` | publishes reports as `window.__projectSurvive*` globals and dispatches `projectsurvive:smoke` / `:scene` events |
| Report writer | `Assets/Main/Scripts/Presentation/Web/JsonReportWriter.cs` | tiny JSON writer; escapes **every** non-ASCII code point as `\uXXXX` because the report crosses an ANSI `LPStr` marshalling boundary |
| Title | `TitlePresenter.cs` + `TitleView.cs` + `UiState.cs` | thin MVP: presenter calls Application, view only assigns text (AGENTS rule 10) |
| Thai spike | `Thai/ThaiSpikeReporter.cs`, `ThaiSpikeTmpView.cs`, `ThaiSpikeToolkitView.cs` | measures the same strings through uGUI+TMP and UI Toolkit, publishes the numbers — evidence for [ADR 0008](../../docs/adr/0008-thai-text-ui-stack.md) |
| Editor setup | `Assets/Main/Scripts/Editor/ProjectSetup.cs` | URP asset, player settings, font baking, scene creation. Scenes are built through the Editor API (AGENTS rule 11) |
| Build | `Assets/Main/Scripts/Editor/WebBuildPipeline.cs` | `BuildWebDevelopment` / `BuildWebRelease`, writes `build-info.json` next to the player |

## The two published globals are the test surface

A WebGL build cannot run Unity's test framework, and scraping the Unity log from a
browser is guesswork, so the player publishes what it did:

| Global | Shape | Asserted by |
| --- | --- | --- |
| `__projectSurviveSmoke` | `{platform, configHash, probeCount, failedCount, passed, probes[]}` | `tests/web/smoke.spec.ts` (every probe named, `failedCount === 0`) |
| `__projectSurviveScene` | the scene name on screen | the same spec (Boot → Title) |
| `__projectSurviveThaiSpike` | `{platform, font, rowCount, totalMissingGlyphs, coversEveryGlyph, stacksMeasureEqually, rows[]}` | the Thai test in the same spec (`totalMissingGlyphs === 0`) |

Two details that cost real debugging time:

- If the JSON cannot be parsed, the `.jslib` **still publishes** a global with
  `passed:false` and the parse error. A browser test then fails on a value instead
  of timing out on a missing global.
- `SceneBoot` accepts `?scene=<name>` for an allow-listed scene
  (`SceneBoot._urlSceneOverrides`). It is the only way a page can reach the Thai
  spike, whose numbers are only meaningful in a real player.

## Environment facts that shape the tooling

- **One Editor per project.** Batch mode (`unity test`, `unity build`,
  `tools/build-web.ps1`) needs the interactive Editor closed. With it open, use the
  pipeline: `unity command run_tests …`, or a **detached** job for a build
  (`unity command eval 'ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment(); …' --detach`
  then `unity job wait <id>`) — a WebGL build takes minutes and one synchronous call
  cannot carry it.
- **`Assets/csc.rsp` carries one line** (`-nullable:annotations`) so the Unity
  compilation of the nullable-annotated rule engine does not emit 12 `CS8632`
  warnings per compile. Response files are raw argument lists — comments in them are
  parsed as file names.
- **Anything under `Assets/Resources/` ships**, referenced or not. The current build
  includes `Assets/Resources/Fonts/THSarabunPSK SDF.asset` (4.0 mb, 3.6 % of the
  build) and the TMP examples (~2 mb) that came with the project template. See
  `SETUP.md` §8.

## Measured, Stage 03

| Measurement | Value |
| --- | --- |
| Development Web build size | 116,854,350 bytes (`Web.wasm` 103 MB, `Web.data` 12 MB) — uncompressed and not stripped by design |
| Build time | 70.1 s incremental, 310.8 s cold (this machine) |
| Build warnings | 2 (empty `Simulation` asmdef; `com.unity.pipeline` runtime config) — both explained in `SETUP.md` §6 |
| Unity EditMode / PlayMode | 12/12, 5/5 |
| Frame timing in the browser | **not measured** — no per-frame timing was instrumented, so no performance claim is made in this stage |

Per AGENTS, a Development build's numbers are not evidence of shipped performance:
they are recorded here only as build facts. Timing claims need a Release build and
are deferred to the stage that needs them.
