# ProjectSurvive — Architecture Overview

> Status: Stage 02b (battle contract). This document describes the target
> architecture and the decisions locked so far. Stage 01 put the layers, the
> harness and the deterministic `Shared` primitives in place; Stage 02 added the
> data pipeline (`data/` → Luban/protoc → `lib/Runtime/Generated`) and the
> `IGameConfig` port; Stage 02b added the **battle contract** in `Domain` with a
> fake implementation and an executable contract suite. No **gameplay rule**
> (combo, shop, combat) is implemented yet.
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
  types, not generated ones — enforced by a reflection guard test over the built
  assemblies plus the domain/generated enum parity test. Decision:
  [ADR 0006](../adr/0006-config-port-and-generated-code-isolation.md).
- **Presentation is thin MVP.** A View holds no logic; a Presenter calls
  Application use cases only. See
  [`docs/adr/0002-thin-mvp-ui-approach.md`](../adr/0002-thin-mvp-ui-approach.md).

## 2b. The static data pipeline (Stage 02)

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

Rules and conventions: [`llmwiki/systems/data-schema.md`](../../llmwiki/systems/data-schema.md).
Decisions: [ADR 0005](../adr/0005-data-pipeline-shape-and-format.md),
[ADR 0006](../adr/0006-config-port-and-generated-code-isolation.md).

| Gate | Tool | What it proves |
| --- | --- | --- |
| generate | `tools/gen.ps1` | Luban + protoc accept `data/`; writes code, both byte sets, the config hash |
| validate | `tools/validate-data.ps1` | the cross-table game rules hold (`lib/Tests~/Data.Validator`) |
| reproduce | `tools/verify-generated.ps1` | a fresh generation matches what is committed, byte for byte |
| reject | `tools/check-fixtures.ps1` | every deliberately broken fixture under `lib/Tests~/fixtures` fails, for the declared reason |

All four run inside `tools/test.ps1` (`-NoPipeline` skips them).

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

## 4b. The battle contract (Stage 02b)

The battle is reached through one seam, defined in `Domain` before the EcsLite
implementation exists (Stage 07):

```
   Application ──►  Domain.Combat.Battle  ◄──  Simulation
   (Run, Fake)      IBattleSession              (EcsLite, Stage 07)
                    IBattleSessionFactory
                    IBattleFrame / IBattleEventSource
                    BattleSetup / BattleResult / BattleReplay
```

- The contract lives in `Domain`, not `Contracts`, because both Application and
  Simulation may reference `Domain` and nothing else in common. It references no
  EcsLite, `UnityEngine`, `R3` or serializer, guarded by
  `ArchitectureTests.Battle_contract_types_reference_no_engine_or_ecs_types`.
- `Step` advances one 30 Hz tick; `Submit` returns a typed `CommandResult`;
  `TryGetResult` stays false while running. Duration and win condition come from
  the floor table, never from code.
- HP carries **across** floors: `BattleSetup.CurrentHp` in,
  `BattleResult.FinalHp` out. The clear heal is Run's job.
- `BattleEventRing` is the shared event stream: fixed capacity, strictly
  increasing `Seq`, independent reader cursors, honest `dropped` reporting, zero
  allocation. **Events are for presentation only** — rules read state, never the
  event stream.
- `FakeBattleSession` (Application) satisfies the contract without EcsLite, and
  `BattleSessionContractTests<TFactory>` is the contract every factory must pass
  (the fake today, EcsLite from Stage 07).

Details: [`llmwiki/systems/battle-contract.md`](../../llmwiki/systems/battle-contract.md),
[ADR 0007](../adr/0007-battle-contract-and-asmdef-placement.md).

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

**Stage 02 landed the data pipeline:** twelve tables under `data/`, the pinned
Luban 5.1.0 + protoc 33.6 toolchain (downloaded by SHA-256 into `tools/vendor`),
the generated C# under `lib/Runtime/Generated/`, the config hash in build
metadata, the `IGameConfig` port with its Infrastructure adapter, and four gate
tools with twelve broken-data fixtures behind them.

**Stage 02b landed the battle contract:** `Domain/Combat/Battle` (interfaces,
`BattleEventRing`, `BattleReplay`, setup/result/loadout types), the
`FakeBattleSession` in Application, and the executable
`BattleSessionContractTests<TFactory>`. **No gameplay rule exists yet** — the
contract is an interface, not a rule; `tests/web/` and `UnityProject/` still hold
only `.gitkeep` files.

Deferred to later stages:

| Concern | Stage |
| Unity shell, first Web build, Thai text spike | 03 |
| CombatLoadoutSnapshot produced by the resolver / combo evaluation | 05–06 |
| Battle ECS world implementing `IBattleSession` + spatial hash | 07 |
| 2.5D presentation | 08 |
| Camp scene + bag UI | 09 |
| Save / resume / settings | 12 |
| Playwright harness + CI | 14 |

## 8. Related documents

- Dependency matrix & risk: [`dependencies.md`](dependencies.md)
- Game design: [`../gdd/prototype-v0.3.md`](../gdd/prototype-v0.3.md)
- Decisions: [`../adr/`](../adr/)
- Glossary: [`../../llmwiki/glossary.md`](../../llmwiki/glossary.md)
