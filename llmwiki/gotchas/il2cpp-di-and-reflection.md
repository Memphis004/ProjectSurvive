# Gotcha — IL2CPP strips constructors that only reflection can see

**Found:** Stage 03, in the **first Web build**, by the browser smoke test.
**Symptom (exact text in the browser console):**

```
[console:error] VContainerException: Type does not found injectable constructor, type: SystemClock
[console:error] SceneBoot has no built RootLifetimeScope - the container cannot resolve anything.
```

The page then reported `{"platform":"BootFailure", … "detail":"SceneBoot has no built
RootLifetimeScope …"}`. Everything was green in the Editor: EditMode 12/12,
PlayMode 5/5, and the Editor PlayMode test that boots the real scene passed.

## Cause

`VContainer`'s `TypeAnalyzer` builds a registration from a `ConstructorInfo`
(`typeInfo.GetConstructors(...)`, `Runtime/Internal/TypeAnalyzer.cs`). Managed code
stripping in an IL2CPP build removes a constructor that **no IL call site reaches** —
and a type that is only ever instantiated through reflection has, by definition, no
such call site. VContainer's own source says it plainly, for Unity `Component`s:

```csharp
// It seems that Unity sometimes strips the constructor of Component at build time.
// In that case, allow null.
```

`Core.Infrastructure.SystemClock` was registered as
`builder.Register<IClock, SystemClock>(Lifetime.Singleton)` — a reflected
constructor — and it was the first registration the analyzer touched, so the
container threw during `LifetimeScope.Awake` and never built.

## Rule

**Register with an explicit factory, not a reflected constructor:**

```csharp
// fragile in a player build: only reflection ever sees the constructor
builder.Register<IClock, SystemClock>(Lifetime.Singleton);

// safe: `new` keeps a call site the linker can see
builder.Register<IClock>(_ => new SystemClock(), Lifetime.Singleton);
```

A factory is also self-documenting where a dependency is constructed by hand (an
optional parameter, a value read from `Resources`), and it keeps the composition
root off VContainer's reflection path entirely.

## Why the Editor test suite cannot catch it

Mono (Editor, EditMode, PlayMode) does **not** strip, so the reflected constructor is
always present. Only an IL2CPP player is a valid oracle here. That is exactly why
Stage 03 publishes the smoke verdict to the page and asserts on it from Playwright:
the browser test is the only automated gate for this class of bug.

## What to check when a container "is not built" in a player build

1. Browser console (or player log) for `VContainerException` — it names the type.
2. Is that type registered with `Register<TImplementation>(Lifetime…)` or
   `Register<TInterface, TImplementation>(Lifetime…)`? Replace it with a factory.
3. Only if a type genuinely cannot be built by a factory (a `Component`, a
   third-party type) reach for `link.xml` / `[Preserve]` — one mechanism, not both.
