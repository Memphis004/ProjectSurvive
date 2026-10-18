# ProjectSurvive template — architecture overview

> Status: after **T2** (data pipeline), docs pass T3. This document describes the
> template as it exists in the tree, not a target state: every file it names is in
> the repository. A game built from the template keeps these boundaries and replaces
> the placeholder content.

## 1. Goal of the architecture

Ship a **genre-free skeleton** that runs in a desktop browser (WebGL2,
single-threaded) with **all rules in pure C#**, so they can be tested with
`dotnet test` without opening Unity.

Two consequences drive almost every decision below:

1. **The rule engine must not depend on Unity.** It lives in `lib/`, a local UPM
   package, and is compiled and tested from a plain `dotnet` solution as well as by
   the Editor.
2. **The rule engine must not depend on serialization or DI frameworks.** Domain and
   Simulation stay free of `R3`, `VContainer` and serializers so the rules stay
   portable and cheap to test.

## 2. Layers and direction of dependency

Arrows point from a layer to what it is allowed to reference.

```
                       (nested inside lib/, a local UPM package)
   ┌───────────────────────────────────────────────────────────────────────┐
   │                                                                       │
   │   Shared  ◄──  Domain  ◄──  Application  ◄──  Presentation            │
   │                    ▲                              ▲                   │
   │                    │                              │                   │
   │              Simulation                      (Unity, in UnityProject) │
   │             ────────────                                              │
   │             references: Shared, Domain                                │
   │                                                                       │
   │   Infrastructure ──► implements Application ports                     │
   │   Contracts      ──► standalone DTOs (MessagePack / Protobuf)         │
   │   Generated      ──► machine output (Luban, protoc). Never edited.    │
   └───────────────────────────────────────────────────────────────────────┘
```

| Layer | May reference | Must NOT reference |
| --- | --- | --- |
| `Shared` | nothing but BCL | UnityEngine, R3, VContainer, serializers |
| `Domain` | Shared | UnityEngine, R3, VContainer, serializers |
| `Application` | Shared, Domain | UnityEngine, R3, VContainer, serializers, Infrastructure |
| `Simulation` | Shared, Domain | UnityEngine, R3, VContainer, serializers |
| `Infrastructure` | Shared, Domain, Application, serializers | UnityEngine |
| `Contracts` | MessagePack / Protobuf attributes only | Domain, Application |
| `Generated` | generated types only | hand-written code |
| `Presentation` (UnityProject) | Application, Infrastructure, Unity | must not compute prices, combos, damage |

**`Domain`, `Application` and `Simulation` must not use `UnityEngine`.**
**`Domain` and `Simulation` must not use `R3`, `VContainer` or any serializer.**

Enforcement:

- **In Unity:** one `.asmdef` per layer in `lib/Runtime/<Layer>/` with explicit
  `references` and `noEngineReferences: true` where the layer must stay engine-free.
- **In `dotnet`:** one `.csproj` per layer in `lib/Tests~/Layers/`, each globbing
  `lib/Runtime/<Layer>/**/*.cs`, so the harness and Unity compile one source set and
  a wrong-direction reference is a compile error in both. Library projects target
  `netstandard2.1` with `LangVersion 9.0` to mirror the Unity 6.3 compiler surface.
  Decision: [ADR 0004](../adr/0004-dotnet-layer-enforcement.md).
- **Guard tests** (`lib/Tests~/Core.Tests/ArchitectureTests.cs`) assert what a
  compiler cannot: that every `.asmdef` and `.csproj` matches the matrix above, that
  no built assembly references `UnityEngine`/`UnityEditor`, that no `lib/Runtime`
  source names `UnityEngine`, and that the rule engine avoids `System.Random`,
  `DateTime.Now` and `Stopwatch` (AGENTS rule 6).
- **Entry point:** `tools/test.ps1` (build → `dotnet test` → Sim → data gates).

### Why these boundaries exist

- **Generated types never leak into Domain.** Luban rows are read through an
  `IGameConfig` adapter implemented in Infrastructure. Domain sees its own plain
  types, not generated ones — enforced by a guard test over the built assemblies plus
  the domain/generated enum parity test. Decision:
  [ADR 0006](../adr/0006-config-port-and-generated-code-isolation.md).
- **Presentation is thin MVP.** A View holds no logic; a Presenter calls Application
  use cases only. See [ADR 0002](../adr/0002-thin-mvp-ui-approach.md).
