# Gotcha — a destroyed Unity object is not a `null` reference

`UnityEngine.Object` overloads `==` and `!=` so that a **destroyed** object compares
equal to `null`. C# language constructs that do not use that operator disagree:

```csharp
Destroy(view);                  // the C++ object is gone this frame
Assert.True(view == null);      // true  — the overload
Assert.False(view is null);     // true  — `is null` is a real null check
Assert.False(ReferenceEquals(view, null)); // true — the managed wrapper still exists
```

The managed wrapper survives until it is collected, so a field can look alive to the
GC while the engine object behind it is gone. `view?.Foo()` uses `?.`, which is a
real null check — it **enters** `Foo()` and throws `MissingReferenceException`.

**Where it bites:** scene unload destroys scene objects before a `MonoBehaviour`'s
`OnDestroy` releases its pooled references, so a `view != null` guard passes and the
next line throws. The same shape appears when a presenter keeps a reference to a
scene object across a scene change.

**Rules of thumb:**

- Guard with `if (view == null)` (the overload), never `is null` or `ReferenceEquals`.
- Release pooled and cross-scene references in `OnDestroy` **before** the scene is
  unloaded, not lazily afterwards.
- Never cache a `UnityEngine.Object` across a scene boundary; resolve it from the
  scene scope instead.
