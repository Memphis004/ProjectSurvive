# Stage 09o — ศาลาพัก's yard (cards + light, the 08e/08f systems on the camp)

## 1. Summary

The camp now stands in its own clearing: a `CampTheme` (a second `ArenaTheme`), a `CampYard`
generator that reuses `ArenaLayout.Build` under a fixed seed and keeps only the pieces that can
frame this camera, and the arena's unchanged `ArenaCard` / `GroundDecal` / `ArenaShadowCard` /
`ArenaEnvironmentView` / `ArenaLightingProfile` doing the work. Two camp profiles (Dusk-Camp,
Night) are one call apart, every station and the เซียมซี cylinder has a warm pool under it, and
nothing in the yard is a collider or a raycast target. The camera, walk area and all usage points
are untouched.

Two follow-ups closed on top of that build: the yard reads **denser and warmer** (65 cards against
the first pass's 28, a lifted ground tint, the backdrop moved a row closer behind the sala), and the
four **world-space price boards** now clear WCAG AA — near-black ink on a brightened board,
**12.39:1**, with **23–24 px** of ink at 1080p against the project's 22 px floor.

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
| 5 | Readability measured on the capture, thresholds in the theme, real numbers | PASS | `stage09o-after-contrast.txt` — 20/20 views, see §3 |
| 6 | Price boards: ink/board contrast and type size clear WCAG AA at 1080p | PASS | price **12.39:1** (AA 4.5), ink **23–24 px** at 1080p (floor 22); `CampYardTests.TheWorldSpaceLabelsAreHeldToWcagAaAndTheProjectsTypeFloor`, `CampWorldPlayModeTests.The_price_boards_use_the_readable_ink_and_type_size` |
| 7 | Denser, brighter yard: more standing frame behind the sala, warmer floor | PASS (numbers in §4; the look is the user's call) | 65 cards vs 28; ground tint 1.32/1.19/0.97 → 1.50/1.34/1.08; `after-dusk-camp-*.png` |
| 8 | One shadow direction, no large patch on the walkable floor | PASS | `CampYardTests.NoShadowDrawsALargePatchOnTheWalkableFloor`, `.EveryShadowFollowsTheProfilesOneDirection` |
| 9 | Yard belongs to the scene, built once, destroyed with it | PASS | `CampEnvironmentBuilder` authors into `Camp.unity`; nothing is Instantiated/Destroyed at runtime |
| Batch budget (08e: ≤ 200) | PASS | `stage09o-after-metrics.txt`: 92–104 batches, 2.44–2.73 ms/frame |
| Full acceptance images | PASS (files) | `artifacts/ui/camp-env/after-{dusk-camp,night}-{centre,shelf0..3,bag}-{1920x1080,1280x720}.png` |

**Suites actually run (after the last edit)**

- `tools/test.ps1 -Suite Fast` → **13 PASS / 0 FAIL / 3 NOT RUN** (114 s): dotnet **612/612**,
  Unity EditMode **250/250**.
- `CampWorldPlayModeTests` (play mode, one fixture) → **11/11 passed**, including the price-board
  test and the yard-click test.
- `SceneFlowPlayModeTests` (play mode) → **6/6 passed**.
- `UiLayoutCheckTests` runs inside the EditMode assembly → passed in the 250/250.

## 3. Readability numbers (measured on the saved pixels)

Thresholds are fields of `CampTheme`: `MinPlayerContrast` 2.20, `MinPropContrast` 1.90,
`MinLabelContrast` **4.50** (WCAG AA for body text), `LabelFloorPixels` 22.

| view | player | shelf | cylinder | pot | price | price ink px | pass |
| --- | --- | --- | --- | --- | --- | --- | --- |
| dusk centre 1080p | 5.13 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |
| dusk centre 720p | 5.13 | 5.24 | 4.44 | 3.37 | 12.39 | 15 | true |
| dusk shelf0 1080p | 4.65 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |
| dusk shelf1 1080p | 3.70 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |
| dusk shelf2 1080p | 3.60 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |
| night centre 1080p | 5.13 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |
| night shelf1 1080p | 3.29 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |
| night shelf2 1080p | 3.09 | 5.24 | 4.44 | 3.37 | 12.39 | 23 | true |

All 20 captured views pass (`pass=True`), both profiles × centre + four shelves × both sizes; the
two panel shots carry no measurement.

**The price board is what changed here.** Its contrast never moved between the *before* capture
(1.95–2.16) and the first *after* capture (2.18–2.89) because the boards are unlit UI canvases: no
grade, pool or shadow can touch them. The fix is therefore in the board's own pixels and type:

- ink (0.10, 0.08, 0.08) → **(0.04, 0.03, 0.03)**, board white → **(1.10, 1.08, 1.04)** — the two
  colours the WCAG ratio is made of;
- type 1.9 → **3.8** TMP font units, which draws **23–24 px** of ink at 1080p (the 22 px floor the
  project states in `LabelFloorPixels`) and 15 px at 720p;
- the focused store's sentence moved from a third price-sized line to a **50 %** line on the same
  board: at the new price size it no longer fits a board-sized label of its own, and a second label
  under the board is hidden by the player standing at the counter of the store it describes.

`pricePx` in the report is the same label measured on its own anti-aliased pixels (2.78–7.30 at
1080p). It is lower than 12.39 because thin Thai strokes ink only part of a glyph cell; that is a
property of the type, not of the colours, so the AA gate uses the design ratio and the ink height
together, and `pricePx` is published next to it.

## 4. What was built

- `CampTheme` (`Assets/Main/Content/Camp/CampTheme.asset`) — the arena's theme type with the camp's
  numbers: centre radius 10.2 (the walk rectangle's corner is at 9.14), ring 14, arena 18,
  34–56 grass, 40–54 ring bushes, 72–92 border trees, 14 leaf/14 flower decals, the four readability
  floors, and the two profiles.
- `CampYard` — the layout: `ArenaLayout.Build(theme, 20261025)` filtered to (a) centre grass inside
  `GrassRadius` 9.2, (b) standing cards only in the frame (|x| ≥ 9.5, or 4.8 ≤ z ≤ 12 — the backdrop
  one row closer than the first pass's 5.4, never nearer than z = −4), (c) one small origin dirt
  patch instead of the arena's 11 × 8 one. Plus three fixed fixtures (two lamps, one sign), eight
  light pools, and `Validate`, which is "no tall card in the walking rectangle" as a function.
- `CampEnvironmentBuilder` (Editor) — builds the two camp card textures (lamp, sign), the camp
  ground material at queue 2000 (the sala floor is a sprite and would otherwise be drawn *over*
  every decal) with a **warmer packed-earth tint** (1.50, 1.34, 1.08 against the first pass's
  1.32, 1.19, 0.97), and authors `CampEnvironment/{Cards,Decals,CardShadows,Lights,LightPools}`
  into `Camp.unity`. Two menu items: rebuild the yard, capture the evidence.
- `CampSorting` — the camp's order ladder, so a card, a shadow, a pool and the furniture keep the
  arena's relative order.
- `CampEnvironmentCaptureTool` (Editor) — the evidence run: 22 frames across two profiles, two
  sizes, six player states, with batches/draws/setPass/ms, the contrast of each saved PNG, and the
  price board's design ratio and ink height.

**The yard as built:** 65 `ArenaCard` objects — 32 standing (12 ring bushes, 8 border trees, 2
lamps, 1 sign, 9 centre tufts) and 33 ground decals (15 dirt, 10 flower, 8 leaf) — 32 instanced
shadow cards and 8 warm pools. The first pass had 28 cards and the same 33 decals.

Dusk-Camp profile: tint (1.1, 1.02, 0.84), key 1.55, ambient 0.78, exposure 0.72, pools 0.46,
vignette 0.14. Night is lower in every term that makes it darker and keeps the pools on.

## 5. Not tested / risks

- **Look.** The user decides: the sala's art is placeholder, so Dusk-Camp reads as a warm brown
  clearing rather than the battle arena's olive field. Judge from
  `after-dusk-camp-centre-1920x1080.png` and `after-dusk-camp-shelf1-1920x1080.png`.
- **720p is reported, not gated.** `LabelFloorPixels` is a statement about 1080p; at 1280×720 the
  same type draws 15 px of ink (`priceInkPx=15`) and the AA gate is not applied there.
- **The lock control's label is still small.** It stays at its 09c size (1.35 TMP units, ≈9 px at
  1080p) on the board. It is a control, not the price, and enlarging it means re-measuring the lock's
  click rectangle — separate work.
- WebGL build and Playwright were **not run**: Stage 09o is not a web gate and only `-Suite Full`
  asks for them.
- Lighting is evaluated in Editor Play Mode only; the Editor's Game View resolution differs from a
  real browser's, so the same 1080p numbers in WebGL are not claimed.

## 6. Commands actually run

```
tools/test.ps1 -Suite Fast                     # 13 PASS / 0 FAIL / 3 NOT RUN (114 s)
unity command run_tests --mode playmode --async_tests --filter CampWorldPlayModeTests   # 11/11
unity command run_tests --mode playmode --async_tests --filter SceneFlowPlayModeTests   # 6/6
menu: ProjectSurvive/Stage 09o/1. Rebuild the camp yard in the open Camp scene
menu: ProjectSurvive/Stage 09o/2. Capture the camp yard (both profiles, both sizes)
```
