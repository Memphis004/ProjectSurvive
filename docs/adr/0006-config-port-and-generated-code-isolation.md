# ADR 0006 — `IGameConfig` is a Domain port; generated rows stay in Infrastructure

- **Status:** Accepted (Stage 02)
- **Date:** 2026-10-13
- **Related:** [ADR 0004](0004-dotnet-layer-enforcement.md) (layer enforcement), [ADR 0005](0005-data-pipeline-shape-and-format.md) (the pipeline that produces the rows)

## Context

Stage 02 generates twelve Luban tables into `ProjectSurvive.Config`
(`lib/Runtime/Generated/Luban`). Domain and Simulation must not reference
`UnityEngine`, `R3`, `VContainer` or a serializer (AGENTS rule 3), and the GDD
adds that **Domain must not use the generated rows directly**. Meanwhile the
Stage 04/05+ rules — footprint parsing, combo evaluation, shop weights, wave
scheduling — all need this data.

So there has to be one seam between "a Luban row" and "a rule", and it has to be
enforceable by the compiler rather than by review.

## Decision

### 1. Domain declares the shape of the data it needs

`ProjectSurvive.Core.Domain.Config.IGameConfig` exposes plain domain types
(`BagConfig`, `ItemConfig`, `SkillConfig`, …), never a generated one. Every list
is in data-file order and every lookup is by the stable string id authored in
`data/`.

### 2. Infrastructure is the only consumer of generated types

`LubanGameConfig : IGameConfig` (`lib/Runtime/Infrastructure/Config/`) maps
`ProjectSurvive.Config.*` rows to domain objects. It is the single file in the
repo that names `ProjectSurvive.Config`, and it is engine-free on purpose: the
caller passes a `Func<string, byte[]>` that yields a table's raw bytes, so the
same adapter serves `Resources/Config` in Unity and `lib/Tests~/data` in the
harness or the validator — and `Application.streamingAssetsPath` never appears in
it.

### 3. The domain enums are hand-written mirrors, and a test holds them in step

`ConfigEnums.cs` duplicates the eight Luban enums with identical names and
numeric values, because the alternative (referencing generated enums from Domain)
would break the rule above. The duplication is the risk, so it is guarded:
`ConfigEnumParityTests` reflects over both sides and fails if a name or a value
drifts. Renumbering a value in `schema.xml` and regenerating is therefore a test
failure, not a silent gameplay bug.

### 4. No default, no stub, no constant fallback

`LubanGameConfig.Load` throws `ConfigLoadException` with a stable
`Code` (`config.table_missing`, `config.table_unreadable`,
`footprint.duplicate_cell`, …) instead of substituting a value. The stage's
acceptance criterion explicitly forbids a stub that returns a constant in place of
real config: a game must not be able to start on invented numbers.

### 5. One rule implementation, two entry points

`lib/Tests~/Data.Validator` holds the rules and runs both as the
`tools/validate-data.ps1` tool and as a library referenced by the xUnit project.
A rule therefore cannot pass in CI and fail in the pipeline.

## Enforcement (what makes this more than a convention)

| Mechanism | What it prevents |
| --- | --- |
| `.asmdef` / `.csproj` references in `lib/Runtime/Infrastructure` only | Generated code leaking upward at compile time |
| `ArchitectureTests.Only_infrastructure_references_the_generated_config_assembly` (reflection over the built assemblies) | A rule-engine assembly referencing `ProjectSurvive.Core.Generated.Luban` or `Luban.Runtime` |
| `ConfigEnumParityTests` | A silent enum renumbering |
| `Generated_sources_carry_a_do_not_edit_banner` | Hand-written code placed in `lib/Runtime/Generated/` |
| `tools/verify-generated.ps1` | Hand-edited generated code that no longer matches `data/` |

## Alternatives considered

- **Domain consumes generated rows directly.** Simplest to write, and it is what a
  single-assembly prototype would do. Rejected: every generated row type would
  then be part of the rule engine's public surface, so a schema rename would
  touch gameplay code, and the "Domain never references generated code" rule
  would be unenforceable.
- **Generate domain types from the schema instead of hand-writing them.** This is
  the strongest option on paper — one source of truth, no parity test — but it
  means a code generator that must also be maintained by hand, and its output
  would live in Domain (the layer that must stay free of generated code). Deferred
  rather than rejected: if the mirror list grows past a handful of enums, revisit.
- **One merged config object with dictionaries instead of typed lists.** Fewer
  types, more `object` casts and typos that the compiler cannot see. Rejected.
- **Load through `ScriptableObject`/Unity serialization.** Rejected: it would put
  config behind `UnityEngine` and make `dotnet test` impossible.

## Consequences

- Adding a field to a table touches the schema, the CSV, the domain type, and the
  adapter's mapping — and, if it is an enum, `ConfigEnums.cs`. This is the
  deliberate cost of keeping the rule engine portable and Unity-free.
- `Simulation`/`Application` consume `IGameConfig` hand-built or loaded; every
  future stage's tests can construct config in memory without the toolchain.