- **The shell is testable from outside the Editor.** A WebGL build cannot run Unity's
  test framework, so the player publishes its own verdicts as page globals
  (`__projectSurviveSmoke`, `__projectSurviveScene`, `__projectSurviveThaiSpike` and
  one document per screen) and Playwright asserts on them in the target browser. See
  [`llmwiki/systems/unity-shell.md`](../../llmwiki/systems/unity-shell.md).
- **One Editor per project.** Batch-mode tooling (`unity test`, `unity build`,
  `tools/build-web.ps1`) requires the interactive Editor to be closed; with it open,
  drive the same operations through its pipeline. `UnityProject/SETUP.md` §5 records
  both paths.

## 2b. The static data pipeline

```
   data/                                  (the ONLY place a balance number lives)
     Defines/schema.xml        enums + beans
     Defines/__tables__.csv    table registry
     tables/*.csv              one file per table
     localization/*.csv        every player-facing string, th + en
          │
          │  tools/gen.ps1   (Luban 5.1.0 --strict, protoc 33.6; cwd-independent)
          ▼
   lib/Runtime/Generated/Luban/**/*.cs    tracked  →  compiled by Unity + dotnet harness
   lib/Runtime/Generated/Proto/App.cs      tracked  →  protoc contract, one copy only
   lib/Tests~/data/*.bytes                 generated →  dotnet tests + Data.Validator
   UnityProject/Assets/Main/Resources/Config/*.bytes   generated →  the Web build
          │
          ▼
   ProjectSurvive.Core.Infrastructure.LubanGameConfig : IGameConfig   (Domain port)
          │
          ▼
   Domain / Application / Simulation      (plain domain types, no generated code)
```

The template ships three tables: `constant` (one singleton row of tuned numbers),
`localization` (every player-facing string, keyed) and `sample` (a deliberately
disposable table that exercises both reference mechanisms — a required `#ref` and an
optional localization key). Rules and conventions:
[`llmwiki/systems/data-schema.md`](../../llmwiki/systems/data-schema.md).
Decisions: [ADR 0005](../adr/0005-data-pipeline-shape-and-format.md),
[ADR 0006](../adr/0006-config-port-and-generated-code-isolation.md).

| Gate | Tool | What it proves |
| --- | --- | --- |
| generate | `tools/gen.ps1` | Luban + protoc accept `data/`; writes code, both byte sets, the config hash |
| validate | `tools/validate-data.ps1` | the rules a schema cannot express hold (`lib/Tests~/Data.Validator`) |
| reproduce | `tools/verify-generated.ps1` | a fresh generation matches what is committed, byte for byte |
| reject | `tools/check-fixtures.ps1` | every deliberately broken fixture under `lib/Tests~/fixtures` fails, for the declared reason |

All four run inside `tools/test.ps1` (`-NoPipeline` skips them).

## 3. Three data lifetimes

| Scope | Lifetime | Owns |
| --- | --- | --- |
| **App** | until the game closes | config, localization, clock, the scene loader, the run's owner |
| **Run** | from the title starting a run until arriving back at the title | the seed, its random streams, the encounter counter, the command log |
| **Scene** | until the scene is left | that scene's presenters, UI state and subscriptions |

`RunSession` must **not** be recreated every time the player returns to the Hub.
Arriving at `Title` disposes the Run scope — that is a property of the transition,
not something each screen remembers — and every subscription a scene created is
disposed with the scene.

The `App` scope is `RootLifetimeScope`, moved into `DontDestroyOnLoad` by Boot and
made the parent of every scene scope that loads afterwards. A scene scope that finds
a parent does not rebuild the shell. The Run scope is `RunLifetimeScope`, created and
disposed by `RunScopeOwner` only. Decision:
[ADR 0022](../adr/0022-the-title-starts-a-run.md).

## 4. Command → event flow

Every state-changing operation is **atomic** and returns a **typed result**. If it
fails, state does not change.

```
   UI / Presenter
        │  command (typed)
        ▼
   Application use case ──► validate ──► mutate state as one unit ──► typed result
        │
        ▼
   Domain state changed  ──► a presentation event, if the screen needs one

   Commands : Start, BeginEncounter, EndEncounter, EndRun, Reset   (RunSession)
   Scenes   : SceneFlow.TryGo / CanGo — the loop's legal edges
```

