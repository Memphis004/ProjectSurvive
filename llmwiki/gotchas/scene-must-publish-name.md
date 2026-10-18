# Gotcha — a scene must publish its own name

A WebGL player cannot run Unity's test framework, so a browser test can only observe
what the player volunteers. `__projectSurviveScene` is that channel: each scene's
director publishes its own `SceneId` when it comes up, and no other layer knows the
scene is on screen.

A scene that forgets to publish looks like a **boot failure**, not like a missing
assertion: the marker stays at the previous scene (or `Boot`), the test waits for a
handover that already happened, and the failure message points at boot.

**Checklist for a new scene:**

1. its director calls `SceneFlow.SyncTo(SceneId.X)` and publishes the marker;
2. `SceneBoot._urlSceneOverrides` lists it if a test should reach it directly with
   `?scene=X`;
3. a PlayMode test enters the scene and asserts the marker.

The marker name has one home per side (`WebGlBridge` constants and
`tests/web/globals.mjs`), and `WebGlBridgeTests` fails when the two disagree.
