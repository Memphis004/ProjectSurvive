# ProjectSurvive — Architecture Overview

> Status: Stage 01 (Core library & dotnet harness). This document describes the
> target architecture and the decisions locked so far. Stage 01 put the layers,
> the harness and the deterministic `Shared` primitives in place; no game rules
> are implemented yet.
>
> Source of truth for gameplay: [`docs/gdd/prototype-v0.3.md`](../gdd/prototype-v0.3.md).

## 1. Goal of the architecture

Prove one loop (`Camp` → `Battle` → reward → back to `Camp`) on Web (WebGL2,
single-threaded, desktop browser) with **all game rules in pure C#** so they can
be tested with `dotnet test` without opening Unity.

Two consequences drive almost every decision below:

1. **The rule engine must not depend on Unity.** It lives in `lib/`, a local UPM
   package, and is compiled and tested from a plain `dotnet` solution.
2. **The rule engine must not depend on serialization or DI frameworks.** Domain
   and Simulation stay free of `R3`, `VContainer`, and serializers so the rules
   stay portable and cheap to test.

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
   │             references: Shared, Domain, EcsLite                       │
   │                                                                       │
   │   Infrastructure ──► Application ports (implements them)              │
   │   Contracts      ──► standalone DTOs (MessagePack / Protobuf)         │
   │   Generated      ──► machine output (Luban, protoc). Never edited.    │
   └───────────────────────────────────────────────────────────────────────┘
```

| Layer | May reference | Must NOT reference |
| --- | --- | --- |
| `Shared` | nothing but BCL | UnityEngine, R3, VContainer, serializers |
| `Domain` | Shared | UnityEngine, R3, VContainer, serializers |
| `Application` | Shared, Domain | UnityEngine, R3, VContainer, serializers |
| `Simulation` | Shared, Domain, EcsLite | UnityEngine, R3, VContainer, serializers |
| `Infrastructure` | Shared, Domain, Application, serializers (MessagePack), EcsLite adapter | UnityEngine |
| `Contracts` | MessagePack / Protobuf attributes only | Domain, Application |
| `Generated` | generated types only | hand-written code |
| `Presentation` (UnityProject) | Application, Infrastructure, Unity | must not compute prices, combos, damage |

**`Domain`, `Application`, and `Simulation` must not use `UnityEngine`.**
**`Domain` and `Simulation` must not use `R3`, `VContainer`, or any serializer.**

Enforcement (implemented in Stage 01):

- **In Unity:** one `.asmdef` per layer in `lib/Runtime/<Layer>/` with explicit
  `references` and `noEngineReferences: true`. The only assembly that keeps an
  engine reference is the **vendored** `Leopotam.EcsLite`, whose IL2CPP
  attribute blocks need Unity assemblies — kept byte-for-byte as upstream ships
  it (see `lib/third_party/ecslite/VENDOR.md`).
- **In `dotnet`:** one `.csproj` per layer in `lib/Tests~/Layers/`, each globbing
  `lib/Runtime/<Layer>/**/*.cs`, so the harness and Unity compile one source set
  and a wrong-direction reference is a compile error in both. Library projects
  target `netstandard2.1` with `LangVersion 9.0` to mirror the Unity 6.3
  compiler surface. Decision: [ADR 0004](../adr/0004-dotnet-layer-enforcement.md).
- **Guard tests** (`lib/Tests~/Core.Tests/ArchitectureTests.cs`) assert what a
  compiler cannot: that every `.asmdef` and `.csproj` matches the matrix above,
  that no built assembly references `UnityEngine`/`UnityEditor`, that no
  `lib/Runtime` source names `UnityEngine`, and that the rule engine avoids
  `System.Random`, `DateTime.Now` and `Stopwatch` (AGENTS.md rule 6).
- **Entry point:** `tools/test.ps1` (build → `dotnet test` → Sim determinism).

### Why these boundaries exist

- **Generated types never leak into Domain.** Luban rows are read through an
  `IGameConfig` adapter implemented in Infrastructure. Domain sees plain domain
  types, not generated ones.
- **Presentation is thin MVP.** A View holds no logic; a Presenter calls
  Application use cases only. See
  [`docs/adr/0002-thin-mvp-ui-approach.md`](../adr/0002-thin-mvp-ui-approach.md).

## 3. Three data lifetimes

| Scope | Lifetime | Owns |
| --- | --- | --- |
| **App** | until the game closes | config, settings, save repository, scene transitions |
| **Run** | from run start until leaving `Result` | seed, currency, HP, bag, shop stock, current floor |
| **Scene** | until the scene is left | views, presenters, the Battle ECS world, pooled visuals |

`RunSession` must **not** be recreated every time the player returns to `Camp`.
Returning to `Title` disposes the Run scope; leaving `Battle` disposes the ECS
world and every subscription it created.

The "scene loop grows memory" acceptance case (Camp ↔ Battle ×20) exists because
this is the most common leak in survivor-likes.

## 4. Command → event flow

Every state-changing operation is **atomic** and returns a **typed result**. If
it fails, state does not change.

```
   UI / Presenter
        │  command (typed)
        ▼
   Application use case ──► validate ──► mutate state as one unit ──► typed result
        │
        ▼
   Domain state changed  ──► domain event

   Commands : PurchaseItem, SellItem, RefreshShop, PlaceBag, MoveItem,
              RotateItem, MergeItems, StartFloor
   Events   : WalletChanged, InventoryChanged, ShopStockChanged,
              LoadoutChanged, FloorCompleted, RunCompleted
