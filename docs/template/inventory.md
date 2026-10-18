# Template extraction inventory (T0)

Scope: classify **every file and folder** under `lib/`, `UnityProject/Assets/`, `data/`, `proto/`, `tools/`,
`tests/web/`, `docs/`, `llmwiki/` and `.agents/skills/`, list what each surviving file depends on, order the
cuts, and record what only the user can decide. Nothing outside `docs/template/` was changed.

Verdicts: **KEEP** as-is · **GENERALIZE** keep but remove game coupling · **DROP** delete · **MOVE** to
`docs/template/examples/` · **REGEN** generated, never hand-edited.

A row that names a folder covers **every file under it**, including the sub-folders a row does not spell out:
`Presentation/Battle/{Hud,Vfx}` (8 + 1), `Assets/Packages/<11 NuGet packages>/**` (also LFS-tracked),
`TextMesh Pro/{Fonts,Resources,Shaders,Sprites}/**`, `lib/third_party/ecslite/src/**` (5 files) and
`lib/third_party/luban-runtime/src/**` (4 files).

Two notes before the tables:

- **Path.** The worktree is `C:\Workspace\ProjectSurvive-template` (branch `template/main`, base
  `972a655`), as the shared brief now states — Q1 is closed.
- **The tree is already dirty.** 156 files are deleted but uncommitted (all `lib/` DROP rows), and
  `lib/Tests~/fixtures/duplicate-id/` is untracked. That predates this pass and conflicts with T0's DoD
("`git status` changes only in `docs/template/`") — see **Q2**. This pass changed and deleted no tracked
file; the only untracked file it removed was its own earlier scratch plan (`docs/stages/T0-plan.md`), so the
change set stays inside `docs/template/`.

---

## 1. `lib/`

### 1.1 `Runtime/Shared/` — 13 files — **12 KEEP, 1 GENERALIZE**

`Result.cs`, `ResultOfT.cs`, `Error.cs`, `ErrorKind.cs`, `FixedTicker.cs`, `IClock.cs`, `IRandomSource.cs`,
`RandomStreams.cs`, `SplitMix32.cs`, `Xoshiro128.cs`, `EventRing.cs` (stage 10t, ADR 0024),
`PrevCurBuffer.cs` (stage 10t, ADR 0024) — the determinism core and typed results; no game type.
`RandomStreamKind.cs` — **GENERALIZE** (member names, see §15 Q3).

### 1.2 `Runtime/Domain/Config/` — 6 files — split

| File | Verdict | Why |
| --- | --- | --- |
| `IGameConfig.cs` | GENERALIZE | keep `ConfigHash`, `Constant`, `Localization`, `TryGetLocalization`, `GetText`; the 11 game tables and 6 `TryGet*` go |
| `ConfigTables.cs` | GENERALIZE | 348 lines: keep `LocalizationEntry` + a template `ConstantConfig`; `BagConfig`…`ShopPoolEntryConfig` go |
| `ConfigEnums.cs` | GENERALIZE | keep `Language`; `Element`, `ItemCategory`, `SkillBehavior`, `EnemyRole`, `MovePattern`, `SynergyKind`, `SynergyStat` go |
| `Footprint.cs` | DROP | bag-grid geometry (313 lines), no meaning outside the bag |
| `CellOffset.cs` | DROP | bag-grid offset used only by footprint/placement |
| `UpgradeVocabulary.cs` | DROP | merge-upgrade stat vocabulary (stage 09f) |

### 1.3 `Runtime/Domain/{Combat,Inventory,Run,Shop}/` — already deleted — **DROP (confirmed)**

`Combat/Battle` 21, `Inventory` 13, `Run` 13, `Shop` 4. One row in the supplied table has no entry:
`Run/SceneId.cs` is a **scene name**, not a game rule, and both Application and Presentation still need it →
**MOVE** to `Application/Flow/SceneId.cs` (see Q4), not DROP.

### 1.4 `Runtime/Application/` — 4 files

| Path | Verdict | Why |
| --- | --- | --- |
| `Run/RunSession.cs` (1238 lines) | GENERALIZE | becomes the run scope: `Seed`, `RandomStreams`, `EncounterIndex`, `Outcome`, `IDisposable`; every command returns `Result`/`Result<T>` |
| `Run/SceneFlow.cs` (175) | GENERALIZE | state machine over `SceneId { Boot, Title, Hub, Encounter, Result }`, no battle session |
| `Run/ReplayLog.cs` (218) | GENERALIZE | `(sequence, command)` list + seed; no `BattleReplay` |
| `Text/LocalizationService.cs` | KEEP | verified: reads `IGameConfig.Localization` + `Language` only |
| `Battle/Fake/` (4 files, deleted) | DROP (confirmed) | fake battle session, no meaning without a battle |

### 1.5 `Runtime/Contracts/Save/` — 4 files

`CheckpointProbe.cs`, `CheckpointProbeCodec.cs` — **KEEP** (MessagePack source-generator smoke test, generic).
`BackpackSnapshotDto.cs`, `BackpackSnapshotCodec.cs` — **DROP** (they name `Domain.Inventory` types).

### 1.6 `Runtime/Infrastructure/` — 3 files

`SystemClock.cs` **KEEP** · `Config/LubanGameConfig.cs` **GENERALIZE** (14 `TableNames` → the kept subset;
the 7 id-keyed dictionaries go) · `Save/MessagePackBackpackSnapshotCodec.cs` **DROP** (the only
`IBackpackSnapshotCodec` adapter; the folder then empties — see Q5).

### 1.7 `Runtime/Simulation/` — 1 asmdef, no scripts — **KEEP + 1 new marker**

`ProjectSurvive.Core.Simulation.asmdef` currently references `Leopotam.EcsLite`; after the cut it references
only `Shared` and `Domain`, and gets `SimulationLayerMarker.cs` (a doc-only static class) so the assembly is
not empty and the layer rule has something to name.

