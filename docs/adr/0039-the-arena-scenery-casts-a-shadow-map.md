# ADR 0039 — The arena scenery casts a real shadow map; the camp keeps decals

- **Status:** Accepted
- **Date:** 2026-10-28
- **Stage:** 08g (item 4)
- **Related:** [ADR 0018](0018-battle-camera-projection.md) (orthographic at a 50° tilt, occlusion
  decided by sorting order), [ADR 0038](0038-the-floor-writes-no-depth.md) (the floor draws first and
  writes no depth), `docs/stages/08f-report.md` (the per-card decal shadows this replaces),
  `UnityProject/Assets/Main/Shaders/ArenaCard.shader`,
  `UnityProject/Assets/Main/Shaders/ArenaGround.shader`,
  `UnityProject/Assets/Main/Scripts/Editor/ArenaBuilder.cs`,
  `UnityProject/Assets/Main/Scripts/Presentation/Battle/Environment/ArenaLightingProfile.cs`,
  `docs/stages/08g-report.md`

## Context

Stage 08e/08f gave the arena its shadows as **painted decals**: one small multiply quad per standing
card plus a wider crown patch under each border tree, all instanced into a single draw call by
`ArenaCardShadowLayer`, every one of them oriented from one authored angle
(`ArenaLightingProfile.ShadowDirectionDegrees`) and never drawn over an actor
(`BattleWorldMap.ShadowSortingOrder`). To match that, the cards were `shadowCastingMode = Off` and
Battle's key light was `LightShadows.None`.

Stage 08g item 4 asks for the other thing: "cards of trees/bushes/landmarks cast shadow through alpha
clip, and the moon is slanted so the shadows stretch long; the characters keep their blob shadow".

Read off the shipped scene before the change (`artifacts/stage08g/`, Unity 6000.3.9f1, Editor Play
Mode): 126 scenery cards, key light rotation `(58, 320, 0)` — 58° from vertical, i.e. already slanted
by the Night-profile work of item 3 — every card `shadowCastingMode Off`, `receiveShadows false`, key
light `LightShadows.None`, `RenderSettings.sun` unset, and `ArenaCard.shader` carried no
`ShadowCaster` pass at all. Nothing in the scene could produce a shadow map, so the decals were the
whole shadow language of the frame.

## Decision

**Battle's scenery casts through the URP main-light shadow map, and the floor is the surface that
receives it. The camp keeps the decal shadows.**

- `ArenaCard.shader` gains a `ShadowCaster` pass that samples the same `_BaseMap` alpha and
  `clip(alpha - _Cutoff)`s the transparent corners, and repeats the foliage sway so the silhouette
  does not lag behind the rendered canopy.
