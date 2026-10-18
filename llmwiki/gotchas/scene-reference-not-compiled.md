# Gotcha — a scene reference is not compiled

**What happens.** An Editor builder creates a GameObject with a component and a
settings object with a `[SerializeField]` for it, but never assigns the field. The
project compiles, the EditMode suite is green, and the screen draws an empty panel.
Nothing in C# connects the two objects; the reference is **scene data**, and the
compiler has no opinion about it.

The game-era example: a HUD builder created a `BattleMiniBagView` and the settings
asset had a `MiniBag` field, but the builder never called the setter. A capture tool
that found the view **by type** still photographed a correct-looking panel; only a
PlayMode test that read the *scene's* wiring (`.MiniBag != null`, and its contents
matching the loadout) saw the defect.

**How to catch it:**

- A PlayMode test that resolves the component **from the loaded scene** and asserts
  every serialized reference the screen needs is non-null. Do not construct the view
  in the test — that proves the type works, not that the scene is wired.
- After regenerating a scene, grep the builder for the assignment you expect. A
  field that appears once (declaration) and never again (assignment) is the tell.
- Prefer a builder API that returns the created component and requires it as an
  argument to the next step, so forgetting it is a compile error rather than an
  empty frame.

**Why it is easy to miss.** Editor-time tools that build the object themselves
(including a capture tool) exercise the class, not the scene. A scene reference is
only observable once the scene is loaded as authored.
