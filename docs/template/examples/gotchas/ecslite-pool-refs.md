# Gotcha — EcsLite pool refs, liveness, and DEBUG-only checks

Vendored EcsLite (`lib/third_party/ecslite`, revision pinned in `VENDOR.md`).
Everything below was read from the vendored source during Stage 01, not assumed
from the README.

## 1. A `ref` into a pool does not survive a world mutation

`EcsPool<T>.Add/Get` return a `ref T` into the pool's sparse array. Creating a
filter (or anything else that resizes the world arrays) can reallocate that
storage, leaving the old ref pointing at stale memory — the read still succeeds
and silently returns old data.

```csharp
ref Position p = ref world.GetPool<Position>().Add(entity);
p.X = 1;
systems.Run();                       // builds a filter -> arrays may resize
Assert.Equal(2, p.X);                // WRONG: stale ref
Assert.Equal(2, world.GetPool<Position>().Get(entity).X); // right: re-read
```

Rule for the battle code: take a ref, use it, drop it. Never hold a ref across a
world mutation.

## 2. `GetUsedEntitiesCount()` is not a liveness count

It returns `_entitiesCount` — the number of **allocated entity slots**, including
recycled (destroyed) ones — not the number of live entities. To observe liveness,
iterate the relevant filter (filters shrink immediately when the last component
is removed, because removing it destroys the entity).

## 3. Sanitize checks exist only in DEBUG

`Has()`, `Get()`, `Add()`, and `DelEntity()` guard with
`#if DEBUG && !LEOECSLITE_NO_SANITIZE_CHECKS`:

- In a **Debug** build, touching a destroyed entity throws
  (`"Cant touch destroyed entity."`) — a great early warning.
- In a **Release** build those checks are compiled out, so the same code reads
  garbage instead of throwing.

Consequence for tests: do not assert on those exceptions; a test that only passes
in Debug is a false signal. `tools/test.ps1` supports `-Configuration Release`
and the suite is required to pass in both.

## 4. The assembly keeps `noEngineReferences: false` — on purpose

Upstream's `.asmdef` is vendored verbatim. It needs Unity assemblies because
`#if ENABLE_IL2CPP` blocks use `Unity.IL2CPP.CompilerServices`, and two
`[UnityEngine.Scripting.Preserve]` attributes sit behind
`#if UNITY_2020_3_OR_NEWER`. Outside Unity those regions are excluded, so the
`dotnet` harness compiles the library as pure BCL — the guard test
`Vendored_ecslite_unity_usage_stays_behind_preprocessor_guards` asserts that every
`UnityEngine` mention stays inside a `#if`.

## 5. Not thread-safe, by design

Upstream states it never will be. That is acceptable here: WebGL2 is
single-threaded and the battle tick is single-threaded too (AGENTS.md rule 9
forbids `Task.Run`/`Thread`).
