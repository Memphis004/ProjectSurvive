# ADR 0022 — The title screen starts the run, and it asks for the run at the moment it is pressed

- **Status:** Accepted (Stage 10d).
- **Date:** 2026-10-17
- **Inherited by:** template-v1 — the decision still applies; the numbering below is the template's own (0001–0099).
- **Related:** [ADR 0016](../template/examples/adr/0016-run-settlement-and-shop-commit-boundaries.md) (where a run's state is
  allowed to change), [ADR 0019](../template/examples/adr/0019-camp-screen-read-model-and-gesture-preview.md) (the camp's read
  model — the report shape this screen copies), AGENTS rules 5 (strings come from localization),
  7 (state-changing commands are atomic and typed) and 10 (a View has no logic)

## Context

Until Stage 10d the title screen was a display: `TitleView` mirrored two strings and
`TitlePresenter.Refresh()` wrote them, and **nothing on the screen could leave it**. The stage's own
acceptance line is what forced the question — “กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่” —
and the loop's first scene could not satisfy it, because Boot hands over to the title and stops
there by design. A player who opened the game sat at a screen with no control, and the browser
suite could open the title and assert its text and nothing more.

So the screen gains exactly one thing: a control that begins a run. Three questions came with it,
and each had a tempting answer that is wrong in a way a player or a replay would eventually see.

1. **Who starts the run?** Since Stage 10a the run is owned by the App scope (`RunScopeOwner`), and
   arriving at the title *disposes* it — that is what makes “a run started after the summary is a new
   one” true. The camp, however, had a guard of its own: opened without a run (a direct
   `?scene=Camp` load) it built and started one. The obvious placement of the new control is
   therefore “press → go to the camp → let the camp start the run”, which needs no new code at all.
2. **Where does the run live while the title holds it?** `TitlePresenter` is an App-scope singleton,
   which outlives every scene. Anything it captures for longer than one press outlives the run scope
   it captured.
3. **What does the button do with the press, and what does Space do?** The screen's control is a
   Unity `Button`, so a press can be handled by a `UnityEvent` in the scene, by the input module's
   submit event, or by reading a key, and two of those three bindings are not what a player assumes
   they are.

## Decision

**1. The title starts the run; the camp keeps its guard for the handovers that do not come through
it.** `TitlePresenter.RequestStart()` calls the **same** Application command the camp calls
(`RunSession.StartRun`) and then records the intent. There is one place a run is started and one
answer to “what does a new run look like”, and the camp's own guard still covers a direct
`?scene=Camp` load — where by then the run is already in `RunFlowState.Camp`, so the guard does
nothing. `RunSession.StartRun` refuses a run whose state is not `Title`
(`RunError.WrongState`), so a title opened over a live run does not restart it: the player is taken
to the camp, which is where a live run belongs.

**2. The run is asked for lazily, never held.** The presenter resolves `RunScopeOwner` and calls
`RequireRun()` at the moment the player presses. It keeps no `RunSession` field. A presenter that
had captured one would hand the screen the run a previous game ended, because arriving at the title
disposes the run scope and the presenter — a singleton of the App scope — outlives that disposal.
“A new run” is then true by construction rather than by remembering to rebuild something.

**3. The press is recorded, not performed.** `RequestStart` sets `RequestedScene = SceneId.Camp`
(one request at a time: a second press while the first is in flight is refused) and
`TitleSceneDirector` — the scene's own component, like `CampSceneDirector`, `BattleSceneDirector`
and `ResultSceneDirector` — reads it and asks the `SceneLoader`. A refusal (a transition already in
flight, or a scene that is not in the build) is typed, is logged once rather than every frame, and
**leaves the screen as it was**: nothing has half-moved, and the request is simply retried on the
next frame, which is free because it costs one comparison per frame until the fade takes over. This
is the shape the summary screen's two ways out already take.

**4. The control is the EventSystem's selected object, and Space is read explicitly because it has
to be.** Enter reaches the button as the input module's own submit event and works only while the
button is selected, so `TitleView` selects it on arrival (and does not steal a selection something
else already made). Space, which a player also expects to press, is **not** in the module's default
submit bindings on the Input System version this project pins — measured: with the focus in place
Enter starts the run and Space does nothing at all. `TitleView.Update` therefore reads
`Keyboard.spaceKey.wasPressedThisFrame` and forwards to the **same** `PressStart`, the way the camp
reads `R` and `Escape`. If a later Input System version binds Space as well, the second call arrives
at a presenter that already has a request outstanding and is refused — the duplication is absorbed
by (3) rather than papered over by removing the key.

