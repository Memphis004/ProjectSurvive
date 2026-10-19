# Battle presentation 2.5D (Stage 08)

The Battle scene puts the Stage 07 simulation on screen: a 50°-tilted 2.5D camera over a
generated arena, one pooled sprite per published entity, interpolation between the two 30 Hz
ticks a frame publishes, a HUD driven by R3, a floor timeline with elite and boss markers, and
pooled floating damage numbers.

Everything here reads the battle through **two** interfaces — `IBattleFrame` and
`IBattleEventSource` — and may not name the simulation assembly at all. That is not a
convention: it is an EditMode test (`Presentation_may_not_reference_the_simulation_assembly`).

| | |
| --- | --- |
| Scene | `Assets/Main/Scenes/Battle.unity` (Index 3, after Boot, Title, ThaiTextSpike) |
| Composition root | `BattleSceneScope : RootLifetimeScope` |
| Driver | `BattleSimulationRunner` (clock + session) → `BattleViewScene` (pixels) |
| Decided by | [ADR 0018](../../docs/adr/0018-battle-camera-projection.md) (orthographic, 50°) |

## 1. The seam, and who is allowed to cross it

`Presentation/Battle/**` is the whole view. It compiles against
`ProjectSurvive.Core.Domain.Combat.Battle` only:

- `IBattleFrame` / `IBattleSession` / `IBattleEventSource` — the contract.
- `RenderEntity`, `RenderEntityKind`, `HudState`, `BattleResult`, `BattleConstants`,
  `EntityRef` — its vocabulary.

Three things it therefore may **not** do, each enforced rather than intended:

| Prohibited | Enforced by |
| --- | --- |
| Referencing `ProjectSurvive.Core.Simulation` | asmdef + `Presentation_may_not_reference_the_simulation_assembly` |
| Deciding an outcome from an event | design: nothing on `IBattleEventListener` returns a value, and the pump runs after the tick |
| Subscribing per entity | design: the HUD subscribes to `BattleHudState`'s scalars, never to an entity stream |

`BattleSceneScope` is the one class that names both the port and its implementations, exactly as
`RootLifetimeScope` does for the shell. Its `_useDevBattle` switch binds `IBattleSessionFactory`
to the Stage 02b dev session instead of `EcsBattleSessionFactory`; the view, the HUD, the pools,
the input and the clock are unchanged, which is what "develop the UI against the fake" means
here. The dev session is reached through `DevBattleSessionBinding` in the Application layer, so
neither the run nor the scene flow has to know it exists.

## 2. The clock: one accumulator, a tick cap, and interpolation

`BattleSimulationRunner.Update` does the whole clock policy in one place — deliberately on the
side of the seam that is allowed a clock, since `Time` may not enter the rule engine.

```
delta = Time.deltaTime × BattleSpeed.Multiplier
steps = FixedTicker.Advance(delta)        // ≤ BattleConstants.MaxStepsPerFrame
for each step: session.Step(input.Current)
copy the frame NOW                          // BattleFrameSnapshot.CopyFrom
view.DrainEvents(session.Events)
alpha = ticker.PendingSeconds / ticker.TickSeconds
view.Render(alpha, delta)
```

Consequences worth stating plainly:

- **A stalled tab drops ticks, it does not replay them.** Ten background seconds are not 300
  steps; `MaxStepsPerFrame` returns a handful and counts the rest in `DroppedTicks`. In a game
  about not being touched, replaying a stall is a death sentence.
- **A frame that ran no step re-renders the same endpoints at a new alpha.** The bridge does its
  binding work once per *tick* and only placement once per *frame*.
- **`OnApplicationPause` resets the ticker.** An inactive browser tab is "not stepped", so
  nothing is owed when it comes back.

## 3. The frame copy is not optional

`IBattleFrame.Entities` / `.PreviousEntities` are two slots the session reuses **in place**; they
are readable only until the next `Step`/`Submit` ([ADR 0012](../../docs/adr/0012-prev-cur-frame-double-buffer.md)).
`BattleFrameSnapshot.CopyFrom` is called immediately after the last step of a frame, before
anything else can step, and it is the object the bridge and the HUD read from then on.

