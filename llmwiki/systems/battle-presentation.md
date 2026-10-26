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

## 12. Pause and the retreat (Stage 13c-2b)

The floor can be held still. **หยุด** is a control in the floor panel (`UiLayoutCheck.PauseButton`),
drawn inside it like the speed toggle is inside the resource panel; it calls
`BattleSimulationRunner.SetPaused(true)`, and the runner's whole `Update` returns before the
accumulator is asked for a step. A paused runner therefore runs **no** steps and copies no frame: the
world stays exactly as the last tick left it, and nothing is owed on resume because
`SetPaused(false)` resets `FixedTicker` rather than letting a menu's worth of wall clock replay into
the arena. The browser's own `OnApplicationPause` is a second, separate flag (`_appPaused`): a hidden
tab and a player's request both stop the clock, but only the second is a game state the overlay shows.

The overlay lives on its own canvas at sorting order 200, with a dim sheet over the whole screen and a
card (`UiLayoutCheck.PausePanel`, alpha 0.98) carrying the heading, the line that says a retreat
forfeits the floor, and the two ways out. The dim sheet is a raycast target on purpose: while the
menu is up, a press cannot reach the HUD under it. `BattlePausePresenter` owns the three actions —
open/resume through the runner, and the retreat through the Application layer:

- `RunSession.AbandonFloor(currentHp)` (Stage 13c-2b) is **not** a surrender. `ApplyBattleResult`
  ends the run on a defeat, so a retreat cannot travel through it; the floor is left unsettled, the
  HP the last frame reported is carried back (clamped by the run, never by the caller), the bag is
  unlocked and the run returns to the camp it departed from with its purse, floor cursor and shop
  offers intact. The floor can be departed for again, and nothing was paid for it.
- The scene change is **requested**, not performed: `BattleSceneDirector` owns the loader and retries
  a refused move, exactly like the summary screen does.

`BattlePauseView` publishes `window.__projectSurvivePause` (scene, canvas size, open, paused, tick,
hp, requested scene, the five labels and the centres of the controls a click can currently reach) and
rewrites it twice a second **only while open**. `tests/web/pause.spec.ts` is built on that: the freeze
is proved by that document's `tick` staying put, then by the battle document's `stepsRun` passing it
once เล่นต่อ was pressed.

## 13. The card forest arena (Stage 08e)

The arena around the fight is scenery, never geometry. `ArenaLayout` is a pure function from
(`ArenaTheme`, seed) to a list of placements — its own 32-bit LCG, never `UnityEngine.Random`, so a
rebuild does not depend on what else the Editor ran first — and `ArenaBuilder` instantiates
`ArenaCard` / `GroundDecal` prefabs at those placements through the Editor API, marks them static and
saves `Battle.unity`. Nothing is created or destroyed while a floor runs: the measured
`viewsGrownDuringPlay` stays 0.

**Zones are measured, not guessed.** The camera is orthographic at 50° with size 14, so at either
16:9 evidence resolution it sees `x ±24.889`, `simY ±18.276` around the player (36.551 units of
depth). That is the number the density bands are built from: the middle 0–12 units holds only low
grass (ceiling 0.75) and ground decals, the ring 12–26 holds sparse bushes with a minimum spacing,
and the wall is 26–40 — the outer ten units of a field whose radius is the simulation's
`BattleArena.Radius` (40), mirrored in `BattleViewCaps.ArenaRadius` and asserted against it.

**The wall is sized by card width, not by a spacing number.** Each layer's slot count is
`circumference / (average card width × theme.BorderSlotPitchFactor)`; at the shipped 0.85 the
neighbouring cards overlap and three layers read as forest instead of as a row of trees. It includes
`tree-big-a/b/c` (9–10.4 units wide, 13.5–15.5 tall) so a player who walks to the edge has canopies
passing in front of them. Caps live in the theme (`BorderTreeMin/Max` 72–96); the shipped seed
places 141 pieces (15 centre, 24 ring, 74 border, 28 decals) at signature `c1ffaa84`.

