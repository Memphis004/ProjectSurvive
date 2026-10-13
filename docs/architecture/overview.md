# ProjectSurvive — Architecture Overview

> Status: Stage 00 (Discovery & Baseline). This document describes the target
> architecture and the decisions locked so far. No game systems are implemented
> yet — folders exist as placeholders only.
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

Enforcement:

- In Unity: one `.asmdef` per layer with explicit `references`, and
  `noEngineReferences: true` where the layer must stay engine-free
  (Stage 01).
- In `dotnet`: separate `.csproj` per layer, or an architecture test. The exact
  mechanism is chosen and recorded in an ADR during Stage 01.

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

## 7. What is NOT here yet

This stage deliberately ships **no game code**. Every `lib/`, `data/`, `proto/`,
`tests/web/`, and `UnityProject/` directory currently holds only a `.gitkeep`.

Deferred to later stages:

| Concern | Stage |
| --- | --- |
| `lib/` UPM package + asmdefs + dotnet harness | 01 |
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