- `ArenaBuilder` sets every Battle card `shadowCastingMode = On`, `receiveShadows = false`, gives the
  key light `LightShadows.Hard` and makes it `RenderSettings.sun` (URP's main light).
- `ArenaGround.shader` samples the map (`GetMainLight(TransformWorldToShadowCoord(...))`) and
  multiplies the floor's tint by the shadow coverage, using the profile's own `ShadowColor` /
  `ShadowStrength` as the shadow tint, so switching Dusk/Night still changes nothing on disk.
- `BuildCardShadows`, the `CardShadows` root and `ArenaCardShadowLayer` are removed from the Battle
  scene. `ArenaCardShadowLayer`, `ArenaShadowCard` and `ArenaArtBuilder.BuildCardShadowPrefab`
  survive because `CampEnvironmentBuilder` still draws the camp's decal shadows with them.
- The actors are untouched: they keep `ArenaBlobShadows`, and cards do not receive shadows, so the
  cards themselves stay flat and only the floor gets darker.

## Why this and not the alternatives

- **Keep the decals and dress them up.** Rejected on what a decal fundamentally is: an authored shape
  placed from one global angle. The moonlight is per profile (`(58, −40)` by day-dusk, `(46, −62)` at
  night), and the brief asks the shadows to stretch long across the floor from that angle; a decal
  cannot follow a light that changes angle with the time of day, cannot shorten or lengthen with the
  canopy's own height except by another authored factor, and — the thing the brief is actually about —
  cannot clip a canopy out of the card's own alpha. Shadow shapes would have to be drawn per style.
- **Shadow map for Battle and keep the decals as well.** Rejected as double darkening: the two would
  multiply on the same floor pixels and neither would be the authority, so no number could be tuned
  without re-measuring the other.
- **Give the cards a `ShadowCaster` pass but light them with `LightShadows.Soft`.** Not chosen:
  soft filtering buys a blurred edge on a 2.5D card silhouette at the cost of the quality level's
  filter taps; `Hard` is what the pixel measurement above was taken with, and item 4 asks for the
  shadows to read as long stripes, where a crisp edge helps.

What made this cheap enough to be a change and not a new stage: the arena has exactly one directional
light, so it needs exactly one shadow map, and the map is only 4 extra batches.

## Consequences

- **Positive:** measured contribution at the shipped framing (key light casting vs. not, frozen
  frame, 1920×1080) — 2.80 % of pixels move, mean absolute channel change 0.142, largest single
  channel change 19/255; at a diagnostic wide framing 5.39 % of pixels move with the tree shadows
  visible across the floor (`artifacts/stage08g/shadow-map-check.txt` and the `shadow-map-*-on/off.png`
  pairs).
- **Positive:** the cost stays inside Stage 08f's ≤ 200-batch budget: 65 batches / 23 set-pass /
  2.32 ms per frame with the map on, 61 / 21 / 2.29 ms with it off — **+4 batches, +2 set-pass,
  +0.03 ms** at 1920×1080 (`artifacts/stage08g/shadow-map-check.txt`).
- **Negative:** a shadow map is a fixed-resolution depth texture, so the canopy edges alias at long
  distances and the map can only cover what the light's frustum holds; the decals had no such limit.
- **Negative:** the cards no longer receive shadows (`receiveShadows false`) — if a card must ever be
  darkened by another card's canopy, that is a second receiving path, not a flag.
- **Revisit if:** the arena gains more shadow-casting lights (one map per light) or a second quality
  level is added — then the shadows toggle (`LightShadows.None`) is the first thing to bind to it.
- **Locked by tests:** `ArenaEnvironmentTests.ArenaCardShaderUsesItsAlphaClippedShadowCasterPass`
  (the pass exists, clips on alpha, and the prefab default stays `Off` so the camp is unaffected),
  `BattleCardsCastAlphaClippedShadowsFromTheSlantedMainLight` (every card casts, none receives, the
  light is `Hard`, it is `RenderSettings.sun`, and its rotation is genuinely slanted),
  `ArenaFloorAppliesTheMainLightShadowCoverageTint` (the floor samples the map and tints by coverage),
  `ActorBlobShadowsRemainSeparateFromTheCardShadowMap` (the actors still have their own blob layer).

## Update — the shadow volume, and an open defect (2026-10-28, second round)

**The volume has to reach the frame, not just exist.** The decision above was measured with URP's
`shadowDistance` at 50, which is shorter than the distance from the camera to the far edge of the
ground the frame shows (`standoff ~60 u + OrthographicSize / sin 50° = 11.6 u`). The map was therefore
faded out *inside* the frame: a sweep of the key light's elevation from 42° down to 20° measured the
same 35/255 at every ground point, which is what a shadow cut by the volume looks like, not what a
shorter shadow looks like. `shadowDistance` is now **80**, chosen as the visible ground's far edge plus
margin rather than an engine default, and `ArenaEnvironmentTests` fails if it drops below that edge
again (verified by setting 50 back and watching the test go 0/1).

The azimuth mattered as much as the length: euler.y −40° sent the shadow up and out of the left edge of
the frame. Dusk is `(62, −22)` and Night `(58, −26)`, with `ShadowStrength` raised (0.85 / 0.90) so a
longer shadow still reads as a shadow. The measured effect: pixels that move went from 2.80 % to
**5.56 %** at the shipped framing (13.01 % at the wide diagnostic one), while the annulus around the
player is unchanged to the pixel (0.00 %, 12 562 samples) — the shadows fall in the border bands, not
under the actor the player is reading.

**Open defect: the shadow's silhouette is a quad, not foliage.** A zoom of the wide capture, and a
frame rendered with every card texture replaced by a fully transparent one (the cards vanish, the
shadows do not), both show each caster's *full quad* in the shadow map. The `ShadowCaster` pass clips
on `_BaseMap.a`, and the source has not changed since this ADR was written, so something between the
material and the pass is dropping the sample — this round ruled out the shadow map's mip level
(2048 px over 80 u puts a 10 u card at mip 1), the volume (fixed above), and the pass's CBUFFER layout
(identical to the forward pass field for field), and did not find the cause. Until it is fixed, the
consequence above ("the canopy edges alias") understates the problem: there are no canopy edges in the
map at all. Evidence: `artifacts/stage08g/zoom-shadow-shape.png`, `alpha-clear-on.png`,
`shadow-alpha-probe.txt`.
