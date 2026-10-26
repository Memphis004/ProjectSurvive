# Stage 17b — พ่อหมอใน Battle (ต่อเข้า VContainer / frame / events)

**Date:** 2026-10-26

## 1. Summary

The Battle scene draws the ghost-doctor with `SpinePlayerView` instead of the pooled placeholder sprite,
and nothing about the frame changed to allow it: the player is still one `RenderEntityKind.Player`
entity, and `PlayerFigurePresenter` derives **everything** out of that entity's own frame —
`speed = |(current − previous)| × 30 Hz`, the pose from the pure `PlayerPoseSelector`, the facing from the
sign of `dx`, `dead` from `BattlePhase.Lost`, the draw order from `BattleWorldMap.SortingOrder` (written
only when the row changes), and the position from `BattleWorldMap.Interpolate`. The hurt flinch enters
through the same VFX event channel `BattleAudioListener` listens on, pause and 1×/2× go through the
animation state's own `TimeScale` (`Time.timeScale` untouched), and the presenter is a VContainer
singleton built by an explicit factory lambda. `BattleRenderBridge.PlayerDrawnElsewhere` makes the sprite
pool yield the player, so a floor can never show two doctors, and a figure that fails to build leaves the
old sprite in place — loudly in dev, with one warning in release. EditMode is **274/274** (9 new in
`BattlePlayerFigureTests`, 3 for the mid-field band), `BattlePlayModeTests` **10/10**, the Editor capture
draws the doctor on floor 1 at 1920×1080 and 1280×720, `-Suite Full` is **PASS 23 | FAIL 0 | NOT RUN 3**
with the Editor open and **PASS 15 | FAIL 0 | NOT RUN 1** with it closed, and in the built player
Playwright **21/21** with `spineReady:true, spinePlayCalls:1, spineAnimation:"idle1"`.
The Release candidate (High stripping / `optimize-size`) is **16.18 MiB gzip / 11.83 MiB Brotli** —
inside both the ADR 0011 gate (< 30) and target (< 15) — and its own browser run still reports
`spineReady:true`, which is what proves the skeleton and the figure's shader survive stripping.
The follow-up the stage needed after looking at the capture: the white robe had no edge against the
field, so the figure now draws with a **one-screen-pixel ink outline**, in a shader of the
project's own (`ProjectSurvive/SpinePlayerOutline`) because URP renders one pass per material and
spine's two-pass outline shader drew the doctor as dark lines with no body at all. A second
follow-up (§2b) then found that the “pale haze band” which prompted the outline was not the arena at
all — it was the screen-top band photographed at its authored white — and dimmed the one thing that
does sit behind the doctor, his own lantern halo.