**5. The screen publishes a report, and it is coordinates, not permission.** `TitleReport` writes
`__projectSurviveTitle` — the scene marker, the strings the player reads (from the localization
table, through the presenter), the start control's name and its centre in canvas pixels — and
`TitleSceneDirector` rewrites it when the canvas size changes, because a point in canvas pixels goes
stale the moment the canvas does. A point is where a control is, not proof a click will be accepted:
the browser test clicks there and asserts the game's own answer (the scene marker becoming `Camp`,
the camp's own report and its run). The same document shape as `CampReport`, `DevMenuReport` and
`ResultReport`, for the same reason: a WebGL player cannot run Unity's test framework.

## Alternatives considered

- **Let the camp start the run (press → camp, camp builds and starts it).** Rejected, although it is
  the smallest change: it makes “the player pressed เริ่มเล่น” and “the camp was opened” the same
  event, so the acceptance line has nowhere to be asserted — a test could not tell a new run from a
  resumed one, and the title would have no run to hand over at all. It also puts the run's creation
  in a screen that is reached two different ways (through the title, and by a direct URL), which is
  exactly the duplication `RunScopeOwner` was introduced to remove.
- **Hold the `RunSession` in the presenter (or in `UiState`) so the title can read it.** Rejected: the
  presenter is an App-scope singleton and the run scope is disposed on arrival at the title, so the
  field would either be a dangling reference or force the run to outlive its own disposal — undoing
  Stage 10a's whole point.
- **Wire the press through a `UnityEvent` on the `Button` in the scene.** Rejected: the scene is
  authored by `ProjectSetup`, and a UnityEvent pointing at a `MonoBehaviour` method would put the
  decision of *what a press means* in the scene file instead of in the presenter (AGENTS rule 10),
  and would leave the keyboard path to a second mechanism. The listener is added in code by the view,
  which is also what makes `OnDestroy` able to remove it.
- **Add an `InputActionAsset` (or a custom submit binding) for Space.** Rejected *for this screen*: it
  adds an asset, a generated wrapper and an enabled-action lifetime to the one screen that needs a
  single key, while the project already reads bare keys in the camp (`R`, `Escape`) — and the
  duplicate-call problem (3) makes the extra binding unnecessary rather than merely redundant.
- **Publish a rectangle (`x`, `y`, `width`, `height`) instead of a centre point.** Rejected for
  consistency and for what it invites: the camp and result reports publish centres, a centre is what
  a click needs, and a rectangle invites a test to assert geometry rather than to make a press whose
  acceptance the game reports itself.
- **Let the report carry a flag saying the control is enabled.** Rejected: the same reasoning as the
  dev menu's report (Stage 10's report §7, row 3 — kept in the game repo's history, tag `survive-stage-10`) — a document is not a permission,
  and the game's own answer is the assertion.
- **Leave the screen as it was and let the browser suite drive the run through the dev menu.**
  Rejected: the dev menu belongs to the arena, not to the loop's first screen, and “the first screen
  of the game can be left” would then be provable only for a Development build.

## Consequences

- **“A run started from the title” is a state, not a scene change.** `Boot_stops_at_the_title_and_the
  _start_control_walks_into_the_camp` asserts the run exists, carries the shell's seed, is on floor 1
  with the starting purse, and that the camp is editing *that* object (`Is.SameAs`) after the
  crossing — none of which would be assertable if the camp had made it.
- **The title is now the only screen that starts a run**, and the camp's guard is a fallback rather
  than a path. `TitlePresenterTests`/`TitleScreenTests` hold the start, the double-press refusal and
  the already-live-run case; the camp's guard keeps its own existing coverage.
- **`TitleKeys.All` is the screen's whole vocabulary** (`ui.title`, `title.start`), and the
  localization table grew one row; the load test's pinned count moved 187 → 188 with it.
- **The scene is three measured regions** (heading 1800×140, build line 1800×60, control 400×76),
  authored above the 22 px Thai floor, so `ProjectSurvive/Tools/1. Capture UI` judges the same
  rectangles the player sees, and `TitleScreenLayoutTests` + `UiLayoutCheckTests` hold the overlap
  and floor rules in EditMode.
- **`Boot` is unchanged.** It still stops at the title on purpose; the title is what now leaves.
- **A second way to leave the title would be a rule, not a UI change.** Anything else that departs
  from the title (a continue button, a settings screen that leaves) goes through
  `TitlePresenter`'s request + the loader, so that (3) keeps holding; a screen that called the
  `SceneLoader` itself would be able to half-move.