### 1.8 `Runtime/Generated/` — 39 files (38 Luban + 1 Proto) — **REGEN**

Never edited by hand. `gen.ps1` rewrites all of it after `data/` is trimmed (T1 step 5 regenerates nothing —
`data` is trimmed in T2, so T1 only stops *reading* the dropped tables; see Q6).

### 1.9 `Tests~/` (dotnet harness)

| Path | Verdict | Why |
| --- | --- | --- |
| `Core.Tests/FixedTickerTests`, `ResultTests`, `Xoshiro128Tests`, `RandomStreamsTests`, `ProbeSerializationTests`, `LocalizationServiceTests`, `InfrastructureTests` | KEEP | generic primitives + ports |
| `Core.Tests/Shared/{EventRingTests,PrevCurBufferTests}` | KEEP | stage 10t generic buffers |
| `Core.Tests/ArchitectureTests` | GENERALIZE | the layer test itself must survive (ADR 0004); drop the EcsLite row and the "Simulation may reference EcsLite" assertion |
| `Core.Tests/RepoLayout` | GENERALIZE | asserts repo paths that move (`data/tables/*`, `docs/stages/*`) |
| `Core.Tests/ConfigLoadTests`, `ConfigFixture`, `ConfigEnumParityTests` | GENERALIZE | retarget from 14 tables to constant + localization + sample |
| `Core.Tests/ConfigRulesTests` | GENERALIZE | keep duplicate-id / missing-ref / range; the bag, synergy, merge and wave cases go |
| `Core.Tests/UpgradeVocabularyTests` | DROP | tests a dropped vocabulary |
| `Core.Tests/EcsLiteSmokeTests` | DROP | the engine is leaving |
| `Core.Tests/{Battle,Inventory,Run}/` (8 + 10 + 6, deleted) | DROP (confirmed) | game rules |
| `Data.Validator/Program.cs`, `ConfigHash.cs`, `.csproj` | KEEP | the runner + hash are generic |
| `Data.Validator/ConfigValidator.cs` | GENERALIZE | keep duplicate id, missing ref, value range; drop bag/synergy/merge/wave/shop rules |
| `Layers/*.csproj` (10) | GENERALIZE | delete `Leopotam.EcsLite.csproj`, drop the `ProjectReference` from `ProjectSurvive.Core.Simulation.csproj`; the other 8 stay |
| `Layers/ProjectSurvive.Core.Simulation.csproj` | KEEP | the layer-enforcement project for Simulation (ADR 0004) |
| `Sim/Program.cs`, `SimEnvironment.cs`, `StreamsMode.cs`, `BenchmarkMode.cs` | GENERALIZE | keep the console shell, the streams check and the benchmark; repoint at the skeleton |
| `Sim/BotRun.cs`, `CampBot.cs`, `KiteBot.cs` | DROP | the bots play the dropped game |
| `Sim/ProjectSurvive.Sim.csproj` | KEEP | |
| `data/` | KEEP | empty placeholder (`.gitkeep`) |
| `fixtures/missing-ref/`, `fixtures/duplicate-id/` | GENERALIZE | **both currently point at dropped tables** (`missing-ref` → `bag.csv`, `duplicate-id` → `item.csv`); see Q7 |
| `fixtures/README.md` + 19 other fixture dirs (40 files, deleted) | DROP (confirmed) | each is a game-table rule |

### 1.10 `third_party/`

`ecslite/` (sources, `Leopotam.EcsLite.asmdef`, `VENDOR.md`, license, package.json) — **DROP**, together with
its 6 references outside the folder: `Simulation.asmdef` (line 7),
`Layers/ProjectSurvive.Core.Simulation.csproj` (line 20), `Layers/Leopotam.EcsLite.csproj` (delete),
`tools/versions.lock.json` `packages[EcsLite]` (line 125), `Core.Tests/EcsLiteSmokeTests.cs`, and
`docs/architecture/dependencies.md` (lines 70, 96, 106, 112). **`tools/doctor.ps1` has no EcsLite check** —
the supplied table's instruction for it is a no-op (verified by grep).
`luban-runtime/` — **KEEP** (the data pipeline stays).

---

## 2. `UnityProject/`

Ignored, not classified: `Library/`, `Logs/`, `Temp/`, `obj/`, `UserSettings/`, `.vs/`, `*.csproj`, `*.sln`.

### 2.1 `Assets/Main/Scripts/` — 84 `.cs`

| Path | Verdict | Why |
| --- | --- | --- |
| `Presentation/` root (10): `RootLifetimeScope`, `SceneBoot`, `UiState`, `TitlePresenter`, `TitleReport`, `TitleView`, `ResultPresenter`, `ResultReport`, `ResultUiState`, `ResultView` | KEEP / GENERALIZE | shell + title/result keep; the Result four and `RootLifetimeScope`'s registrations need edits (§9) |
| `Presentation/Flow/` (4): `RunLifetimeScope`, `RunScopeOwner`, `SceneLoader`, `TitleSceneDirector` | KEEP | the most valuable part of stage 10 — with edits for `RunSession`/`SceneId` (§9) |
| `Presentation/Web/` (2), `Presentation/Smoke/` (2) | KEEP | `WebGlBridge` names only the 3 page globals; verified no game noun |
| `Presentation/Thai/` (5) + `Scenes/ThaiTextSpike.unity` | KEEP | the Thai font regression scene |
| `Presentation/Battle/` (28) | DROP | battle view, HUD, VFX, dev menu |
| `Presentation/Camp/` (12) | DROP | bag screen, camp presenter, tooltip |
| `Scripts/Battle/` (3), `Scripts/Camp/` (2) | DROP | scene scopes + directors |
| `Scripts/ResultScene/ResultSceneDirector` (2) | GENERALIZE | reads `RunSummary`; becomes the generic outcome summary |
| `Scripts/Infrastructure/ResourcesGameConfigLoader.cs` | KEEP | |
| `Scripts/Editor/` (13) | see 2.2 | |