## 2. Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `SpinePlayerView` created at the floor's start (warm-up); `CreatedAfterWarmup` stays 0 and `viewsGrownDuringPlay` 0; the actor is not in the pool | **PASS** | `The_battle_scene_builds_the_doctor_figure_once_and_stands_still_at_first` (`BuildCount` 1 after a second of ticks, `Pool.CreatedAfterWarmup` 0), `Ten_seconds_of_ticks_…` (0 after 300 ticks), the browser report `viewsGrownDuringPlay:0` / `viewsCreated:1793` unchanged; `The_sprite_bridge_stops_drawing_the_player_once_the_figure_stands_there` (EditMode) |
| 2 | The player is still the same entity; speed `(Cur − Prev) × TickRate`, facing from `dx`, `Dead` from `Lost`, sorting from `BattleWorldMap.SortingOrder` only on change, billboard pitch the sprites' own | **PASS** | `The_pose_and_the_facing_come_from_the_frames_own_movement`, `The_draw_order_is_the_world_maps_and_is_written_only_when_it_changes`, `A_lost_floor_draws_the_death_pose_and_a_cleared_one_does_not`; `Walking_runs_the_doctor_turning_follows_and_stopping_returns_to_idle` in the real scene (Run → facing ±1 → Idle, 3.56 u/s). Pitch read from `BattleRenderBridge.Orientation` = `Quaternion.Euler(pitch, 0, 0)` and used verbatim, never re-derived |
| 3 | Hurt from `PlayerDamaged` through a listener on the proper channel; events decide no rule | **PASS** | `_events.AddListener(BattleEventChannel.Vfx, _figure)` next to the audio listener; `A_player_damaged_event_makes_the_doctor_flinch_and_return` (PlayMode, real `BattleEvent`), `A_player_damaged_event_plays_the_hit_animation_and_returns_to_the_pose`, `An_event_that_is_not_the_players_damage_changes_nothing` |
| 4 | `SetPaused` → `SetPaused`, `BattleSpeed` → `SetSpeed`, only on change, never `Time.timeScale` | **PASS** | `Pausing_the_runner_holds_the_figures_animation` (steps frozen, `AnimationTime` frozen, resumed, `Time.timeScale` unchanged), `The_battle_speed_is_written_once_per_change_and_scales_the_animation` (`SpeedWrites` 1 per change) |
| 5 | DI through a factory lambda only; no new abstraction | **PASS** | `Builder.Register(resolver => new PlayerFigurePresenter(resolver.Resolve<BattleSpeed>()), Lifetime.Singleton)` in `BattleSceneScope`; no interface was added, `BattleRunnerContext.PlayerFigure` is the concrete type |
| 6 | `Begin()` failure: loud in dev, placeholder sprite in release, reported — never silent | **PASS** | `PlayerFigurePresenter.Begin` returns false and skips the warning in the Editor/dev build (the view logs the `noun.problem` code as an error); a release build logs one warning naming `spineReady=false`; `spineReady` / `spineErrorCode` in the report and in the Playwright assertions; the capture tool **fails** if the figure will not build |
| 7 | Scene built by `ProjectSurvive/Stage 08/3`, no hand-edited YAML; the four `spine*` fields in `__projectSurviveBattle`; a browser test | **PASS** | `Battle.unity` regenerated by `BattleSceneBuilder` (`PlayerFigure` under `ViewRoot`, profile + starting sorting order assigned); `BattleSimulationRunner` writes `spineReady` / `spineErrorCode` / `spinePlayCalls` / `spineAnimation`; `tests/web/battle.spec.ts` asserts `spineReady:true`, empty error code, a non-empty animation and `spinePlayCalls < 40` |
| — | The doctor reads against the arena's bright band (outline, requested after the first capture) | **PASS (image review is the user's)** — see §2b for the measured "bright band" and the halo dimming | `ProjectSurvive/Spine/4. Create player outline material` writes `Assets/Main/Spine/Materials/SpinePlayer-World-Outline.mat` (`ProjectSurvive/SpinePlayerOutline`, width **1.2 screen px**, ink `#0D0A09`, cutoff 0.25) and points the profile at it; spine's own `Spine/Outline/Skeleton` was measured and **rejected** — two passes in one SubShader, and URP renders one, so the figure came out as thin dark lines with no body. A pixel scan across the torso is now `48 → 11 (ink) → 135/203 (robe) → 43 (ink) → 48`, i.e. the ink really sits between the robe and the floor; 141 ink pixels touch robe pixels in the 1920×1080 capture. `The_profile_draws_with_the_outlined_world_material` pins the material (and refuses spine's outline shader by name); the built **Release candidate** renders it too (`tests/web/test-results/web-battle-mid.png`) |
| — | PlayMode: real scene wiring, walk/stop, hurt, pause, ten seconds, counters | **PASS** | `BattlePlayModeTests` **10/10** in `-Suite Full` (44.9 s) |
| — | Editor capture floor 1 at 1920×1080 | **CAPTURED — awaiting the user's look** (the agent does not pass the picture itself) | [`battle-player-1920x1080.png`](../../artifacts/ui/battle-player-1920x1080.png) — the doctor in white robes running right, mid-arena, on floor 1 played by the shipped simulation (pose Run, animation `run`, 3.56 u/s after 8 ticks); 37 regions, **0 overlaps**; 1280×720 twin too. The run's first verdict was **FAIL (4)** for four *Title settings* label/button overlaps (a 4-px box overlap at both sizes) — not this stage's code, but fixed while it was in front of the tool: the two bottom captions now sit 10 px higher (`TitleSceneBuilder`, rebuilt through `ProjectSurvive/Stage 03/3`), and the whole run is **PASS — 0 failures, 0 overlaps** (`TitleScreenLayoutTests` 4/4, `TitleScenePlayModeTests` 5/5) |
| — | `-Suite Full` at close | **PASS** | Editor open, after the 0.10 change: **PASS 23 \| FAIL 0 \| NOT RUN 3** (the three web steps, Editor holds the lock) ([log](../../artifacts/stage17b/full-editor-open-haze.log)). Editor closed, `-Release`: **PASS 15 \| FAIL 0 \| NOT RUN 1** — only Unity EditMode, which needs the Editor ([log](../../artifacts/stage17b/full-rc-close.log)) |
| — | `build -ReleaseCandidate` to prove stripping; payload vs ADR 0035 | **PASS** | `tools/build-web.ps1 -ReleaseCandidate -Force` exit 0, `stripping High / codegen OptimizeSize / exceptions ExplicitlyThrownExceptionsOnly`, configHash `411BA005…`, 48,182,331 B of payload ([log](../../artifacts/stage17b/full-rc-close.log)). Payload **16.18 MiB gzip / 11.83 MiB Brotli** vs the Stage 15 RC's **15.52 / 11.29** → **+0.66 gzip, +0.54 Brotli** (+1.30 MiB uncompressed, 44.65 → 45.95) ([measurement](../../artifacts/stage17b/rc-size.md)); gate `< 30` **PASS**, target `< 15` **PASS**. The RC artifact is what the suite's own browser run serves: after the 0.10 rebuild **21/21** passed in Chrome ([artifacts/web/playwright.json](../../artifacts/web/playwright.json)), and every battle report says `spineReady:true`, `spineErrorCode:""`, `spinePlayCalls:1`, `spineAnimation:"idle1"`; the two-outline-era RC runs are in [playwright-rc.log](../../artifacts/stage17b/playwright-rc.log) |