The snapshot also indexes the previous tick by entity slot (`_previousSlot[index]`, validated by
version) instead of searching for a match. A linear match per entity would be quadratic at the
1024-entity render cap, and the version check is what stops a recycled slot from interpolating a
new ghost out of a dead one's position — the EcsLite liveness lesson, on the view side.

## 4. From simulation plane to scene: `BattleWorldMap`

The one place `(simX, simY)` becomes Unity `(x, 0, y)`. Simulation **+Y runs away from the
camera** along world +Z, so "further up the screen" is "further into the arena" with no second
coordinate system anywhere.

Depth is **authored, not projected**:

- `SortingOrder(kind, simY)` turns Y into an explicit sprite sorting order — further away sorts
  behind — plus a small per-kind bias, so a bolt still draws over the ghost it just hit.
- Because the order is explicit, occlusion of two characters does not depend on which projection
  is active. That is half of the ADR 0018 experiment: it is why perspective buys nothing for
  *การบังตัวละคร*.
- Billboards are tilted by `BattleCameraRig.BillboardPitch` (50°) so a sprite faces the camera
  square on; under parallel projection that orientation is right at every depth.

## 5. Pooling: nothing is created after warm-up, and that is measured

| Object | Pool | Cap |
| --- | --- | --- |
| Entity sprites | `EntityViewPool` | 1 + 512 + 1024 + 256 = **1793** |
| Damage numbers | `FloatingDamagePool` | 48 |
| Timeline markers | `BattleTimelineView` | one per spawn window, re-anchored |

`BattleViewCaps` mirrors the simulation's structural caps (`BattleArena.MaxEnemies` 512,
`MaxProjectiles` 1024, `MaxPickups` 256, `Radius` 40, `EliteRadiusScale` 1.4, `BossRadiusScale` 2)
because Presentation may not read them, and `View_caps_mirror_the_simulations_structural_bounds`
fails if the pair ever drifts.

The claim "ไม่ Instantiate/Destroy ต่อเนื่องหลัง pool warm-up" is a **counter**, not a reading of
the code: `EntityViewPool.CreatedTotal` and `CreatedAfterWarmup` are published in the battle's
progress report, and both the PlayMode test and the browser test assert the second one stays 0.
Growing the pool is the failure the counter reports; it is not a system the game relies on.

`FloatingDamagePool` caches `int.ToString()` for values up to 200 (a wave hits dozens of times a
second), drives every number from one `Tick` instead of a `MonoBehaviour.Update` per number, and
takes its jitter from `RandomStreams.Visual` — the visual stream, never the battle stream and
never `UnityEngine.Random` — so a damage number cannot reach a rule and a replay cannot depend on
how many were on screen (AGENTS rule 6).

## 6. Events: three cursors, and a drop is a fact

`BattleEventPump` reads `IBattleEventSource` once per tick and fans out to three channels — HUD,
VFX, Audio — each with **its own cursor**. They are separate so a slow consumer cannot cost
another one its events, and so "the VFX dropped 4 events" is a number
(`DroppedTotal`, per channel) rather than a guess. A channel that falls behind the ring is told
how many it lost; it logs in a development build and moves on. Blocking or replaying a backlog
would turn a slow frame into a wrong frame.

Events are presentation-only: nothing here decides an outcome, and the HUD uses them only for
what a frame cannot say ("the floor ended", "the timer is nearly out"). Audio is silent in
Stage 08 but already reads at its own pace, so adding it is a listener and not a redesign.

## 7. HUD: MVP, thin, and scalar

`BattleHudState` holds scalars (HP/MP pairs, bia, elapsed, kills, wave, timeline progress, the
end banner). No entity, no position, no list — `Hud_state_carries_scalars_and_never_an_entity`
asserts it, and it is what makes "HUD ไม่ subscribe ตำแหน่งทุก entity" true by construction: the
300-ghost case costs exactly what the one-ghost case costs.