The template's run is deliberately a **state holder**, not a rules engine: it owns
the seed, the streams, the encounter counter and the replay log, and a game adds its
own commands next to these. A refused command leaves the run exactly as it was —
including its replay log (`RunErrors.*` are stable codes, never sentences).

A game built from the template adds its systems to `Domain` and calls them from
`Application`; Presentation reaches those through its Presenter only (AGENTS rule 10).

## 5. Determinism rules

- Simulation runs at a **fixed 30 Hz tick** (`FixedTicker`).
- All randomness goes through `IRandomSource`. Streams are separated:
  **run / encounter / reward / visual** (`RandomStreams`, `RandomStreamKind`).
- Core code must not use `UnityEngine.Random`, `Time` or `DateTime.Now`.
- Determinism is guaranteed **within the same runtime only** — not across browsers or
  CPUs. See [`llmwiki/gotchas/determinism-scope.md`](../../llmwiki/gotchas/determinism-scope.md).

`Simulation` is an empty, guarded layer in the template: it compiles, it is on the
right side of the layer matrix, and it holds a marker type. A game that needs an
engine (EcsLite or otherwise) adds it there, behind `Application`'s ports.

## 6. Scene and UI technology

- Unity 6.3 + URP; uGUI + TextMeshPro for player UI, with **Static** baked font
  assets ([ADR 0008](../adr/0008-thai-text-ui-stack.md)).
- **Scenes are created through the Unity Editor API** (or a verified template), never
  by hand-writing `.unity` YAML. Each scene has one builder under
  `UnityProject/Assets/Main/Scripts/Editor/` (`BootSceneBuilder`, `TitleSceneBuilder`,
  `HubSceneBuilder`, `EncounterSceneBuilder`, `ResultSceneBuilder`,
  `ThaiTextSpikeSceneBuilder`) with its own menu path, so adding a scene never means
  editing a 2 000-line setup file.
- `SceneBoot` reaches an allow-listed scene from the page URL (`?scene=<name>`), which
  is how a browser test enters a screen without playing through the loop.
- A scene that is on screen publishes its own name as `__projectSurviveScene`; a
  WebGL player cannot run Unity's test framework, so a browser test can only see what
  a scene volunteers. See [`llmwiki/gotchas/scene-must-publish-name.md`](../../llmwiki/gotchas/scene-must-publish-name.md).
- UI framework is a thin, project-owned MVP until a concrete `UI.MVP Lite` package is
  identified — see [ADR 0002](../adr/0002-thin-mvp-ui-approach.md).

## 7. What is here, and what is not

**Here, and measured:**

| Area | What the template ships |
| --- | --- |
| `Shared` | `IRandomSource` / `Xoshiro128` / `RandomStreams`, `IClock` / `SystemClock`, `FixedTicker`, `EventRing<T>`, `PrevCurBuffer<T>`, `Result` / `Error` |
| `Domain` | the config types (`IGameConfig`, `ConstantConfig`, `LocalizationEntry`, `Language`) |
| `Application` | `RunSession` + `SceneFlow` + `ReplayLog`, `SceneId`, `LocalizationService` |
| `Infrastructure` | `LubanGameConfig` (the `IGameConfig` adapter), `SystemClock` |
| `Contracts` | `CheckpointProbe` and its codec — the MessagePack source-generator probe |
| data | three tables, the schema, six broken-data fixtures |
| Unity | seven scenes, the composition root, the three scopes, the 7-probe smoke suite, the page bridge, the Web build pipeline |
| browser | `tests/web/{smoke,title,loop}.spec.ts` against a real Development build |

**Not here (yet), by design:**

| Concern | Where it belongs |
| --- | --- |
| a genre, a combat loop, content tables | the game |
| an ECS engine | the game, in `Simulation` |
| a save format with migrations | the game (`Contracts/Save` holds only the probe) |
| a scene capture / UI layout tool | the game, when it has screens worth measuring (the game-era one left with the screens) |
| CI | a game's decision; every gate is a local command today |

## 8. Related documents

- Dependency matrix & risk: [`dependencies.md`](dependencies.md)
- Test suites: [`../testing/test-suites.md`](../testing/test-suites.md)
- Template overview and upgrade path: [`../template/README.md`](../template/README.md)
- Decisions: [`../adr/`](../adr/)
- Glossary: [`../../llmwiki/glossary.md`](../../llmwiki/glossary.md)