## 2b. Follow-up: the haze band, measured

Asked to dim the arena's mid-field haze so the figure would not need the outline, the band was
measured instead of adjusted — and it is not the arena.

| Claim | How it was checked | Result |
| --- | --- | --- |
| "The middle of the arena is a pale haze band" | pixel scan of `artifacts/ui/battle-player-1920x1080.png`: a neutral ramp `254 → 10` from the top edge down to exactly 50 % of the frame, with a hard cut, on top of the trees | **the band is the `ArenaScreenGradient` overlay**, not the floor: its `Image` is authored white and only `ArenaEnvironmentView.Apply` (from `Awake`) turns it into the profile's near-black green. An Edit-Mode photograph — and the Game view before Play — draws it at full strength in white |
| The same frame from the running game | play-mode `ScreenCapture` and a camera-only render of `Battle.unity`, plus the shipped WebGL frame | **no band at all**: a uniform olive floor, `(80, 87, 42)`. The ring across the doctor's body measures **7.06 : 1** against it |
| What actually sits behind the doctor | the same two frames, ring at 10–14 px around the figure | his own **lantern halo**, `(181, 160, 84)` — 1.91 × the floor, and it moves with him |

Two changes followed, both small:

1. **The picture stops lying.** `UiCaptureTool` applies the scene's authored profile before it
   photographs (`haze/band-applied-in-editor.png` — the white band is gone), the arena and camp
   builders write the authored band into the scene instead of leaving the `Image` white, and
   `BattleCaptureState` lays the runtime floor quad (`ArenaTheme.GroundMaterial`, 80 × 80) so a battle
   shot has the ground the game draws instead of the camera's clear colour.
2. **The halo is dimmed, and only the halo.** Dusk's `GlowIntensity` 0.85 → **0.45** and
   `LightPoolIntensity` 0.24 → **0.15** (mirrored in `ArenaArtBuilder.BuildDuskProfile`, which owns the
   values, and in `ArenaLightingDusk.asset`, which the game reads). Night keeps 0.70 / 0.42 — a dark
   floor is what a lantern halo is for.

Measured on two play-mode frames of the same floor, ring at 10–14 px around the figure:

| | robe vs floor | robe vs its halo | halo vs floor |
| --- | --- | --- | --- |
| glow 0.85 / pools 0.24 | 7.06 | 3.71 | 1.91 |
| glow 0.45 / pools 0.15 | 7.16 | **4.74** | **1.51** |

Evidence: `artifacts/stage17b/haze/before-glow-0.85.png`, `…/after-glow-0.45.png`,
`…/band-applied-in-editor.png`. `ProjectSurvive/Tools/1. Capture UI` re-run: **PASS, 0 failures,
0 overlaps**, `battle-player-1920x1080.png` regenerated with the floor under the figure.

### 2b-2. The haze itself, as a real camera-space band