### 2.2 `Scripts/Editor/` — 13 files

| File | Verdict | Why |
| --- | --- | --- |
| `ProjectSetup.cs` | GENERALIZE | menu entry + the shared scene scaffolding; drop the Camp/Battle calls, add Hub/Encounter |
| `BootSceneBuilder`, `TitleSceneBuilder`, `ThaiTextSpikeSceneBuilder`, `ResultSceneBuilder` | KEEP | four scene builders that survive |
| `BattleSceneBuilder`, `CampSceneBuilder` | DROP | their scenes go |
| `BattleCameraProbe.cs`, `BattleCaptureState.cs` | GENERALIZE | rename to `CameraProbe` / `CaptureState`, drop battle-only probes (keep the iso-camera capture) |
| `UiCaptureTool.cs`, `UiLayoutCheck.cs` | KEEP + edit | capture presets name Battle/Camp; repoint at Hub/Encounter |
| `WebBuildPipeline.cs`, `WebSizeProfiles.cs` | KEEP | the payload gate (ADR 0011) |

### 2.3 `Assets/Main/Scenes/` — 6 scenes

`Boot`, `Title`, `Result`, `ThaiTextSpike` — **KEEP** (Result edited to generic fields).
`Battle`, `Camp` — **DROP**; **NEW** `Hub`, `Encounter` (built by `ProjectSetup` only, never hand-written YAML).
`ProjectSettings/EditorBuildSettings.asset` lists all six today and must list the new six.
Scenes reference scripts by GUID (`m_Script`: Battle 71, Camp 80, Result 20, Title 13, ThaiTextSpike 11,
Boot 2) and also carry the type name as text in `m_EditorClassIdentifier` (Boot:
`ProjectSurvive.Game.Presentation::…SceneBoot`) — so a renamed type shows as an empty identifier, and a
deleted script as Missing Script. Both are repaired by re-running the builders, which is why T1 step 1 comes
before the scene work.

### 2.4 `Assets/Main/` assets

`Fonts/` (5: `Sarabun-Regular.ttf`, `Sarabun-Bold.ttf`, `Sarabun-Regular SDF.asset`,
`Sarabun-Regular FontAsset.asset`, `OFL.txt`) **KEEP** · `UI/` (2) **KEEP** · `Settings/` (2 URP assets)
**KEEP** · `Plugins/WebGL/ProjectSurviveWeb.jslib` **KEEP** · `Resources/Config/` — **REGEN** (the folder is
**empty right now**: the 14 `.bytes` are untracked build output and the 14 `.meta` are among the 156
deletions, so a player built from this tree has no config at all).
`Assets/Resources/Fonts/` (2: `THSarabunPSK.ttf` + SDF) **KEEP** — the Thai spike's font asset.

### 2.5 `Assets/` outside `Main/`

`NuGet/` **KEEP** · `Packages/` (11 NuGet packages) **KEEP** · `Resources/` **KEEP** ·
`csc.rsp`, `packages.config`, `NuGet.config`, `DefaultVolumeProfile.asset`,
`UniversalRenderPipelineGlobalSettings.asset` **KEEP**.
`TextMesh Pro/Fonts`, `Resources`, `Shaders`, `Sprites` — **KEEP**.
`TextMesh Pro/Examples & Extras` — **DROP**: 34 `.cs` with **no `.asmdef`**, so they compile into
`Assembly-CSharp` on every change; grep over `Assets/Main` and `ProjectSettings` finds **zero** references,
and no example scene is in `EditorBuildSettings` (0 matches).

### 2.6 `Assets/Main/Tests/`

EditMode (18 `.cs` + asmdef): **KEEP** `ShellSmokeTests`, `TitleScreenTests`, `TitleScreenLayoutTests`,
`ResultScreenLayoutTests`, `UiLayoutCheckTests`, `WebGlBridgeTests`, `WebSizeProfilesTests`; **GENERALIZE**
`ResultScreenTests`, `ResultReportTests`, `RunFlowEditModeTests`, `RunScopeLifetimeTests`;
**DROP** `BackpackRulesTests`, `BattleMiniBagTests`, `BattlePresentationTests`, `CampPresenterTests`,
`CampTooltipPlacementTests`, `DevCheatTests`, `DevMenuReportTests`.
PlayMode (9 `.cs` + asmdef): **KEEP** `ShellPlayModeTests`, `SceneFlowHarness`, `TitleScenePlayModeTests`;
**GENERALIZE** `SceneFlowPlayModeTests`, `ResultScenePlayModeTests`; **NEW** the 20-round Hub⇄Encounter scope
test and the "back to Title disposes the run" test; **DROP** `BattlePlayModeTests`,
`CampBagDragPlayModeTests`, `CampMergeChooserPlayModeTests`, `CampWorldPlayModeTests`.

### 2.7 `ProjectSettings/` — 24 files — **KEEP**

Edits only: `EditorBuildSettings.asset` (scene list) and, if the product name changes, `ProjectSettings.asset`
— the project keeps the name ProjectSurvive per the shared brief.

---

## 3. `data/` and `proto/`

### 3.1 `data/tables/` — 13 `.csv`

| File | Verdict | Why |
| --- | --- | --- |
| `constant.csv` | GENERALIZE | the singleton row is generic in *shape* but game-shaped in *fields* (`board_width`, `max_speed_penalty_percent`, `weight_speed_percent_per_unit`, `pickup_radius`, `max_hp`, `max_mp`, `max_tier`, `upgrade_choice_count`, …) → keep a template constant set (see Q8) |
| `sample.csv` | **NEW** | the brief keeps a sample table; it does not exist yet (Q8) |
| `bag.csv`, `item.csv`, `skill.csv`, `synergy.csv`, `merge_recipe.csv`, `upgrade_variant.csv`, `upgrade_variant_modifier.csv`, `enemy.csv`, `floor.csv`, `wave.csv`, `shop_pool.csv`, `economy.csv` | DROP | 12 game tables; each drags its schema, enums, generated C#, config rows and validator rules |

