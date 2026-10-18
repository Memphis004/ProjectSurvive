# The loop: three scopes, five screens, one run

The template plays end to end:

**Boot → Title → Hub ⇄ Encounter → Result → Title**

Nothing is simulated: the Encounter is a placeholder screen whose "progress" is a
number of ticks. What the loop does provide is the thing a game would otherwise get
wrong first — **where a run lives**, and what happens when a scene changes or a run
ends.

| Piece | File | What it owns |
| --- | --- | --- |
| App scope | `Assets/Main/Scripts/Presentation/RootLifetimeScope.cs` | the shell: config, localization, clock, `SceneLoader`, `RunScopeOwner`. Persists across scenes (`SceneBoot.PersistAcrossScenes`) |
| Run scope | `Assets/Main/Scripts/Presentation/Flow/RunLifetimeScope.cs` (+ `RunScopeOwner`) | exactly one `RunSession` — seed, streams, encounter counter, command log |
| Scene scope | `HubSceneScope` / `EncounterSceneScope` / `ResultSceneScope` | that scene's presenter, UI state and subscriptions |

The rule that makes the loop work is **a child scope never re-registers the shell,
and a scene never creates a run**. `RootLifetimeScope.Configure` returns early when
it has a parent, so a scene scope's `Resolve<RunSession>()` comes from the App scope's
run scope — the same object across every crossing.

## 1. Leaving a scene is one place

`Presentation.Flow.SceneLoader` is the only way a scene changes: one transition at a
time, behind a fade, with a typed refusal (`transition.in_progress`,
`transition.scene_missing`, `transition.load_failed`, `transition.disposed`) instead
of a half-applied move. Two behaviours are load-bearing:

- **A scene that is not in the build cannot be loaded** — checked *before* the fade,
  so a refusal leaves the player on the screen they were on.
- **Arriving at `SceneId.Title` disposes the Run scope.** "Returning to the title
  disposes the run" is a property of the transition, not something each screen
  remembers — which is what makes "the title has no stale run" a one-line assertion.

A director asks for a departure **after** it has committed the domain change, never
before: the Encounter records its outcome and only then asks to leave; the Result
screen records the player's intent and leaves once the loader accepted it.

`SceneFlow` (Application) is the *policy* half — which edges exist and whether the run
is in a state that allows one. `SceneLoader` is the *mechanism* half. Keeping them
apart is what lets `SceneFlow` be tested with `dotnet test` and `SceneLoader` be the
only thing that touches `SceneManager`.

## 2. The encounter loop

1. the Title starts a run (`RunSession.Start`) and asks for the Hub;
2. the Hub opens an encounter (`BeginEncounter`) and departs for the Encounter;
3. the Encounter ends (`EndEncounter(outcome)`): a cleared encounter raises the
   counter, a lost one finishes the run;
4. the director chooses the destination from the outcome — `Result` when the run is
   over, `Hub` otherwise;
5. `Result` either resets (`RunSession.Reset` → a new run) or returns to the Title,
   which disposes the run scope.

**The departure is asked for repeatedly, and that is deliberate.** A screen can end
while a transition is still in flight, and nothing else will ask again, so the
director retries every frame and logs the first refusal only. A single attempt would
strand the player on a finished screen.

## 3. The three published documents

A WebGL player cannot run Unity's tests, so each screen publishes what it drew and
where its controls are:

| Global | Screen | Contents |
| --- | --- | --- |
| `__projectSurviveTitle` | Title | the two localized strings, build line, canvas size, one `start` control with its centre in canvas pixels |
| `__projectSurviveHub` | Hub | cleared-encounter count, the two ways out, their centres |
| `__projectSurviveEncounter` | Encounter | the progress line, the way out, its centre |
| `__projectSurviveResult` | Result | whether the run finished, encounters cleared, the two controls |

`__projectSurviveScene` is published separately by every scene's director. The names
have one home per side (`WebGlBridge` constants and `tests/web/globals.mjs`), and
`WebGlBridgeTests` fails when they drift.

The browser suite clicks **the point a screen published**, never a hard-coded
coordinate, and asserts the next screen's own document. That is why `loop.spec.ts`
can walk the whole loop without knowing the layout.

## 4. Facts that cost time to learn

- **Frame boundary between `Screen` and `CanvasScaler`.** A report written in the
  frame a canvas resizes carries the new size with the previous geometry. See
  [`../gotchas/layout-report-one-frame-early.md`](../gotchas/layout-report-one-frame-early.md).
- **The scaler's virtual width follows the aspect.** At the default 960×600 canvas
  the reference rect is 1821.6 × 1138.5, so 1920-wide arithmetic is off-centre. See
  [`../gotchas/canvas-scaler-follows-aspect.md`](../gotchas/canvas-scaler-follows-aspect.md).
- **A scene must publish its name.** A missing marker looks like a boot failure. See
  [`../gotchas/scene-must-publish-name.md`](../gotchas/scene-must-publish-name.md).
- **The fade overlay needs an EventSystem, and it must die with the transition.** The
  game era's overlay fallback was created on the `DontDestroyOnLoad` canvas and never
  taken down, so every later scene carried two EventSystems and Unity logged a
  warning every frame — a linear 314 KB per scene change, ~5.4 MiB over 18 crossings.
  It is created and destroyed inside the transition now.

## 5. What a game replaces

| Placeholder | A game's version |
| --- | --- |
| `Hub` | a rest area, a shop, a loadout screen |
| `Encounter` | its real play loop (a battle, a puzzle, a stage) behind `FixedTicker` |
| `Result` | its own summary and rewards |
| `RunSession`'s four commands | its own command set, same atomic/typed shape (AGENTS rule 7) |
| `EncounterOutcome` | its own outcomes |

The shape to keep is: **one run, one loader, one place per scope that creates and
disposes it.**