**Two rules keep the arena and the actors in one coordinate system.** A card's rotation is
`ArenaCard.FacingCamera()`, which quotes `BattleCameraRig.PitchDegrees` — the same 50° the sprite
path gets from `BillboardPitch`, so no second copy of the angle exists; and who hides whom is
`BattleWorldMap.PropSortingOrder(z)` written into `MeshRenderer.sortingOrder`, with the card shader
in the transparent queue writing no depth and testing none. The camera's depth buffer never decides
an occlusion, which is what lets the cards interleave with ghosts and the player by row.

**A tree that hides the player thins out instead of disappearing.** The wall is walkable, so a
canopy can land exactly over the ghost-doctor. `ArenaCardFade` (one component on the arena root,
target = the lantern that already follows the player) decides which cards cover them with a pure
function, `Covers(cardX, cardZ, width, height, playerX, playerZ, margin)`: a card only ever hides
something *in front of* it by sorting order, and a card `H` tall reaches `H / tan(50°) ≈ 0.84H`
ground units up the screen, so the test is `cardZ ≤ playerZ ≤ cardZ + 0.84H` plus half the card's
width. Covering cards are dithered — a 4×4 Bayer discard in `ArenaCard.shader`, driven per renderer
by a `MaterialPropertyBlock` on `_Fade` — down to `_minFade` 0.22 rather than to zero, because a
tree that vanishes leaves a hole in a forest that otherwise never moves. The dither is also why the
card prefabs are **not** `BatchingStatic`: static batching draws through a combined mesh and ignores
the block, so the fade would read back as set and draw solid. The cards keep their own
`sortingOrder`, so no batch was possible anyway.

The dirt patch is a *multiply* decal (`ProjectSurvive/ArenaDirt`, `Blend DstColor Zero`) rather than
an alpha-blended polygon: the floor is the brightest surface the fight is read against, and a decal
laid over it could only ever lift it.

Evidence and the acceptance table are in [the stage report](../../docs/stages/08e-report.md); the
capture is `ProjectSurvive/Stage 08e/1. Build arena (theme + seed)` plus
`AgentScripts/ArenaEvidenceCapture.Run` (player centred and near the edge at 1920×1080 and
1280×720, with 300 ghosts: 133–172 batches, 2.4–2.6 ms/frame in the Editor on an i5-14400F /
RTX 5060 Ti). `AgentScripts/ArenaFadeCompare.Run` is the dither's own pair: it walks to the edge,
freezes `Time.timeScale` so the sway stops, and captures the same frame with the fade on and off —
the two images differ only where the dither removed pixels
(`stage08e-dither-compare.png`).

## 14. Two looks from one set of assets (Stage 08f)

Dusk is the shipped look and Night is the same arena under a colder key; neither is a different
scene, a different material or a different texture. `ArenaLightingProfile` is a ScriptableObject that
owns ambient, the single directional light, the shadow colour and its one angle, fog, the screen
top band, the fake-light intensities and the post grade, and the arena's shaders read it through
globals only (`_ArenaTint`, `_ArenaLightPool`, `_ArenaGlow`, `_ArenaShadowColor`,
`_ArenaShadowStrength`, `_ArenaScreenGradient`). `ArenaEnvironmentView.SetTimeOfDay` swaps two
profiles; an EditMode test and the capture's own `assets unchanged by the profile switch: True`
assert that no material's colour, texture or render queue moved.

**Shadows are decals with one direction.** A card writes no depth and 300 ghosts leave no room for
a shadow pass, so each standing card gets a soft quad on the floor: length =
`height × ShadowLengthPerHeight`, width = the card's own, oriented from
`ArenaLightingProfile.ShadowDirectionDegrees` (245° at both profiles). There is exactly one angle in
the scene and a test reads it back off the placed transforms. The wide canopy patch exists only
under border trees (`ArenaShadowCard.IsCanopy`, 74 of 187); the open centre may not hold one. All
shadows sit in one render queue below every actor order (`BattleWorldMap.ShadowSortingOrder`), so a
shadow can never cover a ghost, a pickup or the player.