### 3.2 `data/Defines/`

`__tables__.csv` **GENERALIZE** — 14 registry rows → `TbConstant`, `TbLocalization`, `TbSample`.
`schema.xml` **GENERALIZE** — keep the `Language` enum and the `Constant`/`Localization`/`Sample` beans; the 7
game enums and 12 beans (`Bag`, `Item`, `Skill`, `Synergy`, `MergeRecipe`, `UpgradeVariant`,
`UpgradeVariantModifier`, `Enemy`, `Floor`, `Wave`, `ShopPool`, `Economy`) go. The file's header comment
(currently "ProjectSurvive static data schema (Stage 02)") must stop citing the stage.

### 3.3 `data/localization/localization.csv` — 193 lines

**GENERALIZE** — every key of a dropped screen (bag, shop, synergy, battle HUD, merge chooser, tooltip,
dev menu) goes; the shell, title, result, and the new hub/encounter keys stay. Player-facing text stays in
this file only (AGENTS rule 5); no string literals in scenes.

### 3.4 `data/luban.conf` — **KEEP**

Groups, schema files, `dataDir`, target `client` / manager `Tables` / `topModule` `ProjectSurvive.Config` are
all generic. No edit, even though the table list behind `__tables__.csv` shrinks.

### 3.5 `proto/app.proto` — **GENERALIZE**

`Platform`, `AppInfo`, `ClientHello`, `ServerHello` are generic. `RunReport` is one field away from generic:
`highest_floor` (line 51) and `completed` describe the dropped run. Rename to `encounters_cleared` and
`outcome`, then regenerate (never hand-edit `lib/Runtime/Generated/Proto/App.cs`). `proto/.gitkeep` KEEP.

---

## 4. `tools/`

| Path | Verdict | Why |
| --- | --- | --- |
| `gen.ps1` | GENERALIZE | drives Luban + protoc; reads `__tables__.csv`, so it shrinks by itself — but its report mentions the 14 tables |
| `validate-data.ps1` | GENERALIZE | table list + rule names |
| `check-fixtures.ps1` | GENERALIZE | fixture list (2 remain) |
| `verify-generated.ps1` | KEEP | diff check, table-agnostic |
| `test.ps1` | GENERALIZE | suite doc-comment and step names: the `sim: streams` step matches `shop/battle/visual` digest labels (lines 333–349), the browser step names `smoke + title + battle + camp + result` (line 543) |
| `sim.ps1` | GENERALIZE | bot modes (`bot`, `camp`) go with the bots |
| `doctor.ps1` | GENERALIZE | it checks the Unity/Web modules and tool versions, but its `$expectedDirs` (line 289) names `docs\gdd` and its `$expectedFiles` (line 301) names `docs\gdd\prototype-v0.3.md` — both leave in T5, so doctor would FAIL on the template. **No EcsLite check exists** to remove |
| `build-web.ps1` | KEEP | scene-agnostic; reads the build settings' scene list |
| `setup-tools.ps1`, `serve-web.mjs`, `measure-web-size.mjs`, `measure-il2cpp-size.mjs`, `coverage-report.mjs`, `font-metrics.py`, `screen-text-metrics.py`, `reference/xoshiro128_reference.py` | KEEP | toolchain, measurement and reference implementations — generic |
| `versions.lock.json` | GENERALIZE | remove `packages[EcsLite]` (line 125); update `updatedBy` |
| `vendor/` (luban 5.1.0, protoc 33.6) | KEEP | the data pipeline's pinned toolchain; `.vendor` is not committed |
| `rename.ps1`, `check-links.mjs` | **NEW** | the two tools the plan asks for: project rename, and a link checker over `docs/` + `llmwiki/` |

---

## 5. `tests/web/`

| Path | Verdict | Why |
| --- | --- | --- |
| `globals.mjs` | KEEP | 3 page globals only (`Smoke`, `Scene`, `ThaiSpike`); verified against `WebGlBridge` |
| `smoke.spec.ts` (153) | KEEP | boot self-check |
| `title.spec.ts` (263) | KEEP + retarget | presses the start control; after T1 it must land in `Hub`, not `Camp` |
| `screens.spec.ts` (292) | GENERALIZE | photographs 6 scenes by name (`Battle`, `Camp`) → the new 6 |
| `battle.spec.ts` (216), `camp.spec.ts` (1216), `result.spec.ts` (362) | DROP | 1794 lines clicking deleted HUD; replaced by `loop.spec.ts` |
| `loop.spec.ts` | **NEW** | Title → Hub → Encounter → Result → Title with real clicks |
| `scripts/collect-geometry.mjs`, `scripts/probe-boot.mjs` | KEEP | geometry collection + boot probe, both name-agnostic |
| `playwright.config.ts`, `package.json`, `package-lock.json`, `.gitkeep` | KEEP | |

---

## 6. `docs/` and the repository root

| Path | Verdict | Why |
| --- | --- | --- |
| `adr/0001`–`0006`, `0008`, `0011`, `0012`, `0022`, `0024` (11) | KEEP | repository/artifact conventions, thin MVP UI, perf baseline, layer enforcement, data pipeline, config port, Thai UI stack, payload budget, Prev/Cur buffer, title-starts-a-run, Shared generic buffers |
| `adr/0007`, `0009`, `0010`, `0013`–`0021`, `0023` (13) | MOVE → `docs/template/examples/` | battle contract, footprint, item effects, weight curve, bag editing, synergy kinds, run settlement, EcsLite, battle camera, camp read model, bag moves carry items, merge choice, cooldown on the frame |
| `architecture/overview.md`, `architecture/dependencies.md` | GENERALIZE | layer diagram + dependency table (EcsLite row and its 4 mentions go) |
| `build/size-baseline.md` | GENERALIZE | the numbers come from a Survive build; keep the method, re-measure a template build |
| `testing/test-suites.md` | GENERALIZE | suite membership changes (bots, camp/battle specs gone, loop added) |
| `gdd/prototype-v0.3.md` | DROP | the game's design document |
| `stages/` (24 files: `00`…`10t` plus `process-fast-full-report.md`) | DROP | stage reports of the game; replaced by `stages/_TEMPLATE.md` |
| `adr/.gitkeep`, `build/.gitkeep`, `testing/.gitkeep` | KEEP | |