`BattleHudPresenter` is the only writer of the state. It formats numbers and looks up every
caption in localization (AGENTS rule 5), and **guards each publish on its inputs, not on the
string** — a HUD sitting at constant HP builds no string at all, where a naive
`Subscribe(_ => label.text = ...)` would publish, re-layout and re-upload TMP geometry 60 times a
second for nothing.

`BattleHudView` mirrors reactive values into labels, bars and the timeline, and computes nothing
(AGENTS rule 10). Its one interaction is the 1×/2× button, which talks to `BattleSpeed`
directly because that control *is* UI state rather than a rule. `BattleSpeed` scales the delta
the ticker consumes; it never touches the simulation, and `MaxStepsPerFrame` still bounds one
frame, so fast-forward on a slow machine degrades to dropped ticks rather than to a long frame.

`BattleTimeline` reads the floor's spawn windows out of `data/` (which window is elite, which is
the boss) and hands the view normalized spans — the marker colours are the view's, the *meaning*
is the model's.

### 7b. The mini ย่าม (Stage 08c)

The battle screen is six anchored panels: the **mini ย่าม** top left (≤ 25 % of the canvas width by
`anchorMax.x`: 432 × 546 px at 1920 × 1080), the **floor** top centre, the run's numbers top right,
the **status panel** bottom left (figure, red HP bar, blue MP bar, current/max), the **timeline**
along the bottom middle, and the **dev column** bottom right on its own canvas (sorting order 200).
The equipped-spell list is gone; the bag says what is equipped.

`BattleMiniBagView` draws the bag **read-only**, and only the bounding box of the cells the bag
covers — never the whole 9 × 7 board. Geometry comes from the `BagSnapshot` and the
`CombatLoadoutSnapshot` the run already handed the battle (`BattleViewScene.Build`);
`BattleMiniBagLayout` fits the cell size to the panel and decides nothing a rule owns.

**Stage 08d: the grid hangs from the caption's corner, not the panel's middle.** The grid's area
(`MiniBagContent`) is anchored to the panel's top-left, one named `CaptionGap` (8 UI px) under the
caption's own line box, with the caption's `LeftInset`; the cell size still fits the area it was
handed, and the panel is still a quarter of the canvas wide. Inside that area the layout's origin is
the **top-left corner** — the earlier version centred the bounding box, which put a 3 × 3 bag 110 px
right and 151 px down at 1080p (27.6 / 45.8 screen px at 720p) from where it belonged. The rule is
checked three ways: `BattleMiniBagTests` (both sizes, plus the L-shaped `bag.wicker_basket_l`),
`UiLayoutCheck.FindMiniBagStackViolations` (below the caption, same left edge, no overlap — a rule
an overlap check cannot express, because a centred grid is *disjoint* from the caption), and the
battle PlayMode case that measures the real scene's rectangles.

A piece that can cast wears a plate from `IBattleFrame.Cooldowns` (08b), read out of the snapshot
the runner copied **in the same frame** — never from a clock of the view's own:

| The frame says | The plate shows |
| --- | --- |
| `IsReady` | nothing — a ready entry has no plate |
| `ReadyButManaShort` | the faint blue tone |
| still counting down | dark, `RemainingTicks / PeriodTicks` of the box |

"No plate" and "a plate at 0 %" are the same picture, which is why the tone is what separates them.
Elements are built once at floor start and only values that changed are written; the view counts its
own builds (`CreatedAfterBuild`) and writes (`PlateUpdates`), so a test can tell "the floor moved the
plate" from "the view redrew everything". Synergy link lines are deliberately not drawn yet.

## 8. The camera, and the one experiment the stage had to run

Both projections ship (`BattleProjection` + `BattleCameraRig`); the scene authors
**Orthographic** at the GDD's 50°. The decision is
[ADR 0018](../../docs/adr/0018-battle-camera-projection.md), taken from a measurement
(`ProjectSurvive/Stage 08/4. Measure camera projection`, output
`artifacts/stage08/camera-projection.json`) that builds the camera through the same factory
`ProjectSetup` uses, so the experiment cannot describe a camera the game does not run.

