# Gotcha — a UI pointer event does nothing while the Game View does not have focus

`InputSystemUIInputModule.Process()` returns before it touches any pointer state:

```csharp
// InputSystemUIInputModule.cs (com.unity.inputsystem 2436e68, read in the installed package)
if (!eventSystem.isFocused && !shouldIgnoreFocus)
{
    for (var i = 0; i < m_PointerStates.length; ++i)
        m_PointerStates[i].OnFrameFinished();   // nothing is delivered
}
else
{
    ProcessNavigation(...);
    ProcessPointer(...);                         // raycast, enter, press, click
}
```

`EventSystem.isFocused` is `m_HasFocus`, set from `OnApplicationFocus` — and in the Editor a PlayMode
run started from a terminal (or any run whose Game View lost focus) is *not* focused. The module then
looks completely healthy — enabled, active, `currentInputModule` set, its point action reading
`Performed` — while `EventSystem.IsPointerOverGameObject()` stays `false` and every click is dropped.
A scene reload, a re-enabled module and a second click do not change it; clicking the Game View does.

`shouldIgnoreFocus` is `InputSettings.backgroundBehavior == IgnoreFocus`, or the input runtime's own
`runInBackground`, or (Editor only) the input system's event-passthrough state.

## What that cost here

`CampBagDragPlayModeTests` and `CampMergeChooserPlayModeTests` each open the จัดย่าม panel with a real
click. Once the suite ran one fixture per test run (`tools/test.ps1 -Suite Full`), each run lost one
test — `clicking จัดย่าม did not open the panel … drag previews 0` — with a different victim per run,
and the same at `HEAD`. Diagnosed 2026-10-23 by logging the module, the device and a raycast the test
performs itself at the button: 2 hits, module enabled, point action `Performed`, `IsPointerOverGameObject`
false.

## What to do

In a PlayMode test that drives the UI with a simulated pointer:

```csharp
[SetUp]
public override void Setup()
{
    base.Setup();
    _backgroundBehavior = InputSystem.settings.backgroundBehavior;
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
}

[TearDown]
public override void TearDown()
{
    InputSystem.settings.backgroundBehavior = _backgroundBehavior;
    base.TearDown();
}
```

Under `InputTestFixture` the runtime is an `InputTestRuntime` whose `runInBackground` is `false` and is
`internal`, so `InputSettings.BackgroundBehavior.IgnoreFocus` is the reachable switch;
`Application.runInBackground = true` does **not** help there (measured — it changes the player's flag,
not the test runtime's). The project has no `.inputsettings` asset, so the change is session-only, and
the fixture restores it anyway.

A fixture should also wait until `EventSystem.current.IsPointerOverGameObject()` is true after moving the
pointer, so that a failure says "the pointer is not over the UI" instead of "the click did nothing".
