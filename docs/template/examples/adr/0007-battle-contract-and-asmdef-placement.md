# ADR 0007 — Battle contract v1, its layer placement, and the fake

- **Status:** Accepted
- **Date:** 2026-10-14
- **Stage:** 02b (inserted between Stage 02 and Stage 03)
- **Related:** [ADR 0004](0004-dotnet-layer-enforcement.md) (layer enforcement),
  [ADR 0006](0006-config-port-and-generated-code-isolation.md) (config port),
  [`llmwiki/systems/battle-contract.md`](../../llmwiki/systems/battle-contract.md)

## Context

Stage 07 builds the battle as an EcsLite world. Stages 05, 06, 08, 10, 11 and 12
all touch it: 05 resolves a `CombatLoadoutSnapshot`, 06 stands a stub in for the
battle result, 08 presents it, 10 wires the loop, 11 adds boss AI, 12 saves
around it. Every one of those stages would otherwise invent its own idea of what
"the battle" is, and the first real integration would then discover the
mismatches late and expensively.

The battle also has to be **deterministic**, **allocation-free once running**, and
**free of `UnityEngine`, EcsLite, `R3` and serializers** in its rule layer. Those
constraints are much cheaper to fix as an interface than as a running ECS world.

So: define the contract first, prove it with a fake and an executable contract
test suite, and defer the EcsLite implementation to Stage 07 unchanged.

## Decision

### 1. The contract lives in `Domain`, not in `Contracts`

`ProjectSurvive.Core.Domain.Combat.Battle` holds every interface and value type of
the contract. `Domain` is the only layer that both **Application** (SceneFlow /
Run calls the battle) and **Simulation** (EcsLite implements it) may reference, so
placing it anywhere else would force one of them into a wrong-direction reference.

`Contracts` was rejected: it is reserved for Save / Protobuf DTOs (ADR 0001,
ADR 0006) and is referenced by nothing.

`IRandomSource` already lived in `Shared` (Stage 01), which `Domain` may
reference, so no move was needed.

### 2. `UnityEngine` / EcsLite / `R3` are kept out by a guard test

No contract type references those namespaces, and a reflection test
(`ArchitectureTests.Battle_contract_types_reference_no_engine_or_ecs_types`)
scans every public type in the namespace and fails if one does. The build graph
already prevents the assembly from referencing them; the test keeps a future edit
from drifting.

### 3. A fake session carries the loop until Stage 07

`FakeBattleSession` / `FakeBattleSessionFactory` (Application) implement the
contract without EcsLite. They read duration and boss-ness from the floor table,
seed the battle stream from the setup, emit events through the shared
`BattleEventRing`, and produce a deterministic `ComputeStateHash`. They do not
model combat: that is Stage 07's job, and pretending otherwise would be a second
implementation to maintain.

### 4. The contract is executable

`BattleSessionContractTests<TFactory>` (abstract, xUnit) is the contract. Stage 07
binds the EcsLite factory to the same base class with a one-line derived class and
must pass every case, so the real simulation is held to the rules the fake was
proven against.

### 5. `BattleEventRing` is shared by both implementations

The ring lives in `Domain` because both the fake and the simulation need exactly
the same event semantics (fixed capacity, strictly increasing `Seq`, independent
reader cursors, honest `dropped` reporting, zero allocation). Writing it once is
what makes "events are for presentation only" a property of the type rather than a
convention.

### 6. Replay is split: `BattleReplay` here, `ReplayLog` at Stage 06

`BattleReplay` records a single battle (setup + per-tick inputs + ticked
commands). The run-level `ReplayLog` of Stage 06 embeds one per floor; neither
duplicates the other.

## Consequences

- **Positive:** Stage 05–12 can be written and reviewed against a stable seam; the
  contract tests make a Stage 07 regression visible immediately.
- **Positive:** the fake already lets Stage 06 run a scripted 5-floor loop with no
  ECS in the tree.
- **Negative:** the contract is a second thing to keep in step with the simulation.
  The contract tests are the mechanism that keeps the cost bounded — they fail on
  drift instead of on playtest.
- **Negative:** a `BattlePhase` value (1) is reserved and unused. Accepted: it
  documents the one extension the design might want (`AwaitingChoice`) without
  building it (AGENTS rule 12).
- **Not covered:** the fake is not a combat model and must never be used to judge
  balance. Any balance claim waits for Stage 07's real simulation.