**~190 shadow quads are one draw call.** As scene renderers they cost a batch each and took the
300-ghost frame to 205; `ArenaCardShadowLayer` bakes their matrices once and draws them with one
`Graphics.DrawMeshInstanced`, disabling the individual renderers. The builder still places one
`ArenaShadowCard` per shadow, so the direction/zone/sorting tests read the scene rather than a
parallel list. `ArenaScreenGradient` is a camera-space canvas at sorting order −100 (below the HUD) —
URP fog darkens by camera distance, which is not the screen's vertical axis under an orthographic
rig — and `ArenaContrast` measures readability as the WCAG luminance ratio of a group's 90th
percentile against the median floor ring around it, on the pixels of the saved PNG itself.

**The lantern's own halo is part of the floor the figure is read against (Stage 17b follow-up).**
`GlowIntensity` draws the additive halo around the doctor and moves with him, so it lifts the floor
under his feet from ~0.09 to ~0.31 relative luminance and puts a white robe at 2.3 : 1 against its
own light instead of the 6–7 : 1 it gets a step away. Dusk's halo is therefore 0.45 and its light
pools 0.15 (0.85 / 0.24 before); night keeps its stronger glow because a dark floor is what that
halo is for. Measured on two play-mode frames of the same floor: robe-vs-halo ring 3.71 → 4.74,
robe-vs-floor 7.06 → 7.16, halo-vs-floor 1.91 → 1.51.

**The haze across the middle is a profile value, not a screenshot artefact (Stage 17b follow-up).**
`ScreenMidGradient` / `ScreenMidColor` draw a second `Image` on the same below-the-HUD canvas, on its
own sprite (`screen-mid-band.png`: opaque at the frame's vertical middle, zero above 18 % and below
86 %), so the band lifts the strip the fight is read in and leaves both edges alone. Dusk 0.10 of
`(0.62, 0.63, 0.55)`, night 0.10 of `(0.10, 0.14, 0.22)`, the Camp's profile 0 (wired, disabled).
It is a veil over the actor too: measured on one play-mode frame it takes the doctor from 6.97 : 1 to
**5.62 : 1** against the ground under him (0.16, the first value, measured 4.97 and veiled the middle
of the frame more than the look wanted), so it is atmosphere with a price tag — the strength is the
knob, and a larger figure, not more haze, is what raises salience. The band's canvas sorts at **−100**
against the HUD's **0**, so it draws *under* the panels; those are translucent (alpha 0.88–0.98), which
is why their colour moves by up to 6/255 between the two strengths while everything above y = 104
(14 % of the height) does not move at all.

**A photograph of the arena must apply the profile first.** `ArenaScreenGradient`'s `Image` is
authored white and `ArenaEnvironmentView.Apply` (called from `Awake`) is what turns it into the
profile's near-black green. A picture taken in Edit Mode therefore showed the top band at full
strength in **white**, washing the upper half of every arena and camp shot — a haze the game never
draws, and enough to misread a white robe as illegible. Both builders now write the authored band
into the scene, and `UiCaptureTool` applies the profile before it photographs; `BattleCaptureState`
also lays the runtime floor (one quad, `ArenaTheme.GroundMaterial`, 80 × 80) so the picture has the
ground the game draws rather than the camera's clear colour.

Readability thresholds live in `ArenaTheme` (player 2.2, ghost 1.9, pickup 2.4, telegraph 1.3) and
are adjusted by changing the lighting, never the number. Evidence: `docs/stages/08f-report.md`,
`AgentScripts/ArenaStage08fCapture.Run` writes `stage08f-contrast.txt` / `stage08f-metrics.txt` next
to the six captures in `artifacts/ui/battle-env/`.

## 15. The doctor is a Spine figure (Stage 17b)

`SpinePlayerView` replaces the pooled placeholder sprite for **one** entity — the player — and nothing
else about the frame changes. The player is still `RenderEntityKind.Player` in the snapshot; the only
difference is who draws it. `BattleRenderBridge.PlayerDrawnElsewhere` is set from whether the figure
actually built, and while it is true the bridge does not acquire a view for that kind, so a floor can
never show two doctors. The figure is the scene's own object under `ViewRoot` (built by
`ProjectSurvive/Stage 08/3`), not a pooled view: its counters are its own (`BuildCount`,
`PlayCalls`), which is why `viewsCreated` / `viewsGrownDuringPlay` stay 0.

