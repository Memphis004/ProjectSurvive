# ADR 0018 — The 2.5D Battle camera is orthographic at a 50° tilt

- **Status:** Accepted
- **Date:** 2026-10-15
- **Stage:** 08
- **Related:** [ADR 0012](0012-prev-cur-frame-double-buffer.md) (the Prev/Cur frame the
  interpolation reads), [ADR 0017](0017-ecslite-battle-simulation.md) (the arena radii the
  silhouette must agree with), `llmwiki/systems/battle-presentation.md`,
  `UnityProject/Assets/Main/Scripts/Presentation/Battle/BattleCameraRig.cs`

## Context

GDD v0.3 fixes the view as *"สไปรต์ 2D บนฉาก 3D low-poly กล้องเฉียงประมาณ 50°"* and leaves one
choice open: *"เลือก orthographic หรือ perspective ใน Stage 08"*, then makes the experiment part of
the stage work — *"ทดลอง orthographic กับ perspective (depth sorting, การบังตัวละคร) แล้วเลือกด้วย
ADR"*.

So the decision is not "which looks nicer". It has to be settled by measuring the two things the
GDD names, on the rig the scene actually runs:

1. **การบังตัวละคร** — which character is drawn in front when two overlap.
2. **depth sorting** — whether depth is resolved by the projection itself or by the scene.

The constraint that shapes both: the battle simulation is strictly 2D (`RenderEntity.X` / `.Y`,
collision radii constant and depth-independent — `BattleArena`), and the view is a stack of
billboards. Everything the player can see about "who is in front" therefore has to be authored,
because the simulation has no third dimension to sort by.

## Decision

**Orthographic.** `BattleCameraRig` supports both projections (the enum, the two poses and the
rig's constants are all shipped), the Battle scene ships `BattleProjection.Orthographic`, and the
tilt stays the GDD's 50°.

The measurement that decided it is reproducible from the Editor:

```
ProjectSurvive/Stage 08/4. Measure camera projection (ADR 0018)
  -> artifacts/stage08/camera-projection.json
```

It builds a temporary camera through `BattleCameraProbe.CreateAuthoredCamera()` — the same factory
`ProjectSetup` uses to build the scene's camera, so the experiment cannot describe a camera the
game does not run — measures both projections at a fixed 1280×720 probe resolution, and destroys
everything it made.

### Result of the run (Unity 6000.3.9f1, probe 1280×720, pitch 50°, ortho size 14, FOV 35°, perspective standoff 44.402)

| Measurement | Orthographic | Perspective |
| --- | --- | --- |
| Visible ground band (simulation Y) | −18.276 … +18.276 | −14.452 … +24.850 |
| Visible depth (world units) | 36.551 | 39.303 |
| Pixels per world unit, near edge | 16.529 | 14.164 |
| Pixels per world unit, mid (target) | 16.529 | 16.819 |
| Pixels per world unit, far edge | 16.529 | 17.026 |
| **Near ÷ far scale** | **1.000** | **0.832** |
| Sorting-order spread between the two probe rows | 160 | 160 |
| Authored order puts the near row on top (pixel sample) | **true** | **true** |
| A higher sorting order wins (pixel sample) | **true** | **true** |

## Why

### 1. Depth sorting is authored, so the projection cannot be the thing that decides occlusion

The occlusion probe stacks two quads at one point, gives them the orders
`BattleWorldMap.SortingOrder(Enemy, −5)` and `…(Enemy, +5)` (spread 160), renders, and samples the
centre pixel: the near quad wins. It then *moves the order* and samples again: the pixel follows the
order, not the geometry. That is the difference between "sprites happen to be sorted" and "the
scene sorts them", and it holds identically under both projections.

Since occlusion is decided by `BattleWorldMap.SortingOrder` (simulation Y → sorting order, further
away sorts behind, a small per-kind bias so a bolt draws over the ghost it just hit), perspective
buys **nothing** for การบังตัวละคร. It would only add a second, contradictory signal: under
perspective Unity also depth-sorts within one order, so two characters in the same order would swap
front/back as the camera moves, which is exactly the flicker that an explicit order exists to
prevent.

### 2. Under perspective the same ghost is not the same size in two places

pixels-per-world-unit swings from 14.164 at the near edge to 17.026 at the far edge — a 0.832
near/far ratio, i.e. ±9% around the middle. A prototype whose whole visual language is *identical
silhouettes at high count* cannot afford that: the player learns to read "one ghost that size" and
then the same ghost is a fifth smaller or larger depending on which row it stands in. Under
orthographic the number is 16.529 at every depth (ratio exactly 1.000): a silhouette has one size,
and the only thing that changes it is an authored event — an elite's 1.4×, a boss's 2×, a hit
flash's tint.

The shape is uniform for the same reason. The bridge tilts a billboard by the pitch printed from
the rig and never aims it per entity; under parallel projection that orientation is square to the
camera *everywhere*, so every sprite is seen at one angle. Under perspective the near and far rows
are seen at different angles, so sprites of the same art differ in apparent height *and* in apparent
lean as they walk up or down the arena.

### 3. The framing is symmetric, which is what the follow camera wants

Orthographic shows ±18.276 units of ground around the camera's target; perspective shows −14.452 …
+24.850, an asymmetric band: less ground behind the player (toward the camera) than ahead. The
player therefore sits at 37% of the visible depth but 50% of the screen, so the crowd behind the
player leaves the frame sooner than the crowd in front — a chase camera in a surround-style game
should not have a preferred direction it cannot see.

Both projections keep the player centred on screen (the camera looks *at* the player), and neither
covers the full 40-unit arena radius; the difference measured here is about *how* the visible 36–39
units are distributed and how consistently they are scaled, not about one seeing more.

### 4. What was rejected, and why

- **Perspective for the depth cue.** Real, but the cue contradicts the model: collision radii are
  constant while apparent size would not be, so a far row that looks harmless would hit exactly as
  hard as a near one. A legibility gain that disagrees with the simulation is a cost, not a feature.
- **Perspective for "a more 3D scene".** The scene has no verticality — one ground plane, sprites
  standing on it. Foreshortening buys depth that nothing else in the presentation uses.
- **Orthographic with an extra fake depth scale per row.** That is a re-implementation of
  perspective by hand, with the same inconsistency and none of the geometry's honesty.

## Consequences

- **Positive:** one silhouette scale, everywhere. The pool, the elite/boss multipliers, the hit tint
  and the interpolation are the only things that change how a ghost looks.
- **Positive:** occlusion stays single-sourced in `BattleWorldMap.SortingOrder`. No camera-relative
  sort, so a character never flickers in front of another as the camera drifts.
- **Positive:** the camera has no zoom state to keep in step with anything: tilt, target and the
  orthographic size are the whole pose, which is why `BattleCameraRig` is ~90 lines.
- **Negative:** distant entities are not smaller, so the far edge of the arena carries no size cue
  for "that ghost is far away". The compensating cues are the ground grid and the fact that the
  arena is bounded — a 2D game with a bounded arena, which is what this is.
- **Negative:** the projection is a scene-authored value (`BattleViewSettings.Projection`), so the
  perspective variant stays reachable and must keep working. It is covered by the probe above rather
  than by a test that would freeze the enum.
- **Revisit if:** the view ever gains a second height level, real 3D geometry, or a zoom camera.
  Then perspective becomes a question about the *scene* rather than about silhouettes, and this ADR
  is the baseline to re-measure against.