### 6.1 Repository root (files the brief's folder list does not name)

| Path | Verdict | Why |
| --- | --- | --- |
| `AGENTS.md` | GENERALIZE | all 14 rules are project-agnostic in *intent*; the game-specific wording (rule 5's Thai-text examples, rule 6's 30 Hz/streams, rule 13's fantasy-naming note) needs a template phrasing |
| `knowledge.md` | GENERALIZE | the status table lists stages 00–10t and the "current state" paragraph describes the survivor-like |
| `.editorconfig`, `.gitattributes`, `.gitignore` | KEEP | LFS is configured in `.gitattributes`; `.gitignore` already ignores `artifacts/*` and the Unity folders |
| `.aiignore` | **does not exist** | the plan says GENERALIZE it — there is no such file in the tree (see Q12) |
| `ProjectSurvive GDD v0.3 + Master Prompt + Stage Plan (ฉบับรวม).md` (138 KB) | MOVE → `docs/template/examples/` | the game's design document *and* this extraction plan; neither is a template rule |
| `artifacts/` | KEEP | one tracked `.gitkeep`; every report, trace and screenshot under it is ignored |
| `UnityProject/{Library,Logs,Temp,obj,UserSettings}`, `*.sln`, `*.csproj` | ignored | not tracked, nothing to classify |

---

## 7. `llmwiki/`

| Path | Verdict | Why |
| --- | --- | --- |
| `index.md` | GENERALIZE | the map of the wiki; drop the game system entries |
| `glossary.md` | GENERALIZE | keep the generic vocabulary (run, scope, tick, port); drop backpack/synergy/shop/bia/ghost names |
| `stage-log.md` | GENERALIZE | the 3-line-per-stage format stays; the game's entries go |
| `gotchas/determinism-scope.md`, `il2cpp-di-and-reflection.md`, `luban-5-strict-and-tables.md`, `tmp-font-assets-in-builds.md`, `tmp-tryaddcharacters-lies.md`, `webgl-size-profile-settings-residue.md` | KEEP | each is a Unity/Luban/WebGL fact with no game noun in its lesson |
| `gotchas/ecslite-pool-refs.md` | MOVE | the lesson belongs with the engine that left |
| `systems/loop-and-scopes.md`, `systems/unity-shell.md` | KEEP | rewrite to the placeholder loop (scene names, no battle) |
| `systems/backpack.md`, `battle-contract.md`, `battle-presentation.md`, `battle-simulation.md`, `camp.md`, `run-shop.md` | MOVE | game systems; extract any general fact as a gotcha first |
| `systems/data-schema.md` | MOVE | per the plan — but extract the Luban 5.x schema facts already captured in `gotchas/luban-5-strict-and-tables.md` before moving |

---

## 8. `.agents/skills/` — 128 skills

| Group | Count | Verdict | Why |
| --- | --- | --- | --- |
| Unity-MCP tool skills (`assets-*`, `gameobject-*`, `scene-*`, `cinemachine-*`, `inputsystem-*`, `navigation-*`, `profiler-*`, `screenshot-*`, `script-*`, `package-*`, `console-*`, `editor-*`, `reflection-*`, `object-*`, `animation-*`, `animator-*`, `tests-run`, `unity-*`, …) | 121 | KEEP | editor tooling, names no game type |
| Authored outside the MCP set (`chroma-key-portrait-pipeline`, `disciple-portrait-prompt`, `portrait-edit-prompt`, `pordee`, `pordee-stats`, `ponytail`, `typesafe-ai`) | 7 | KEEP | workflow/prompt tooling; the portrait ones are reusable for a character screen. None mentions Survive (verified by grep). `.agents/mcp.json` also KEEP |

---

## 9. Dependencies: surviving file → dropped type → fix

| Surviving file | Refers to | Fix |
| --- | --- | --- |
| `Application/Run/RunSession.cs` | ~25 `Domain.{Combat,Inventory,Run,Shop}` types | rewrite as the scope skeleton (Seed, RandomStreams, EncounterIndex, Outcome, typed `Result`) |
| `Application/Run/ReplayLog.cs` | `RunCommand`, `BattleReplay`, `IBattleSessionFactory`, `RunSession` | keep `(sequence, command)` + seed; delete the replay-through-battle loop |
| `Application/Run/SceneFlow.cs` | `IBattleSession`, `BattleSetup`, `BattlePhase`, `RunFlowState`, `SceneId` | transitions only over the new `SceneId`; no battle session |
| `Contracts/Save/BackpackSnapshotDto/Codec` | `Domain.Inventory` | DROP both files |
| `Infrastructure/Save/MessagePackBackpackSnapshotCodec.cs` | `IBackpackSnapshotCodec`, DTO | DROP; folder empties |
| `Infrastructure/Config/LubanGameConfig.cs` | 14 generated table types, `BagConfig`…`ShopPoolEntryConfig` | keep `TableNames` = `tblocalization`, `tbconstant`, `tbsample`; keep the localization + constant mapping and the duplicate-key check; delete the 7 other dictionaries and their `TryGet*` |
| `Core.Tests/ArchitectureTests` | assembly names incl. `Leopotam.EcsLite`, `Core.Simulation` | rewrite the expected matrix (Shared←Domain←Application←Presentation; Simulation→Shared+Domain) |
| `Core.Tests/{ConfigLoadTests,ConfigFixture,ConfigEnumParityTests}` | 14 generated tables | retarget to `TbConstant` + `TbLocalization` + `TbSample` |
| `Core.Tests/ConfigRulesTests`, `Data.Validator/ConfigValidator` | bag/synergy/merge/wave/shop rules | keep generic rules: duplicate id, missing ref, value range |
| `Core.Tests/RepoLayout` | `data/tables/*`, `docs/stages/*` | update the expected paths |
| `Tests~/Sim/{Program,SimEnvironment,StreamsMode,BenchmarkMode}` | `RunSession`, bots, battle | repoint at the skeleton; delete the bot modes |
| `Unity …/RootLifetimeScope.cs` | `Domain.Run` (`RunScopeOwner`, `RunSession`), `BattleSceneScope`, `CampSceneScope` | keep config/clock/localization/UI state/scene loader/run owner; drop the battle+camp registrations; keep registration style `factory` (il2cpp gotcha) |
| `…/Flow/RunScopeOwner.cs` | `RunSession`, `RunSummary` | keep one run per scope; the summary becomes the generic outcome |
| `…/Flow/RunLifetimeScope.cs` | `RunSession`, `IBattleSessionFactory`, `EcsBattleSessionFactory` | register the skeleton only |
| `…/Flow/SceneLoader.cs` | `SceneId` (`Domain.Run`) | `Application.Flow.SceneId`; "arriving at Title disposes the run" stays |
| `…/Flow/TitleSceneDirector.cs`, `TitlePresenter.cs` | `RunSession.StartRun` | start the skeleton run at the press (ADR 0022 unchanged) |
| `…/ResultPresenter.cs`, `ResultReport.cs`, `ResultUiState.cs`, `ResultView.cs`, `ResultScene/ResultSceneDirector.cs` | `RunSummary`, `RunState`, `RunOutcome` | generic fields: seed, encounters cleared, outcome, HP-free |
| `…/Editor/ProjectSetup.cs` + `BattleSceneBuilder`/`CampSceneBuilder` | battle/camp scene construction | delete the two builders, add `HubSceneBuilder` + `EncounterSceneBuilder` |
| `…/Editor/BattleCameraProbe.cs`, `BattleCaptureState.cs` | battle entities/capture | rename + strip the battle probes |
| `…/Editor/UiCaptureTool.cs` | capture presets `Battle`, `Camp` | repoint at `Hub`, `Encounter` |
| `…/Presentation/Web/WebGlBridge.cs` | **nothing dropped** (verified) | no code change; keep the 3 globals in sync with `tests/web/globals.mjs` and add nothing per scene |
| `Assets/Main/Tests/EditMode/*`, `Tests/PlayMode/*` | classes named in §2.6 | drop the 7 + 4 game classes, adapt the 4 + 2 that read run/result state, add the 2 new PlayMode scope tests |
| `Assets/Main/Scenes/{Battle,Camp}.unity` | 151 script refs between them | delete; regenerate Boot/Title/Result/Hub/Encounter/ThaiTextSpike from the builders |
| `tests/web/screens.spec.ts` | scene names + geometry of 6 scenes | retarget to the surviving scenes |
| `tests/web/{battle,camp,result}.spec.ts` | page globals + game HUD | delete (1794 lines); replaced by `loop.spec.ts` |
| `tests/web/globals.mjs`, `scripts/probe-boot.mjs`, `scripts/collect-geometry.mjs` | the 3 page globals only | no change needed (verified) |
| `tools/{gen,validate-data,check-fixtures,verify-generated}.ps1`, `sim.ps1`, `test.ps1` | table names / fixtures / suites | update the table list and the fixture list; `test.ps1`'s browser step names the specs and its `sim: streams` step matches `shop/battle/visual` |
| `tools/doctor.ps1` | `docs/gdd`, `docs/stages`, `docs/gdd/prototype-v0.3.md` in its expected-dir/file arrays | drop the `docs\gdd` entry and re-point the expected doc to `docs/stages/_TEMPLATE.md` |
| `docs/architecture/*`, `docs/testing/test-suites.md`, `docs/build/size-baseline.md` | game systems and counts | rewrite (T5) |

---

## 10. Cut order (callers before callees)

1. **Tests and specs that name game types** — EditMode/PlayMode/Battle·Camp classes, `battle/camp/result.spec.ts`,
   the three bot modes, `UpgradeVocabularyTests`, `EcsLiteSmokeTests`. Deleting these first keeps every later
   step compilable on its own.
2. **Unity scene layer** — `Scenes/Battle|Camp`, `Presentation/{Battle,Camp}`, `Scripts/{Battle,Camp}`,
   `BattleSceneBuilder`, `CampSceneBuilder`; re-run the builders so the surviving scenes stop pointing at them.
3. **Unity shell edits** — `RootLifetimeScope`, `Flow/*`, Result, Title, `ProjectSetup`, probes, capture presets.
4. **Application** — `Application/Battle/Fake`, then the three `Application/Run` files.
5. **Simulation** — `Simulation/Battle/**`, leaving the asmdef and the marker.
6. **Domain** — `Domain/{Combat,Inventory,Run,Shop}` (already gone), then `Domain/Config` trim.
7. **Contracts/Infrastructure** — `BackpackSnapshot*`, then `Infrastructure/Save`.
8. **Vendored + tooling** — `third_party/ecslite`, `Layers/Leopotam.EcsLite.csproj`, the two csproj references,
   `versions.lock.json`, `dependencies.md`.
9. **Data** — 12 tables and `proto/app.proto` content, then `gen.ps1` (REGEN) and the validators.
10. **Docs** — `docs/gdd`, `docs/stages/*`, ADR move, `AGENTS.md`, `knowledge.md`, `llmwiki/*`.

Order matters for one reason only: step 1 and 2 are what make the tree compile at every intermediate commit;
the rest is independent.

---

## 11. Tests: what disappears and why, what must survive

**Disappears (13 classes + 3 specs).** `BackpackRulesTests`, `BattleMiniBagTests`, `BattlePresentationTests`,
`CampPresenterTests`, `CampTooltipPlacementTests`, `DevCheatTests`, `DevMenuReportTests`,
`BattlePlayModeTests`, `CampBagDragPlayModeTests`, `CampMergeChooserPlayModeTests`, `CampWorldPlayModeTests`,
plus in dotnet `{Battle,Inventory,Run}` test folders — each asserts a rule of a system that no longer exists.
`battle.spec.ts`, `camp.spec.ts`, `result.spec.ts` — 1794 lines of clicks on deleted UI.

**Must survive, in some form.** `Tests~/Layers` (ADR 0004 — never deleted), `ArchitectureTests`
(layer directions + "no UnityEngine in Core"), `Xoshiro128Tests` / `RandomStreamsTests` / `SplitMix32`
coverage (determinism + stream separation), `FixedTickerTests`, `ResultTests`/`Error`,
`ConfigLoadTests`/`ConfigEnumParity`/`Data.Validator` (data gate), `ProbeSerializationTests`
(MessagePack AOT), `ShellSmokeTests`/`ShellPlayModeTests` (DI + boot), the Thai spike PlayMode test,
`RunScopeLifetimeTests` + the new 20-round loop test (the most valuable tests of stage 10),
and the browser suite `smoke` / `title` / `screens` — plus the new `loop`.

---

## 12. Placeholder loop (draft)

**Boot → Title → Hub ⇄ Encounter → Result → Title**

| Scene | Contents (all text from localization, all scene changes through `SceneLoader`) |
| --- | --- |
| `Boot` | `SceneBoot` + `RootLifetimeScope`, `PersistAcrossScenes()`, then `TryGo(Title)`. No run. |
| `Title` | title + one button `ui.title.start`; on press **`RunScopeOwner.RequireRun()`** (the run scope is born here, ADR 0022) then `TryGo(Hub)`. |
| `Hub` | `ui.hub.start_encounter` → `RunSession.BeginEncounter()` then `TryGo(Encounter)`; `ui.hub.end_run` → `TryGo(Result)`. Shows `EncounterIndex`. |
| `Encounter` | a counter driven by `FixedTicker` (ticks, not `Time`), `ui.encounter.win` → `RunSession.EndEncounter(Won)` → `TryGo(Hub)`; `ui.encounter.lose` → `EndEncounter(Lost)` → `TryGo(Result)`. |
| `Result` | outcome, encounters cleared, seed; `ui.result.back_title` → `TryGo(Title)`, where arriving disposes the run scope. |

Typed results everywhere: `SceneLoader.TryGo` returns `Result` (`transition.in_progress`,
`scene_missing`, `load_failed`, `disposed`); `BeginEncounter` / `EndEncounter` return `Result` /
`Result<EncounterOutcome>` and refuse out of order with `ErrorKind.InvalidState` — a refused command leaves
the state untouched (AGENTS rule 7). Every scene publishes `__projectSurviveScene`, so `loop.spec.ts` can
click what the screen drew rather than a hard-coded coordinate.

---

## 13. Game tokens and the grep that proves they are gone

```
# DoD scope: 0 hits expected in each of these three commands (Generated excluded)
git grep -n -i -E "backpack|synergy|\bshop\b|\bbia\b|footprint|\bwave\b|\bfloor\b" \
  -- 'lib/Runtime/*' ':!lib/Runtime/Generated/*'
git grep -n -i -E "backpack|synergy|\bshop\b|\bbia\b|footprint|\bwave\b|\bfloor\b" \
  -- 'UnityProject/Assets/Main/Scripts/*'
git grep -n -i -E "backpack|synergy|\bshop\b|\bbia\b|krasue|nangtani|pob|ecslite|leopotam" -- 'tests/*'

# Wider sweep before closing T1 (data + docs are allowed to lag until T5)
git grep -n -i -E "ecslite|leopotam" -- lib tools docs UnityProject/Assets
git grep -n -i -E "krasue|nangtani|pob" -- data/
```

Baseline measured on the current tree (2026-10-18, case-insensitive match counts):

| Token | `lib/Runtime` (no Generated) | `Assets/Main/Scripts` | `tests/` |
| --- | --- | --- | --- |
| Backpack | 74 | 42 | 5 |
| Bag | 105 | 593 | 41 |
| Synergy | 23 | 43 | 0 |
| Shop | 58 | 68 | 17 |
| Bia | 21 | 126 | 27 |
| EcsLite | 2 | 6 | 0 |
| Leopotam | 1 | 0 | 0 |
| Footprint | 53 | 14 | 0 |
| Wave | 8 | 48 | 3 |
| Floor | 133 | 372 | 72 |
| Krasue / NangTani / Pob | 0 | 0 | 0 — 11 matches live in the data instead: `enemy.csv` (2), `floor.csv` (1), `wave.csv` (4), `localization.csv` (4) |

`Bag` and `Floor` need a case-sensitive re-read: `bag` also matches identifiers such as `TbBag`, and `floor`
matches `MathF.Floor` — the T1 check should use the `\b`-anchored forms above and treat a remaining
`MathF.Floor` as a false positive.

---

## 14. Pre-flight checks (prompt item 6)

| Check | Answer |
| --- | --- |
| **asmdef references: names or GUIDs?** | **Names.** Every `references` array holds assembly names (`"ProjectSurvive.Core.Shared"`, `"Leopotam.EcsLite"`), never `GUID:…`. So renaming an assembly breaks compile loudly, and deleting `Leopotam.EcsLite` needs only the one line in `Simulation.asmdef`. |
| **`.unity`/`.prefab`/`.asset` storing type names as text** | **Yes, in every scene** via `m_EditorClassIdentifier: <assembly>::<type>` (Boot has `…SceneBoot`, `…RootLifetimeScope`). No `SerializeReference` anywhere in `lib/` or
`Assets/Main`. Prefabs: **none** in `Main` (0 `.prefab`); the repository's only three `.prefab` files live in
`TextMesh Pro/Examples & Extras`, which is another reason that folder can go. The only `.asset` naming the
project is the two URP settings assets, which are ours and stay. |
| **`link.xml`** | **None** in the repository (only `Library/` would hold generated ones). IL2CPP stripping is therefore governed by the Unity defaults + the MessagePack source generator; the Result scene's DI is the known open risk in `docs/stages/10-report.md`. |
| **`InternalsVisibleTo`** | **None** in `lib/`, `tools/` or `Assets/`. `CheckpointProbe`'s `Record` helpers are `internal` but only called from inside the assembly. |
| **Git LFS** | The repo uses LFS for binaries: the two Sarabun `.ttf`, the NuGet-for-Unity DLLs/icon, and the imported `Assets/Packages/**` DLLs + icons. **No game asset is in LFS** — nothing to migrate or purge for the template. |
| **TMP `Examples & Extras` referenced?** | **No** — zero references from `Assets/Main` or `ProjectSettings`, no `.asmdef` (so its 34 scripts compile into `Assembly-CSharp`), and no example scene in the build list. Safe to DROP. |
| **Custom skills bound to Survive** | `.agents/skills` holds **128** skills; **none** mention ProjectSurvive, backpack, survivor, พ่อหมอ, krasue or ย่าม (grep over every `SKILL.md` = 0 hits). **121** are the Unity-MCP tool set (`assets-*`, `gameobject-*`, `scene-*`, `profiler-*`, …); the other **7** are authored elsewhere — `chroma-key-portrait-pipeline`, `disciple-portrait-prompt`, `portrait-edit-prompt`, `pordee`, `pordee-stats`, `ponytail`, `typesafe-ai` — portrait/workflow tooling, not game rules. KEEP all, as the table says. |

---

## 15. Decisions only the user can settle

- ~~**Q1 — the working path.**~~ **Closed:** the shared brief now names `C:\Workspace\ProjectSurvive-template`,
  which is this worktree.
- **Q2 — the 156 uncommitted deletions.** T0's DoD says only `docs/template/` changes, but the tree already
  deletes 156 `lib/` files (and `Resources/Config`'s 14 `.meta`). Options: **(a)** restore them
  (`git checkout -- .`) so T0 starts from a clean base and T1 deletes with the inventory in hand — my
  recommendation; **(b)** commit them as a prep commit `generic: drop lib game systems`; **(c)** leave them
  and accept that T0's DoD cannot be met literally.
- **Q3 — `RandomStreamKind`.** `Run / Encounter / Reward / Visual` (as briefed) or three real streams plus a
  note? Today's fourth member (`Upgrade`) is documented as *derived, never consumed* (ADR 0021), so calling it
  `Reward` would make it a stream by name and a derivation by behaviour.
- **Q4 — where `SceneId` lives.** `Application/Flow/SceneId.cs` (recommended: Domain need not name a scene) or
  `Shared/SceneId.cs` (Presentation could read it without Application).
- **Q5 — `Infrastructure/Save` dies** with the backpack codec; only `CheckpointProbeCodec` (in Contracts)
  remains. Accept, or keep a `SaveDirectory`-style port?
- **Q6 — T1 vs T2 boundary for data.** The brief has T1 trim `IGameConfig` to constant+localization while the
  14 tables still exist (step 5) and T2 delete the tables and REGEN. That leaves `lib/Runtime/Generated/`
  containing 39 files for 12 unused tables between the two steps. Accept the transient, or move the REGEN into
  T1's tail?
- **RESOLVED in T2 — the two surviving fixtures** (`duplicate-id`, `missing-ref`) both target dropped tables
  (`item.csv`, `bag.csv`). Retarget them at the new `sample` table, or keep the fixture *shapes* and delete the
  rows? (The brief says keep both; the ids inside them are game ids.)
- **RESOLVED in T2 — `sample.csv`.** The brief keeps `constant.csv` + `sample.csv`; `sample.csv` does not exist today. What
  should it demonstrate — one row of a generic `TbSample` (id, int, float, string, localization key), or a
  second generic table?
- **Q9 — `Hub` / `Encounter` scene names.** Keep these names (brief) — confirm they are the template's names
  and not placeholders for Jianghu's `Rest`/`Bracket`/`Arena`.
- **Q10 — ADR 0024** (`Shared` generic buffers) is not in the KEEP/MOVE lists; it names no game type, so I
  recommend KEEP. Confirm.
- **Q11 — `RunReport` in `proto/app.proto`.** Rename `highest_floor`/`completed` to
  `encounters_cleared`/`outcome` and regenerate (recommended, nothing consumes the field yet), or leave the
  wire shape alone and document it as legacy?
- **Q12 — `.aiignore`.** The plan lists it as GENERALIZE, but it does not exist; create one for the
  template (Unity `Library/`, `artifacts/`, `tools/vendor/`) or drop the row?

---

## 16. T0 definition-of-done status

| Criterion | Status |
| --- | --- |
| `inventory.md` covers every top-level folder | **PASS** — `lib/`, `UnityProject/Assets/` (+`ProjectSettings`), `data/`, `proto/`, `tools/`, `tests/web/`, `docs/`, `llmwiki/`, `.agents/skills/` |
| No general test fell into DROP | **PASS** — Layers, Architecture, RNG, FixedTicker, Result, Config, serialization probe, shell smoke, Thai, scope loop, smoke/title/screens are all KEEP/GENERALIZE; see §11 |
| Every surviving file that touches a dropped type has a fix written | **PASS** — §9, 29 rows |
| Questions at the end answered before T1 | **OPEN** — Q2…Q12 (Q1 closed) |
| `git status` changes only in `docs/template/` | **FAIL (pre-existing)** — 156 deletions + 1 untracked fixture dir arrived with the worktree; §15 Q2 |