**Everything the figure is told comes out of the frame.** `PlayerFigurePresenter` reads the player's
`RenderEntity` and its previous tick, so
`speed = |(current − previous)| × TickRate` (units/second), the pose is `PlayerPoseSelector`'s call on
that speed, the facing is the sign of `dx`, the death pose is `BattlePhase.Lost` from the floor's own
result, and the draw order is `BattleWorldMap.SortingOrder(Player, z)` written **only when the row
changes**. It stands at `Quaternion.Euler(BattleCameraRig.BillboardPitch, 0, 0)` — literally the
rotation `BattleRenderBridge.Orientation` gives a sprite, so a figure and a ghost cannot lean
differently. Position is `BattleWorldMap.Interpolate(from, to, alpha)`, the same interpolation the
sprites use.

**The hurt flinch is an event, and it decides nothing.** `PlayerDamaged` is read through the VFX
channel's listener (`BattleEventChannel.Vfx`, the one `BattleAudioListener` uses for audio), and it
plays the profile's `hit` animation once; a dropped event costs a flash, never HP. `Play` is called
once per pose change, never per frame — the report's `spinePlayCalls` is what makes that measurable in
a player build.

**No clock of its own.** The animation state's `TimeScale` carries the 1×/2× control and the pause:
`BattleSimulationRunner.SetPaused` → `PlayerFigurePresenter.SetPaused` → `SpinePlayerView.SetPaused`,
and `BattleSpeed.Multiplier` → `SetSpeed`, both only when the value changed. `Time.timeScale` is
never touched, so the HUD and the pause overlay keep running while the floor is held.

**The ink is one screen pixel, in one pass.** The figure draws with
`ProjectSurvive/SpinePlayerOutline` (`Assets/Main/Spine/Shaders/`), not with spine's
`Spine/Outline/Skeleton`: that shader puts ink and body in two passes of one SubShader, and **URP renders
exactly one untagged (`SRPDefaultUnlit`) pass per material**, so the first attempt drew the doctor as
thin dark lines with no body at all (`artifacts/ui/battle-player-1920x1080.png` before the shader
existed). The project's shader keeps the body as a verbatim copy of `Spine/Skeleton`'s Normal pass and
computes the ink from eight samples stepped by the UV derivatives — a screen pixel at any camera size —
created by `ProjectSurvive/Spine/4. Create player outline material`, which is also what points the
profile at it.

**A figure that fails to build is loud in dev and documented in release.** `Begin()` failing logs the
view's `noun.problem` code as an error in the Editor and a development build (the floor then draws the
placeholder sprite, and the capture's report says so); a release build logs one warning instead of
losing the doctor silently. `__projectSurviveBattle` publishes `spineReady`, `spineErrorCode`,
`spinePlayCalls` and `spineAnimation` so a browser test can tell the Spine render from the fallback.
Evidence: `docs/stages/17b-report.md`, `artifacts/ui/battle-player-1920x1080.png`.

## 16. The status panel's figure (Stage 17d)