Asked again for a mid-field haze rather than a dimmed scene, one now exists and is owned by the
profile: `ArenaLightingProfile.ScreenMidGradient` + `ScreenMidColor`, drawn by a second `Image` on the
same below-the-HUD canvas (`ArenaScreenGradient.ApplyMid`) on its own sprite, `screen-mid-band.png` —
a **bump**, opaque at the vertical middle of the frame and zero above 18 % and below 86 %, so the top
and bottom edges keep the values they had. Dusk **0.10** of `(0.62, 0.63, 0.55)` — 0.16 was the first
value and is kept below as the comparison, because at 0.16 the strip read as a grey veil over the
floor rather than a change of air; night **0.10** of `(0.10, 0.14, 0.22)`; the Camp's own profile
leaves it at 0 (the object exists and is wired, disabled).

**What it costs, measured.** Composited over one 1280 × 720 play-mode frame of the halo series at 0.16
(a different frame and a different region set from the pair table below, hence the few per-cent
difference — both say the same thing):

| | robe | ground under the band | robe-vs-floor |
| --- | --- | --- | --- |
| before | 248, 249, 251 | 79, 86, 41 | **6.98** |
| haze 0.16 | 236, 238, 236 | 97, 102, 68 | **4.94** |

The band is a veil over the figure too, so at 0.16 it *lowered* his local ratio by 29 % — it cannot do
otherwise: any screen-space layer that covers him blends his robe toward the floor as well. If the
goal is salience instead of atmosphere the levers are the strength or the figure's size — not more
haze.

**The strength was then halved, on a side-by-side.** Same frozen frame (`Time.timeScale` 0, one
play-mode floor of the shipped scene), the shipped 0.10 and the former 0.16 captured back to back with
the only difference being `ArenaScreenGradient.ApplyMid`:

| dusk mid haze | robe (brightest px) | floor patch mean | robe-vs-floor |
| --- | --- | --- | --- |
| 0 (`lk-dusk-off`, the same floor) | 246, 248, 250 | 85.2, 88.4, 41.7 | **6.97** |
| **0.10 (shipped)** | 240, 243, 241 | 96.0, 98.8, 61.4 | **5.62** |
| 0.16 (former) | 236, 239, 236 | 101.4, 104.4, 69.9 | **4.97** |

Pixel-diffed, the two frames are **identical above y = 104** and everything else is the bump's own
footprint: from y = 104 to 129 the two frames differ by at most 2/255 — a residue, not an effect — and
the visible change (≥ 3/255) runs from **y = 129 (18 % of the height) to y = 561 (78 %)**, peaking at
**9/255 on open floor** and **13/255 on the figure's ink edge**. The top band above y = 104 and the frame's bottom edge are
pixel-identical. The HUD panels are not, and the reason is worth stating: the band's canvas is
`sortingOrder` **−100** against the HUD's **0**, so the band sits *under* the panels — but the panels
are translucent (alpha 0.88–0.98), so the band shows through them by up to **6/255** (7/255 in the dev
menu). That is the price of a full-screen layer, and it is why the band ends at 86 % instead of covering
the frame.

0.10 costs the doctor 19 % of his robe-to-floor ratio (against 29 % at 0.16) and still leaves the
change of grade the haze was asked for; 5.62 : 1 is 2.6 × the `ArenaTheme` player threshold (2.2).
The ratio is WCAG relative luminance of the robe's brightest pixel (max channel sum inside
`x 615–680 / y 310–375`) against the mean of the 20 × 20 floor patch `x 630–650 / y 360–380` just
below his feet — the same two regions in all three frames, which is what makes the columns
comparable, and `artifacts/stage17b/haze/measure_pair.py` is the script that prints these three rows. Evidence:
`artifacts/stage17b/haze/looks/pair-dusk-0_10.png` vs `…/pair-dusk-0_16.png`, the earlier same-frame
set `…/looks/lk-dusk-{off,010,016}.png` (which measured
5.50 / 4.89 and is what the pair was repeated to confirm), the night value `…/looks/lk-night-010.png`,
and the regenerated capture `artifacts/ui/battle-player-1920x1080.png`.

Guards: `TheMidFieldHazeIsProfileDrivenAndCanBeTurnedOff` (strength drives the alpha, a zero-strength
profile costs no draw call) and `TheShippedSceneCarriesTheMiddleHazeWiredToTheProfile` (the shipped
scene has the image, the sprite, `raycastTarget` off and the dusk strength stored — not the `Image`'s
white default).

