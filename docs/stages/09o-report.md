# Stage 09o — ศาลาพัก's yard (cards + light, the 08e/08f systems on the camp)

## 1. Summary

The camp now stands in its own clearing: a `CampTheme` (a second `ArenaTheme`), a `CampYard`
generator that reuses `ArenaLayout.Build` under a fixed seed and keeps only the pieces that can
frame this camera, and the arena's unchanged `ArenaCard` / `GroundDecal` / `ArenaShadowCard` /
`ArenaEnvironmentView` / `ArenaLightingProfile` doing the work. Two camp profiles (Dusk-Camp,
Night) are one call apart, every station and the เซียมซี cylinder has a warm pool under it, and
nothing in the yard is a collider or a raycast target. The camera, walk area and all usage points
are untouched.

## 2. Survey and acceptance

The pre-change survey (camera rig, visible ground extent at both sizes, walk area, station
positions, "before" frames) is recorded in [`08h-report.md`](08h-report.md) — the path the prompt
named.

**Acceptance, item by item**

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Survey first, recorded | PASS | `08h-report.md`; before frames `artifacts/ui/camp-env/before-before-*.png` |
| 2 | Camera unchanged | PASS | `08h-report.md` §1 values still in the scene; `CampYardTests` reads `WalkArea` |
| 3 | CampTheme: decals/grass only inside the walk area; frame outside it; no collider, no raycast, clicks still land | PASS | `CampYardTests.NothingTallStandsInsideTheWalkArea`, `.CardsAndDecalsCarryNoCollider`, `.TheYardInTheSceneIsNeitherSolidNorClickable`; `CampWorldPlayModeTests.The_yard_never_stands_between_the_pointer_and_a_station`, plus the four existing click/rent tests |
| 4 | Dusk-Camp warmer and brighter than the arena's dusk, pools around the sala and shelves, softer vignette, one call to Night | PASS | `CampYardTests.DuskCampIsWarmerAndBrighterThanTheArenasDusk`, `.EveryUsagePointHasWarmLightUnderIt`, `.OneCallSwitchesTheCampsLook` |
| 5 | Readability measured on the capture, thresholds in the theme, real numbers | PASS (props/player), non-regression for the price board | `stage09o-after-contrast.txt` — see §3 |
| 6 | One shadow direction, no large patch on the walkable floor | PASS | `CampYardTests.NoShadowDrawsALargePatchOnTheWalkableFloor`, `.EveryShadowFollowsTheProfilesOneDirection` |
| 7 | Yard belongs to the scene, built once, destroyed with it | PASS | `CampEnvironmentBuilder` authors into `Camp.unity`; nothing is Instantiated/Destroyed at runtime |
| Batch budget (08e: ≤ 200) | PASS | `stage09o-after-metrics.txt`: 71–100 batches, 1.90–2.67 ms/frame |
| Full acceptance images | PASS (files) | `artifacts/ui/camp-env/after-{dusk-camp,night}-{centre,shelf0..3,bag}-{1920x1080,1280x720}.png` |

**Suites actually run**

- `tools/test.ps1 -Suite Fast` → **13 PASS / 0 FAIL / 3 NOT RUN** (151 s): dotnet **612/612**,
  Unity EditMode **250/250**.
- `CampWorldPlayModeTests` (play mode, one fixture) → **9/9 passed**, including the new yard test.
- `SceneFlowPlayModeTests` (play mode) → **6/6 passed**, 52 s (its 20-round scene loop included).
- `UiLayoutCheckTests` runs inside the EditMode assembly → passed in the 250/250.

## 3. Readability numbers (measured on the saved pixels)

Thresholds: `MinPlayerContrast` 2.20, `MinPropContrast` 1.90, `MinLabelContrast` 2.00 — all three
are fields of `CampTheme`.

| view | player | shelf | cylinder | pot | price | pass |
| --- | --- | --- | --- | --- | --- | --- |
| dusk centre 1080p | 5.97 | 4.78 | 5.18 | 3.92 | 2.44 | true |
| dusk centre 720p | 5.97 | 4.70 | 5.18 | 3.92 | 2.18 | true |
| dusk shelf0 1080p | 5.09 | 5.08 | 5.08 | 3.92 | 2.72 | true |
| dusk shelf2 1080p | 4.93 | 5.08 | 5.18 | 3.92 | 2.89 | true |
| night centre 1080p | 4.10 | 4.63 | 5.18 | 3.92 | 2.44 | true |
| night shelf1 1080p | 3.70 | 5.08 | 5.18 | 3.92 | 2.67 | true |

