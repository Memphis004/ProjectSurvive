# System — Unity shell

The `UnityProject/` half of the repository: the Unity `6000.3.9f1` project that loads
the rules package, shows the placeholder loop, and produces the WebGL2 build the
browser tests run against. Setup and commands live in
[`../../UnityProject/SETUP.md`](../../UnityProject/SETUP.md); this note explains how
the pieces fit.

## What it is made of

| Piece | File | Job |
| --- | --- | --- |
| Composition root | `Assets/Main/Scripts/Presentation/RootLifetimeScope.cs` | the only place a port is bound to an adapter. Persists across scenes; a scene scope inherits it |
| Boot | `Assets/Main/Scripts/Presentation/SceneBoot.cs` | runs the smoke suite, publishes the verdict, then hands over to Title. Accepts `?scene=<name>` for an allow-listed scene |
| Smoke suite | `Presentation/Smoke/SmokeRunner.cs` | 7 probes: `di.resolution`, `config.tables`, `r3.subscribe`, `unitask.cancellation`, `messagepack.roundtrip`, `protobuf.roundtrip`, `localization.thai` |
| Page bridge | `Presentation/Web/WebGlBridge.cs` + `Assets/Main/Plugins/WebGL/ProjectSurviveWeb.jslib` | publishes reports as `window.__projectSurvive*` globals and dispatches `projectsurvive:smoke` / `:scene` events |
| Report writer | `Presentation/Web/JsonReportWriter.cs` | tiny JSON writer; escapes **every** non-ASCII code point as `\uXXXX` because the report crosses an ANSI `LPStr` marshalling boundary |
| Title / Hub / Encounter / Result | `Presentation/{Title,Hub,Encounter,Result}*` + `Hub/`, `Encounter/`, `ResultScene/` | thin MVP per screen: a presenter calls Application, a view only assigns text, a director starts the screen and asks the loader to leave |
| Thai spike | `Presentation/Thai/*` | measures the same strings through uGUI+TMP and UI Toolkit, publishes the numbers — the evidence behind [ADR 0008](../../docs/adr/0008-thai-text-ui-stack.md) |
| Scopes | `Presentation/Flow/{RunLifetimeScope,RunScopeOwner,SceneLoader,TitleSceneDirector}.cs` | the Run scope and the only scene loader |
| Editor setup | `Assets/Main/Scripts/Editor/ProjectSetup.cs` | URP asset, player settings, font baking, the build list, and the UI/entity helpers the builders call |
| Scene builders | `Assets/Main/Scripts/Editor/{Boot,Title,Hub,Encounter,Result,ThaiTextSpike}SceneBuilder.cs` | one menu per scene, each written through the Editor API (AGENTS rule 11) |
| Build | `Assets/Main/Scripts/Editor/WebBuildPipeline.cs` | `BuildWebDevelopment` / `BuildWebRelease`, writes `build-info.json` next to the player |

`ProjectSettings/` and `Packages/manifest.json` are part of the template: the engine
patch is locked in `tools/versions.lock.json`, and a package version is not changed
without an ADR and a re-measurement.

## The published globals are the test surface

A WebGL build cannot run Unity's test framework, and scraping the log from a browser
is guesswork, so the player publishes what it did:

| Global | Shape | Asserted by |
| --- | --- | --- |
| `__projectSurviveSmoke` | `{platform, configHash, probeCount, failedCount, passed, probes[]}` | `tests/web/smoke.spec.ts` (every probe named, `failedCount === 0`) |
| `__projectSurviveScene` | the scene name on screen | `smoke.spec.ts` and `loop.spec.ts` |
| `__projectSurviveThaiSpike` | `{platform, font, rowCount, totalMissingGlyphs, coversEveryGlyph, stacksMeasureEqually, rows[]}` | the Thai test in `smoke.spec.ts` (`totalMissingGlyphs === 0`) |
| `__projectSurviveTitle` / `Hub` / `Encounter` / `Result` | the screen's strings plus every control's centre in canvas pixels | `title.spec.ts`, `loop.spec.ts` |

The **names** have one home on each side: the constants in `WebGlBridge`, and
`tests/web/globals.mjs` for the specs. The `.jslib` takes the name as an argument
instead of writing it into the plugin, and `WebGlBridgeTests` fails if the plugin
hard-codes a name or the two files disagree.

Two details that cost real debugging time:

- If the JSON cannot be parsed, the `.jslib` **still publishes** a global with
  `passed:false` and the parse error. A browser test then fails on a value instead of
  timing out on a missing global.
- `SceneBoot` accepts `?scene=<name>` for an allow-listed scene
  (`ThaiTextSpike`, `Hub`, `Encounter`, `Result`). It is the only way a page reaches a
  screen directly.

## Scenes are generated, never hand-written

Six scenes exist: `Boot`, `Title`, `ThaiTextSpike`, `Hub`, `Encounter`, `Result`. Each
is built by its own builder through `EditorSceneManager` and saved; `ProjectSetup`
keeps only what is shared (URP, player settings, font baking, the UI helpers,
`ConfigureBuildSettings`). The build list only adds a scene whose file exists, so an
older checkout still boots.

Consequences:

- A new scene is **one builder file plus one menu item** — the 2 000-line setup
  monolith is not the place for it.
- Re-running a builder recreates the scene with fresh `fileID`s, so a large harmless
  diff is expected. Commit it with that said.
- Never edit `.unity` YAML by hand:
  [`../gotchas/unity-yaml-not-hand-editable.md`](../gotchas/unity-yaml-not-hand-editable.md).

## The Web build

`tools/build-web.ps1` runs the Editor in batch mode and calls
`WebBuildPipeline.BuildWebDevelopment` (or `BuildWebRelease`), so the artifact is the
same one the menu builds. It writes `build-report.json` (including `sizeSettings`) and
`build-info.json` next to the player; **the configuration a size may be quoted under
is the one recorded there**, never one assumed from the menu path. See
[ADR 0011](../../docs/adr/0011-webgl-payload-budget-and-size-measurement.md) and
[`../../docs/build/size-baseline.md`](../../docs/build/size-baseline.md).

The build needs the interactive Editor **closed**; `tools/test.ps1` reports the web
steps NOT RUN when the project is locked, never PASS.
