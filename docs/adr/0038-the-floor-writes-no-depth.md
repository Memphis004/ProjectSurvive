# ADR 0038 — The arena floor writes no depth

- **Status:** Accepted
- **Date:** 2026-10-27
- **Stage:** 08h
- **Related:** [ADR 0018](0018-battle-camera-projection.md) (orthographic at a 50° tilt, occlusion
  decided by sorting order), `docs/stages/08e-report.md` (the floor and the card shader),
  `UnityProject/Assets/Main/Shaders/ArenaGround.shader`,
  `UnityProject/Assets/Main/Scripts/Presentation/Battle/BattleViewMap.cs` (`BattleWorldMap.SortingOrder`),
  `docs/stages/08h-report.md`

## Context

Stage 08e made the arena floor an **opaque, depth-writing** mesh (`ArenaGround.shader`: `ZWrite On`,
`ZTest LEqual`, queue `Geometry` 2000). The intent was "the floor is underneath, so everything on it
draws over it" — which was true, and is also true by render queue alone.

Stage 08h was opened because, with the floor switched on, **every actor's lower half disappeared**:
ghosts, bullets and pickups were drawn as a half-disc / half-ghost sitting on the ground, and the
whole sprite came back when the floor renderer was disabled.

Measured on the running scene (Unity 6000.3.9f1, Editor Play Mode, `artifacts/stage08h/`):

| thing | value | how |
| --- | --- | --- |
| floor material | `ground`, `ProjectSurvive/ArenaGround`, queue **2000** | read from the renderer |
| floor pass | `ZWrite On`, `ZTest LEqual` | read from the shader asset the material uses |
| actor sprite material | `Sprite-Unlit-Default` (URP 2D), queue **3000**, `ZWrite [_ZWrite]` (off), **no `ZTest` stated → Unity's default `LEqual`** | read from a pooled `view-ghost` |
| billboard placement | `position = BattleWorldMap.ToWorld(x, y)` = **(x, 0, y)**, `rotation = Euler(50°, 0, 0)`, sprite pivot **(0.5, 0.5)** | `BattleRenderBridge`, `BattlePlaceholderArt.Finish` |
| the sprite's bottom edge | **y = −0.3214 u** for a 1 u sprite (0.5 × cos 50°) | measured in the scene |
| lower-quarter pixels drawn, floor **on** | **0.000** for ghost, elite, boss, bolt … | hidden/reveal pixel diff, 1920×1080 and 1280×720, dusk and night |
| the same, floor **off** | 0.169 (ghost) … 0.594 (bolt) | same measurement |

So the sprite is a billboard whose **centre** sits on the ground plane. Half of it hangs below that
plane, and the floor's depth write discarded exactly those fragments — the sprite material's default
`ZTest LEqual` is what makes the floor's depth visible to it. This is a second, contradictory
occlusion rule, which is the thing [ADR 0018](0018-battle-camera-projection.md) and Stage 08e's own
card work deliberately avoided: in this arena *who hides whom* is authored by
`BattleWorldMap.SortingOrder`.

## Decision

**The floor keeps its queue (2000, drawn before everything that stands on it) and stops writing
depth.** One line in `ArenaGround.shader`: `ZWrite On` → `ZWrite Off`.

## Why this and not the alternatives

- **Move the pivot / lift the sprites so the whole quad is above the plane.** This is the other
  honest candidate: a sprite with a base pivot at y = 0 would never dip below the floor. Rejected
  because it is not one point but a new coordinate convention — the pooled views, the telegraph
  decals, the floating numbers, the Spine figure's host, the elite/boss scale anchors and the blob
  shadows all place something from the same simulation position, and every one of them would have to
  move by half its own (scale-dependent) height. It also changes the silhouette the player reads: the
  ghost's body would start at its feet instead of straddling the ground, and the entity would no
  longer be centred on the point the simulation collides at.
- **Give the actor materials `ZTest Always`.** Rejected as the same class of mistake in the other
  direction: it silences the depth test for the sprite but leaves the contradictory rule in place for
  anything else that hangs below the plane (a telegraph drawn flat on it, a decal, a future prop), and
  it would also make the sprite ignore any real occluder the scene ever gains.
- **Make the floor's depth test `Always` but keep `ZWrite On`.** Rejected: it hides the symptom for
  the floor's own fragments and keeps writing a depth value that still cuts the sprites.

Writing depth bought the floor nothing. It is a single flat quad at the bottom of the scene, drawn
first by queue; the cards use `ZTest Always` on purpose, the decals and shadows are transparent with
`Offset -1,-1`, and the actors are sorted by order. Nothing in the arena needs the floor in the depth
buffer, and one thing was actively hurt by it.

## Consequences

- **Positive:** every billboard draws its whole silhouette at both canvas sizes and both lighting
  profiles; the floor's contribution to a billboard's pixels is now nil (measured: floor on and floor
  off agree to ≤ 0.03 of a band, `artifacts/stage08h/stage08h-coverage-after.txt`).
- **Positive:** the telegraph ring, which is coplanar with the floor (`y = 0`, rotated 90°), no longer
  z-fights against it at all.
- **Negative:** the floor can no longer occlude anything below its plane. Nothing in the arena is
  below the plane except the lower half of a billboard, which is precisely what must stay visible, so
  this is not a loss today.
- **Negative:** a future feature that puts real 3D geometry under the floor (a pit, a ramp) cannot
  rely on the floor hiding it; it would need its own occluder with depth on.
- **Revisit if:** the arena gains a second height level or real 3D props that must be hidden by
  ground. Then the choice is per-object (`ZWrite On` for that object), not a floor-wide rule.
- **Locked by tests:** `ArenaEnvironmentTests.TheFloorWritesNoDepthAndIsDrawnBeforeEverythingOnIt`
  (queue 2000, below the decals' 2950, `ZWrite Off` in the pass) and
  `ArenaSpriteFloorDepthPlayModeTests.AnActorsLowerHalfSurvivesTheFloorAtBothSizesAndBothProfiles`
  (ghost, elite, boss, bolt, pickup × 1920×1080 and 1280×720 × dusk and night — fails on `ZWrite On`,
  passes on `ZWrite Off`).