The short version: occlusion is authored, so perspective buys nothing for it — and under
perspective the same ghost is 14.164 px/unit at the near edge and 17.026 at the far edge
(near÷far 0.832), while orthographic is 16.529 everywhere (1.000). A prototype whose visual
language is *identical silhouettes at high count* cannot afford a silhouette that changes size
with the row it stands in.

## 9. Placeholder art is generated, not sourced

`BattlePlaceholderArt` draws every sprite in code — a disc for the ghost-doctor, a wavy blob for a
ghost, a dot and a diamond for bolts and drops, a tiled grid for the floor. AGENTS rule 13 is the
reason: nothing is copied from a referenced game, there is nothing to attribute, and no binary
asset enters the repository (which also keeps the WebGL payload honest). Elite and boss are the
ghost sprite scaled and tinted at exactly the simulation's radius scales, so the silhouette a
player sees is the hitbox the simulation uses.

## 10. Running it

```powershell
# The measurement that chose the projection (Editor menu)
ProjectSurvive/Stage 08/4. Measure camera projection (ADR 0018)

# Unity tests (Editor must be open; MCP or Test Runner)
Editor:  ProjectSurvive.Game.Tests.EditMode    # BattlePresentationTests 15, BattleMiniBagTests 18
PlayMode: ProjectSurvive.Game.Tests.PlayMode   # 6 battle scene tests

# The real player, in a real browser
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Release
npx playwright test tests/web/battle.spec.ts
```

A WebGL build cannot run Unity's test framework, so the scene publishes its own progress as
`window.__projectSurviveBattle` (`scene`, `phase`, `tick`, `stepsRun`, `droppedTicks`, `speed`,
`viewsCreated`, `viewsGrownDuringPlay`, `viewsBound`, `poolCapacity`, `damageNumbers`,
`timelineMarkers`, `droppedEvents`, `hp`/`maxHp`/`mp`/`bia`/`kills`, and `outcome`/`endReason`
once the floor ends). `tests/web/battle.spec.ts` asserts on that document — the same shape the
Stage 03 smoke suite uses — and on a screenshot, rather than on how the page looks.

## 11. Facts that cost time to learn

- **A scene reference is not compiled, so nothing but a PlayMode test can see it.** Stage 08c's
  `BuildBattleHud` created the `BattleMiniBagView` and set its serialized fields, but no
  `SetObjectReference(runner, "_viewSettings.MiniBag", …)` ever followed, so the real scene had a
  null `BattleViewSettings.MiniBag` and drew an empty panel — with a clean compile, a green EditMode
  suite and capture shots that looked correct (that tool finds the component by type and builds it
  itself). The assertion that caught it reads the **scene's** wiring before it reads any value.
- **A destroyed Unity object is not a null reference.** During PlayMode teardown a scene unload
  destroys the pooled renderers before `OnDestroy` releases them, so `if (view != null)` passes
  and the next line throws. Comparisons in `EntityView`, `EntityViewPool` and
  `FloatingDamagePool` go through the `UnityEngine.Object` operator (or an explicit null check now
  that it is overridden) precisely so a torn-down object reads as gone.
- **`BattleConstants.MaxRenderEntities` is 1024 but the pools are 1793.** The pools are sized by
  *kind* (a frame can publish any mixture), the cap is on the whole frame; the pool must be the
  larger of the two or a legal frame could ask for a view that does not exist.
- **The alpha must stay below 1.** `Alpha()` clamps to 0.9999 rather than 1.0, because a full
  tick of accumulated alpha means another step was due in the same frame and the extrapolation
  would land the sprite exactly on the next tick's start — visible as a one-frame skate.
- **Interpolation is unclamped on purpose.** An overshoot of a fraction of a tick is smoother than
  a snap, and the alpha can never become a rule input because no rule can see it.
- **`BattleSceneScope` aliases `EcsBattleSessionFactory`.** The simulation's namespace also
  declares a `Lifetime` component that collides with VContainer's lifetime enum in every
  registration; the alias is the fix, not a preference.