All 20 captured views pass (both profiles × centre + four shelves × both sizes; the panel and the
drum views are the two extra states).

**The price board is a separate finding, not a lighting problem.** Its contrast never moved between
the before capture (1.95–2.16) and the after capture (2.18–2.89) because the boards are unlit UI
canvases: no grade, pool or shadow can change them. `MinLabelContrast` is therefore the
*non-regression* floor (2.00, the before measurement), and the WCAG AA gap is recorded as a
proposal, not fixed here:

> **Proposal (not done, out of this stage's scope):** the four world-space price boards measure
> **2.4:1** against their own ink (AA needs 4.5:1) and their ink is **12 px tall at 1080p**
> (`CampTheme.LabelFloorPixels` asks 22). Both are properties of the board's own colours and type
> size — work item 5 says report, do not enlarge.

## 4. What was built

- `CampTheme` (`Assets/Main/Content/Camp/CampTheme.asset`) — the arena's theme type with the camp's
  numbers: centre radius 10.2 (the walk rectangle's corner is at 9.14), ring 14, arena 18, 26–40
  grass, 14 leaf/14 flower decals, the three readability floors, and the two profiles.
- `CampYard` — the layout: `ArenaLayout.Build(theme, 20261025)` filtered to (a) centre grass inside
  `GrassRadius` 9.2, (b) standing cards only in the frame (|x| ≥ 9.5, or 5.4 ≤ z ≤ 12, never nearer
  than z = −4), (c) one small origin dirt patch instead of the arena's 11 × 8 one. Plus three fixed
  fixtures (two lamps, one sign), eight light pools, and `Validate`, which is "no tall card in the
  walking rectangle" as a function.
- `CampEnvironmentBuilder` (Editor) — builds the two camp card textures (lamp, sign), the camp
  ground material at queue 2000 (the sala floor is a sprite and would otherwise be drawn *over*
  every decal), and authors `CampEnvironment/{Cards,Decals,CardShadows,Lights,LightPools}` into
  `Camp.unity`. Two menu items: rebuild the yard, capture the evidence.
- `CampSorting` — the camp's order ladder, so a card, a shadow, a pool and the furniture keep the
  arena's relative order.
- `CampEnvironmentCaptureTool` (Editor) — the evidence run: 20 frames across two profiles, two
  sizes, six player states, with batches/draws/setPass/ms and the contrast of each saved PNG.

Dusk-Camp profile: tint (1.1, 1.02, 0.84), key 1.55, ambient 0.78, exposure 0.72, pools 0.46,
vignette 0.14. Night is lower in every term that makes it darker and keeps the pools on.

## 5. Not tested / risks

- **Look.** The user decides: the yard is deliberately a light frame (28 cards / 33 decals), and the
  sala's ground is dark placeholder art, so Dusk-Camp reads as a warm brown clearing rather than the
  battle arena's olive field. Judge from `after-dusk-camp-centre-1920x1080.png`.
- WebGL build and Playwright were **not run**: Stage 09o is not a web gate and only `-Suite Full`
  asks for them.
- Lighting is evaluated in Editor Play Mode only; the Editor's Game View resolution differs from a
  real browser's, so the same 1080p numbers in WebGL are not claimed.
- The four price boards' AA gap above is known and unresolved on purpose.

## 6. Commands actually run

```
tools/test.ps1 -Suite Fast                     # 13 PASS / 0 FAIL / 3 NOT RUN (151 s)
unity command run_tests --mode playmode --async_tests --filter CampWorldPlayModeTests   # 9/9
unity command run_tests --mode playmode --async_tests --filter SceneFlowPlayModeTests   # 6/6
unity command run_tests --mode editor --filter CampYardTests                            # 16/16
menu: ProjectSurvive/Stage 09o/1. Rebuild the camp yard in the open Camp scene
menu: ProjectSurvive/Stage 09o/2. Capture the camp yard (both profiles, both sizes)
```