```

At "depart for battle" the flow is:

```
   validate bag ──► aggregate stats + combo bonuses ──► CombatLoadoutSnapshot
        ──► hand to Battle ──► lock bag editing until back at Camp
```

Battle never reads UI positions and never recomputes combos itself.

## 5. Determinism rules

- Battle simulation runs at a **fixed 30 Hz tick**.
- All randomness goes through `IRandomSource`. Streams are separated:
  **shop / battle / visual**.
- Core code must not use `UnityEngine.Random`, `Time`, or `DateTime.Now`.
- Determinism is guaranteed **within the same runtime only** — not across
  browsers or CPUs.

## 6. Scene and UI technology

- Unity 6.3 + URP; 2.5D (2D sprites on a low-poly 3D set).
- **Scenes and prefabs are created through the Unity Editor API** (or a verified
  template), never by hand-writing `.unity` YAML.
- UI language decision (uGUI + TMP vs UI Toolkit) is deferred to the Stage 03
  Thai text spike.
- UI framework is a thin, project-owned MVP until a concrete `UI.MVP Lite`
  package is identified — see ADR 0002.

## 7. What is here, and what is not

**Stage 01 landed the skeleton and the deterministic foundations:** the `lib/`
UPM package, the six layer asmdefs, the vendored EcsLite, the `dotnet` harness
(one project per layer + xUnit + a Sim console), and the `Shared` primitives
(`IRandomSource` / `Xoshiro128` / `RandomStreams`, `IClock`, `FixedTicker`,
`Result` / `Error`) with `SystemClock` as the `IClock` implementation.

**`Domain`, `Application`, `Simulation` and `Contracts` are still empty.** No
game rule, no data table, and no battle system exists yet; `data/`, `proto/`,
`tests/web/` and `UnityProject/` still hold only `.gitkeep` files.

Deferred to later stages:

| Concern | Stage |
| --- | --- |
| Luban + protoc pipeline, `IGameConfig` | 02 |
| Unity shell, first Web build, Thai text spike | 03 |
| CombatLoadoutSnapshot / combo evaluation | 05–06 |
| Battle ECS world + spatial hash | 07 |
| 2.5D presentation | 08 |
| Camp scene + bag UI | 09 |
| Save / resume / settings | 12 |
| Playwright harness + CI | 14 |

## 8. Related documents

- Dependency matrix & risk: [`dependencies.md`](dependencies.md)
- Game design: [`../gdd/prototype-v0.3.md`](../gdd/prototype-v0.3.md)
- Decisions: [`../adr/`](../adr/)
- Glossary: [`../../llmwiki/glossary.md`](../../llmwiki/glossary.md)
