# ADR 0004 — Layer enforcement in the dotnet harness

- **Status:** Accepted
- **Date:** 2026-10-13
- **Stage:** 01

## Context

`lib/` is a local UPM package compiled by Unity from `.asmdef` files, but the
whole point of the package is that its rules can also be compiled and tested by
`dotnet test` without opening the Editor (Stage 00, ADR 0001). That creates two
distinct risks:

1. **Drift.** If the `dotnet` projects list their sources separately from the
   `.asmdef`, the two builds slowly stop compiling the same files — and then a
   green `dotnet test` says nothing about the Unity build.
2. **Unenforced layering.** `Shared ← Domain ← Application`, with `Simulation`
   and `Infrastructure` restricted to their documented references, is only
   actually enforced if both builds refuse to compile a wrong-direction
   reference. Outside Unity the asmdef does nothing.

Stage 00 left this open on purpose: *"In `dotnet`: separate `.csproj` per layer,
or an architecture test. The exact mechanism is chosen and recorded in an ADR
during Stage 01."*

## Options considered

| Option | Assessment |
| --- | --- |
| **A. One `.csproj` per layer, each globbing `lib/Runtime/<Layer>/**/*.cs`** | Compile-time enforcement of the whole reference matrix, and the file set is shared *by construction*. No new dependency. |
| **B. One project for all of `lib/` + architecture tests** | Simpler project graph, but the wrong reference still *compiles*; the violation is caught late, only for the rules the test happens to know. Needs reflection over assemblies or a Roslyn/NetArchTest dependency. |
| **C. One project, sources mirrored with an explicit `<Compile Include>` per file** | Double bookkeeping; a file added in `lib/` and forgotten in the list is silently not tested. |
| **D. Copy the sources into the test project** | Two diverging copies of the rules. Rejected. |

## Decision

Take **option A**, plus a small set of guard tests for what a compiler cannot see.

1. **One project per layer**, in `lib/Tests~/Layers/`:
   `ProjectSurvive.Core.{Shared,Domain,Application,Simulation,Infrastructure,Contracts}.csproj`
   and `Leopotam.EcsLite.csproj` for the vendored ECS. Each sets
   `EnableDefaultCompileItems=false` and a single `<Compile Include="$(LibRuntimeDir)<Layer>/**/*.cs" />`,
   so the compiled set *is* the folder the `.asmdef` covers. A shared
   `Directory.Build.props` supplies `LibRuntimeDir` / `LibThirdPartyDir` and a
   `VerifyLayerSources` target that fails the build when a glob matches nothing
   (a silently empty assembly is the one way this scheme could lie). Layers that
   are empty by design opt out explicitly with `<AllowEmptyLayer>true</AllowEmptyLayer>`.
2. **Layer rules are the project reference graph.** `ProjectReference` follows
   the documented matrix exactly, so a wrong-direction reference is a compile
   error in the harness just as it is in Unity.
3. **Mirror Unity's compiler surface.** Library projects target
   `netstandard2.1` with `LangVersion 9.0` — what Unity 6.3 compiles. Code that
   needs a newer language feature or a .NET 5+ API fails here instead of in the
   Editor. Test and Sim projects target `net9.0` so they can use the modern
   test host.
4. **Guard tests** in `lib/Tests~/Core.Tests/ArchitectureTests.cs` cover the rest:
   every `.asmdef` and every layer `.csproj` matches the documented matrix, no
   built assembly references `UnityEngine`/`UnityEditor`, no `lib/Runtime` source
   names `UnityEngine`, the rule-engine layers avoid `System.Random`,
   `DateTime.Now` and `Stopwatch`, the vendored EcsLite's Unity usage stays
   inside `#if` regions, and the vendored revision is pinned in both `VENDOR.md`
   and `tools/versions.lock.json`.

`tools/test.ps1` is the single entry point: build, `dotnet test`, then two
process-level determinism checks on the Sim.

## Consequences

- **Positive:** a violation fails fast and locally; there is no file list to keep
  in sync; `dotnet build` and Unity compile the same sources with the same
  language version, so a green harness is real evidence for the Unity compile.
- **Positive:** no new tooling dependency — MSBuild alone enforces the matrix.
- **Negative:** the `~` in `lib/Tests~` and the layer projects living under
  `Tests~` is slightly unusual; the folder must stay excluded from Unity's asset
  database (Unity ignores `~`-suffixed folders, and `.gitignore` re-includes it,
  which Stage 00 already arranged).
- **Negative:** adding a reference between layers is now a two-place edit
  (`.asmdef` + `.csproj`); the guard test fails if only one is updated, which is
  the intended tripwire.
- **Not covered:** the harness cannot see Unity-only assemblies, so the
  Presentation-layer rules ("View has no logic", "no R3/VContainer in Domain")
  remain the asmdef's job. `noEngineReferences: true` in every hand-written layer
  and the guard tests are what hold the line until the Unity project exists
  (Stage 03).