The placeholder bust is gone: the panel's figure is the doctor's own skeleton, drawn by a
`BattleHudPortrait` that builds **one** `SpineUiActor` (a `SkeletonGraphic`) inside the 72 × 120
box anchored to the status panel's top-left corner. It is the same `SpinePlayerProfile` the arena
and the camp name, so the panel and the floor cannot show two different doctors, and the one
material is a serialized reference from `ProjectSurvive/Spine/3. Create UI materials`
(`SpineGraphic-Normal.mat`) handed to the actor through `SetMaterials` — no `Shader.Find` in
runtime code, because a stripped player returns null for a shader looked up by name. The
SkeletonGraphic's own clock is pinned (`timeScale = 0`) and the portrait ticks its own animation
state, exactly as the world figure does; the mesh is scaled and centred into the box once, by
`MatchRectTransformToBounds` plus one uniform scale, and a second `Begin` builds nothing
(`CreatedAfterBuild` stays 0). It shows one looping idle and decides nothing — no hurt, no death,
no numbers. The Canvas' `additionalShaderChannels` are only touched when a tint-black actor is
built (`SpineUiActor`'s own rule; the HUD portrait is not tint-black).

The box is a measured region now (`UiLayoutCheck.Portrait`), so “the picture does not land on the
HP/MP rows” is a verdict at 1920×1080 and 1280×720 rather than a look: 8 px between the portrait's
right edge and the HP caption, 4 px above the possession line. One capture caveat, recorded rather
than hidden: `UiCaptureTool` borrows the overlay canvas into camera space to photograph it, and the
arena's foreground cards then sort above the panels at the bottom edge, so the Editor pictures hide
the lower HUD band the running game draws on top; the geometry and the overlap counts are still
measured from those shots, and the look is judged from a play-mode frame
(`artifacts/stage17d/battle-portrait-1920x1080.png`). Evidence: `docs/stages/17d-report.md`,
`BattleHudPortraitTests` (EditMode), `BattlePlayModeTests.The_status_panels_portrait_is_the_spine_figure_built_once`.

## 17. The bust is the halfbody, held still (Stage 17e)

17d's figure was the **walker's** skeleton in a 72 × 120 box; 17e gives the panel its own art: the
halfbody export `Resources/Portrait/2110101` (656 × 554, one bone, 19 slots, 11 skins, one zero-length
`stand` animation), drawn by the same `BattleHudPortrait` → `SpineUiActor` path in an **80 × 110** box
with a `RectMask2D` on the box. Nothing is animated: `Begin` composes the profile's skin parts, plays
`stand` at **speed 0 / time 0** (or leaves the setup pose when a profile names no animation), and
there is no per-frame update at all — `PlayCalls` stays 1 and `AnimationTime` stays 0 for a whole
floor, which is what the PlayMode case measures. The parts stay separate (body, face, face
decoration, hair, hat) so a later customisation screen can swap them; the pick lives in
`SpinePortraitProfile`, never in the view.

The atlas arrived with **no materials**, so `SpinePortraitTool` (`ProjectSurvive/Spine/4. Create portrait
profile`) creates `SpinePortrait-2110101.mat` from the project's own SkeletonGraphic template over the
page texture and assigns it to the atlas asset as the texture source; the source game's outline/stencil
shader is not copied. The one declared-empty-slot list (`expectedEmptySlots`) now exists for both
profiles, so the four `w_*` slots on the walker and the thirteen unused face/hair slots on the bust are
silent while an unexpected gap still warns. Evidence: `docs/stages/17e-report.md`,
`BattleHudPortraitTests`, `BattlePlayModeTests.The_status_panels_portrait_is_the_still_halfbody_built_once`,
`artifacts/ui/battle-begin-1920x1080.png`.

## 18. Facing is mirrored once, and the art already faces left (Stage 17e)

The art in `body_1110901` is drawn facing the viewer's **left** in `stand`, `idle5` and `run`
(measured from close-ups of the running game, each direction on its own: facing +1 is
`artifacts/stage17e/zoom-A-run-right.png`, facing −1 is `zoom-B-run-left.png`, `stand` at +1 is
`zoom-C-stand-right.png` — the face and the trailing sash both follow the direction). The profile therefore says
`artFacesRight = false`, and `SpinePlayerView.ApplyFacing`'s one rule (`flip = ArtFacesRight ? facing < 0
: facing > 0`) writes the mirror: walking right is the mirrored figure, walking left is the plain one.
Both scenes feed the same field through the same method — the camp from the key direction, the arena
from the frame's `dx` — so the fix is the profile value, not a second sign in either scene. Both
profiles now carry it (`SpinePlayerProfile_Camp.asset` / `_Battle.asset`), and the PlayMode cases read
`SkeletonTransform.localScale.x` against `artFacesRight` and the facing the frame produced.