## 3. What was not tested, and the risks

- **The art still has no licence** (`ATTRIBUTION.md`, known issues §1b). The RC build below contains
  `Resources/body_1110901`, so it is evidence about stripping, **not** a shippable candidate.
- **No performance claim.** Nothing was profiled: the figure is one actor with one material, but the
  spine renderer's cost per frame in the arena has not been measured, and a Development build's timing
  may not be quoted anyway (ADR 0003).
- **The dev-build warning path is not executed by any test.** A release build's fallback (warning +
  placeholder sprite) is written and reviewed, but the only exercised failure path is the dev one; a
  fixture that forces `Begin` to fail in a player build does not exist.
- **One figure, one skin.** The profile's composed look still carries no weapon, and only the Battle
  scene has a figure — Camp and the Title are 17c/17d.
- **The hurt flinch is cosmetic-only by construction**, so a lost or dropped event is invisible in the
  numbers; only the animation is affected.
- **The mid-field band is a look with a measured price, not a free effect**: at the shipped 0.10 it
  removes 19 % of the doctor's robe-vs-floor ratio (6.97 → 5.62), and the number is a pixel sample of
  one frozen frame, not a player study. 0.10 was chosen on a side-by-side against 0.16 (which measured
  4.97 and was judged a grey veil); no other strength was looked at. The night value (0.10, cool tone)
  has been captured once (`looks/lk-night-010.png`, ratio 10.4 : 1) but not judged by anyone but the
  agent.
- **The halo dimming is one look value, judged on two frames.** The numbers above are WCAG ratios of
the saved pixels, not a player study; the light pools also dimmed, so the arena reads slightly cooler
at dusk than in the Stage 08 art pass, and the same two numbers are the place to turn if the lantern
now reads as too faint.
- **Hand-tuning of the figure's size** (1.6 world units at the arena camera) is an art value read from
  the capture, not a measurement of readability against the 08f thresholds. At 1920×1080 the doctor is
  ~45 px tall against ~22–26 px ghosts, and this stage did **not** change it: the outline was the
  requested fix, and growing the figure is still an open art decision.
- **The outline is one screen pixel and eight texture taps**, measured at 1920×1080 and 1280×720 and in
  the Release candidate. Below roughly 30 px tall the ink covers a larger share of the figure, and a
  pose with intentional semi-transparency (a fade) would fall under the 0.25 cutoff and lose its edge;
  neither case exists in the current art.
- **Nothing measures the outline's cost.** It is one extra pass inside the same material (not a second
  draw), and no frame-time number was taken for it.

## 4. Commands actually run

```powershell
# Unity, through the open Editor's Pipeline connection
unity recompile
unity command run_tests --mode editmode --async_tests --filter "BattlePlayerFigureTests"     # 9/9
unity command run_tests --mode playmode --async_tests --filter "<one PlayMode case at a time>"
unity command menu "ProjectSurvive/Stage 08/3. Build Battle scene"
unity command menu "ProjectSurvive/Tools/1. Capture UI"          # -> artifacts/ui/
unity command menu "ProjectSurvive/Spine/4. Create player outline material"   # the ink
unity command menu "ProjectSurvive/Tools/1. Capture UI"          # again, with the outline on

# the mid-field haze: one frozen play-mode frame, ApplyMid at 0.10 and then at 0.16
#   (timeScale 0 through the Pipeline connection, ScreenCapture per strength)
python artifacts/stage17b/haze/measure_pair.py artifacts/stage17b/haze/looks/*.png   # 6.97 / 5.62 / 4.97

# the stage's gate, after the 0.10 change: two runs, because the Editor and the WebGL build
# cannot both hold the project
unity quit                                                             # Editor closed
pwsh -File tools/test.ps1 -Suite Full -Release   # PASS 15 | FAIL 0 | NOT RUN 1 (build + Playwright 21/21)
# Editor reopened; the Pipeline connection is what runs the Unity steps
pwsh -File tools/test.ps1 -Suite Full            # PASS 23 | FAIL 0 | NOT RUN 3 (EditMode 274/274, PlayMode 82/82)

# the stripping proof and the payload of the rebuilt Release candidate
pwsh -File tools/build-web.ps1 -ReleaseCandidate -Force
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label rc-haze-0.10 \
    --json artifacts/stage17b/rc-size.json --markdown artifacts/stage17b/rc-size.md
```
