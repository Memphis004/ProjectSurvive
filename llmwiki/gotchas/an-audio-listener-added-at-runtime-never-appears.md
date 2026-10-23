# Gotcha — an `AudioListener` added at runtime never appears, and the warning flood is a memory leak

`gameObject.AddComponent<AudioListener>()` called from a scene object's `Awake` (and, measured,
from its `Start` too) does not produce a listener. `GetComponent<AudioListener>()` returns null in
the very same call, and Unity reports

```
There are no audio listeners in the scene. Please ensure there is always one audio listener in the scene.
```

**once per frame for every playing `AudioSource`** — not once per scene load. Nothing throws, the
game just runs silent, so the only visible symptom is that line in the console.

## Why it cost a test case

The PlayMode runner keeps every captured log line in memory. About 1,750 lines per camp/battle
visit is a megabyte of strings per visit, which landed on the 20-visit heap case
(`SceneFlowPlayModeTests.Twenty_camp_and_battle_visits_leave_nothing_behind_and_do_not_grow_the_heap`)
as 5.07 MB of "managed growth" in the Editor and 17.8 MB in a batch run — against a 3 MB budget.
The leak the case was built to find (a surviving scope, a live ECS world) was gone; the case was
measuring the warning.

The same class of mistake, with a different publisher: `WebGlBridge.Publish` used to `Debug.Log`
the whole JSON document, and camp/battle reports are kilobytes on a timer. That is now a size line
(`"[ProjectSurvive] <global> (<n> chars)"`); the smoke report is still logged in full because it is
one small document per session.

## What to do instead

Author the component into the scene that owns it, on the object that persists — here the `Audio`
child of the Boot scene's app root, which `SceneBoot` moves into `DontDestroyOnLoad`:

```csharp
audioObject.AddComponent<AudioListener>();
AudioDirector audio = audioObject.AddComponent<AudioDirector>();
```

`BootSceneBuilder` does this, and `SceneFlowPlayModeTests`
`The_game_has_one_audio_listener_that_survives_every_scene_change` asserts the result: exactly one
enabled listener, on `AudioDirector.Instance`'s own object, after camp → battle → title.

Do not "check for an existing listener and add one if missing" from a component's `Awake` — the
check passes, the add silently does nothing, and the absence is only visible per frame.
