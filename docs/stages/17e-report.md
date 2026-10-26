# Stage 17e — the doctor's Spine art, scene by scene

**Date:** 2026-10-27 · **Scope:** Presentation + Editor tools + tests (no `lib/`, no `data/`, no
simulation, no contract). Scenes were changed through their builders, never by editing YAML.

## 1. Summary

- **Two player profiles, one look.** `SpinePlayerProfile_Camp.asset` stands with `stand`,
  `SpinePlayerProfile_Battle.asset` with `idle5`; `CampSceneBuilder` and `BattleSceneBuilder` each
  assign their own. One tool (`ProjectSurvive/Spine/2. Create player profiles`) writes both, names
  the idle explicitly, and on an existing asset rewrites **only** that field, logging the change.
- **The facing bug was one wrong constant.** The skeleton's art is drawn facing the viewer's **left**
  in `stand`, `idle5` and `run`, while the profile claimed `artFacesRight = true`. Both new profiles
  say `artFacesRight = false`, so nothing changed in `SpinePlayerView.ApplyFacing` (still the single
  place a flip is decided, for both scenes). No per-animation flip field was needed: the three poses
  agree.
- **The HUD's figure is the uploaded halfbody** (`2110101`) drawn by one `SpineUiActor` inside the
  status panel's 80 × 110 box, clipped by a `RectMask2D`, held at **one** still frame
  (`PlayCalls = 1`, time 0, `timeScale = 0`, `CreatedAfterBuild = 0`).
- **The four weapon slots are now declared empty on purpose** (`expectedEmptySlots`), so the build log
  warns only about a slot nobody expected.

## 2. What existed before this stage

| Piece | Where | What it did |
| --- | --- | --- |
| actor module | `Presentation/SpineActors/` | `SpineActorBase` (skins, flip, one track), `SpineUiActor`, `SpineWorldActor`, `SpineActorFactory`, `SpineSkeletonLoader` |
| the figure's data | `Assets/Main/Spine/SpinePlayerProfile.asset` | **one** asset for both scenes: `idle1` / `run` / `hit` / `dead`, 1.6 world units, `artFacesRight = true` |
| Camp wiring | `CampSceneBuilder` → `CampWorldView._spineProfile` → `SpinePlayerView` | pose from the distance the walk covered, facing from the key direction |
| Battle wiring | `BattleSceneBuilder` → `BattleViewSettings.PlayerFigure` → `PlayerFigurePresenter` | pose from the frame's `dx`/speed, facing from the frame's `dx` |
| HUD | `BattleSceneBuilder` built `UiLayoutCheck.Portrait` with a `BattleHudPortrait` | the walker's own skeleton (1110901) as a 72 × 120 bust |

`SpineProfileTool` used to resolve animation names by keyword and refused to overwrite an asset.

## 3. The halfbody, read from the asset (no guessing)

`Resources/Portrait/2110101_SkeletonData` — Spine **3.8.75**, skeleton `scale = 0.01`,
`defaultMix = 0.2`, skeleton size 656 × 554, **1 bone** (`root`), **19 slots**: `accessory_b`,
`hat_b`, `hair_b`, `body`, `face`, `face_acc`, `face_ear`, `ear_acc`, `face_acc1`, `face_acc2`,
`face_eye`, `eye_acc`, `face_eyebrow`, `face_mouth`, `face_nose`, `face_gauze`, `hair_f`, `hat_f`,
`accessory_f`.

- **11 skins**, all `RegionAttachment`s: `body_2110101_4_1/2/3` (`body`), `face_2110101_1` (`face`),
  `face_acc_2110101_7` (`face_acc`), `hair_2110101_1_1/2/3` (`hair_b` + `hair_f`),
  `hat_2110101_5_1/2/3` (`hat_f`).
- **1 animation: `stand`, duration 0** — a still pose, not a loop. The portrait therefore plays it
  once at speed 0; with an empty name it would draw the setup pose instead.
- **Atlas: one page 1252 × 1028 RGBA8888**, Linear/Linear, 14 regions (`2110101_1` 88×114,
  `2110101_1_1..1_3` 147×259 / 147×259 / 88×237, `2110101_2_1..2_3` 446×352/359/366,
  `2110101_4_1..4_3` 525×443/353/354, `2110101_5_1..5_3` 48×85/49×91/49×82, `2110101_7` 8×18).
- **The composed look** (from the tool, one skin per part): `body_2110101_4_1`, `face_2110101_1`,
  `face_acc_2110101_7`, `hair_2110101_1_1`, `hat_2110101_5_1` — regions `2110101_4_1`, `_1`, `_7`,
  `_1_1`, `_5_1` = **284,904 px, 22 % of the page**. The other 71 % (918,923 px) is the parts this
  look does not wear: hair `_1_2`/`_1_3`, hat `_5_2`/`_5_3`, body `_4_2`/`_4_3`, and the three
  `2110101_2_*` regions **no skin references at all**. It is one PNG (987 KB) either way, so the
  unused variants cost atlas space now and nothing in the file count; they are kept on purpose for
  the customisation the parts exist for.
- **Relation to `1110901`: unconfirmed.** The bundle path is
  `character/spinelib/halfbody_dizi/jianxiu/2110101_skeletondata.ab`; character `2110101` appears in
  no project table, and `jianxiu` is the bundle's folder name, not a link. Recorded as unconfirmed.

## 4. The choices, and why

| Choice | Why |
| --- | --- |
| **Two profile assets** rather than an "idle per scene" field on one asset | the scene builder assigns the asset; one reference that means a different pose depending on who reads it is exactly the kind of implicit coupling ADR 0037 §2 refuses. Two assets cost two files and nothing in code |
| **`artFacesRight = false`** on both | the only place a flip is decided is `SpinePlayerView.ApplyFacing`; correcting the constant fixes Camp and Battle at once instead of adding a second sign somewhere else |
| **No per-animation flip field** | `stand`, `idle5` and `run` are all drawn facing the viewer's left (close-ups in §6); a field for a difference that does not exist would be invented complexity |
| **A separate `SpinePortraitProfile`**, not `SpinePlayerProfile` | the bust has no run, hurt, death, world height or world material; reusing the player profile would mean inventing four values the art does not have |
| **A copy of the UI material for the page** (`SpinePortrait-2110101.mat`) | the atlas must carry its own texture; the material is a copy of `SpineGraphic-Normal` (same shader), so the chibi's material is shared by the actor and the page — no second shader, no `Shader.Find` |
| **`RectMask2D` on the box** | the bust is wider than the column; clipping at the panel edge is the cheapest honest way to keep it off the HP/MP rows, and it is the mechanism the HUD already uses |

## 5. Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Camp idle `stand`, Battle idle `idle5`, run/hurt/death unchanged | **PASS** | two assets (§4); `SpinePlayerTests.The_two_profiles_name_their_own_idle_and_the_art_faces_left`; `PlayMode` log line `idle=idle5` |
| 2 | Each scene assigns its own profile | **PASS** | `SpinePlayerTests.The_camp_scene_names_the_profile_its_figure_is_built_from`, `The_battle_scene_names_the_arena_profile_for_its_figure`; `Camp.unity` 1-line change (`_spineProfile` → Camp asset), `Battle.unity` rebuilt through the builder |
| 3 | Tool names the idle explicitly, validates, refuses loudly | **PASS** | `SpineProfileTool` (`CampIdleAnimation`/`BattleIdleAnimation` constants + `RequireAnimation`); `SpinePlayerTests.The_profile_refuses_an_animation_the_skeleton_does_not_have` (the message lists the skeleton's animations) |
| 4 | Existing asset: only the idle field is rewritten, and logged | **PASS** | `SpineProfileTool.WriteProfile` (one field, `SetDirty`, log line `idle 'x' -> 'y'; only the idle field was written`); no other field touched |
| 5 | Facing bug fixed in one place, both scenes | **PASS** | `artFacesRight = false`; `SpinePlayerView.ApplyFacing` untouched; `SpinePlayerTests.Walking_right_mirrors_the_art_and_walking_left_leaves_it_alone` |
| 6 | Each animation checked separately | **PASS** | `artifacts/stage17e/zoom-A-run-right.png`, `zoom-B-run-left.png`, `zoom-C-stand-right.png` — face and trailing sash follow the direction; all three agree, so no per-animation field |
| 7 | Editor capture 1920×1080: Camp run left / run right / stand, Battle idle5 | **PASS** | `artifacts/stage17e/camp-run-left-1920x1080.png`, `camp-run-right-1920x1080.png`, `camp-stand-1920x1080.png`, `battle-idle5-1920x1080.png` (crops `_crop-*` are the same frames at 1.5×) |
| 8 | PlayMode: real input/frames move the sign, stopping keeps the last direction | **PASS** | `CampWorldPlayModeTests.Walking_draws_the_figure_running_and_stopping_stands_it` (12/12), `BattlePlayModeTests.Walking_runs_the_doctor_turning_follows_and_stopping_returns_to_idle` (11/11) |
| 9 | HUD status panel draws the halfbody through `SpineUiActor`, not an Image | **PASS** | `BattleHudPortrait` builds one `SpineUiActor`; `BattlePlayModeTests.The_status_panels_portrait_is_the_still_halfbody_built_once` asserts `SkeletonDataName == "2110101_SkeletonData"`, `BuildCount == 1`, `CreatedAfterBuild == 0`, `PlayCalls == 1`, `AnimationTime` frozen and `Graphic.timeScale == 0` across a second of floor |
| 10 | Import path reused from 1110901, no new pipeline | **PASS** | same `Resources/` layout, same `SkeletonDataAsset`/atlas import; the only new asset is the page material the tool makes |
| 11 | Still frame: setup pose or one frozen animation; `PlayCalls` does not grow | **PASS** | `stand` has duration 0 → played once at speed 0, then `Tick(0)`; PlayMode asserts the same numbers after 30 ticks |
| 12 | Skin composition through the shared combined-skin mechanism, selection in serialized settings | **PASS** | `SpinePortraitProfile.skinParts` + `SetSkinParts`/`SpineActorBase.ApplyCombined`; no skin or slot name in `BattleHudPortrait` |
| 13 | Empty slots declared, not guessed | **PASS** | `expectedEmptySlots` on both player profiles (`w_dao`, `w_zhang`, `w_qiang`, `w_jian`) and on the portrait profile (13 slots); `SpineActorBase` warns only outside the list |
| 14 | Layout at 1920×1080 and 1280×720: bust in its column, no overlap, clipped | **PASS** | `artifacts/ui/ui-report.md` (22:40): 37 regions, **0 overlaps** at both sizes; `Portrait` 80×110 at 1920 / 53.3×73.3 at 1280; `BattleHudPortraitTests.The_portrait_box_clears_the_HP_and_MP_rows_and_stays_inside_the_status_panel`; `artifacts/stage17e/hud-portrait-1920x1080.png`, `hud-portrait-1280x720.png` |
| 15 | `__projectSurviveTextFit` / canvas `additionalShaderChannels` untouched | **PASS** | `BattleHudPortraitTests.The_canvas_gets_the_tint_black_channels_only_when_tint_black_is_on`; the UI report's regions still measure |
| 16 | Texture imported per the project's WebGL policy; max size vs display size | **PASS** | `2110101.png.meta`: `maxTextureSize 2048` (source 1252 × 1028, so nothing is downscaled), no mipmaps, sRGB, WebGL override compressed — the same importer shape as the project's other atlas pages; displayed at most 80 × 110 px, so the page is used at ~1/12 of its linear size, yet **not** reduced (no capture was made to prove a reduction is lossless) |
| 17 | Unused parts reported, not deleted | **PASS** | §3: 71 % of the page, 918,923 px, kept for customisation |
| 18 | Relation 2110101 ↔ 1110901 stated or reported unknown | **PASS (reported unknown)** | §3, `ATTRIBUTION.md` |
| 19 | Licensing recorded | **PASS (as a blocker)** | `ATTRIBUTION.md` row for `2110101` (**UNKNOWN — BLOCKER**), `docs/release/known-issues.md` §1c |
| 20 | Payload impact measured (`-ReleaseCandidate` + `measure-web-size.mjs`) | **PASS** | see §7 |
| 21 | EditMode tests for the portrait profile + layout | **PASS** | `BattleHudPortraitTests` 5 cases (`profile validates against the halfbody and refuses a wrong name`, `box clears the HP/MP rows`, …) |
| 22 | `tools/test.ps1 -Suite Fast` | **PASS** | `artifacts/stage17e/fast-suite.log`: PASS 13, FAIL 0, NOT RUN 3, `dotnet test` 612, EditMode **283/283** |
| 23 | `-Suite Full` at close | **PARTIAL** | the harness cannot do both halves at once (PlayMode needs the open Editor, the WebGL build needs it closed). Each half was run on its own: PlayMode per fixture (§6) and the ReleaseCandidate build + browser gate by hand (§7): `smoke`, `battle`, `release` **6/6 PASS** against the RC player, and `screens` **13/13** |
| 24 | The bust draws in a real browser | **PASS** | `artifacts/stage17e/browser-battle-1920x1080.png` (+ crop `browser-battle-hud-crop.png`, 1280×720 too): the bust in its column, HP/MP clear of it; the battle probe reports `spineReady=true`, `spinePlayCalls=1`, `spineAnimation=idle5` for the arena figure |

## 6. What was seen, not just asserted

- `camp-run-left` / `camp-run-right`: the ponytail and the trailing sash fall **behind** the
  direction of travel. Close-ups (`zoom-A-run-right` = facing +1, `zoom-B-run-left` = facing −1) show
  the face turning the same way: the mirrored figure faces right, the unmirrored one faces left, so
  "art faces left" is measured, not assumed. `zoom-C-stand-right` shows `stand` agreeing.
- `battle-idle5`: same figure, `idle5`, same rule.
- `hud-portrait-1920x1080` / `hud-portrait-1280x720`: the bust sits in its column, its bottom cut by
  the mask above the HP bar, no text or bar under it.
- `browser-battle-1920x1080` (a WebGL ReleaseCandidate frame, WebGL2 in Chrome): the same panel in the
  exported player — the bust is there, clipped, and the HP/MP rows and bars are clear of it.
- One caveat carried over from 17d: `UiCaptureTool` borrows the overlay canvas into camera space, so
  the arena's foreground cards sort above the bottom HUD band in Editor captures. The geometry and
  overlap counts are measured from those shots; the portrait's own look is also visible in them
  because the panel's top-left corner is clear of the cards.

## 7. Payload

`powershell -File tools/build-web.ps1 -ReleaseCandidate -Force` (optimize-size, settings restored):
**RESULT: OK**, `build-info.json` configHash `411BA005…`, 0 C# errors. Then
`node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label stage-17e`:

| Basis | Stage 17e | Stage 15 RC | Delta | Gate / target | Verdict |
| --- | ---: | ---: | ---: | --- | --- |
| uncompressed | 47.28 MiB | 44.65 MiB | +2.63 MiB | reported | — |
| gzip -9 | **17.26 MiB** | 15.52 MiB | +1.74 MiB | gate < 30 MiB | **PASS** |
| Brotli q11 | **12.79 MiB** | 11.29 MiB | +1.50 MiB | target < 15 MiB | **PASS** |

`Web.data` 13.77 MiB raw / 6.82 MiB gzip (was 11.94 / 5.32). The delta is **not** all this stage: it
spans stages 16 → 17e, including the walker's own skeleton (17a). `2110101`'s four files are
992,749 B raw (987 KB PNG + 1.6 KB skel + 1.5 KB atlas + 3 KB `source.json`), and a PNG does not
compress further, so the bust is worth roughly **0.95 MiB gzip** of that growth. The 4.4 MiB headroom
on the Brotli target is still open.

## 8. Not tested, and the risks

- **`-Suite Full` was not run as one command**: it needs the open Editor for PlayMode and a closed one
  for the WebGL build. Run separately: PlayMode per fixture (Camp, Battle) through the live Editor,
  then the ReleaseCandidate build with the Editor closed and the browser gate by hand. The other
  PlayMode fixtures (title, settings, upgrade window, result, persistence) were **not** re-run this
  stage, and the other browser specs (`title`, `camp`, `result`, `persistence`, `audio`, `pause`,
  `release-candidate`) were **NOT RUN** — this stage touched the battle HUD and the two worlds' figure,
  not those screens.
- **Licence (Release blocker).** `2110101` has no established provenance, and its relation to the
  walker's skeleton is unconfirmed; both are recorded rather than guessed.
- **Flake seen once.** The first PlayMode pass of `BattlePlayModeTests` failed 6 cases with
  `ArgumentException: State format KEYS from event does not match state format MOUS of device
  Mouse:/Mouse` — the Input System's native mouse state in that Editor session had been corrupted by
  simulated input, not by this stage's code (the same exception appeared in the Editor console outside
  the tests). The same fixture passed 11/11 on the immediate re-run. Recorded because a green suite
  that hides a flake is not evidence.

## 9. Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
# Unity Editor, one fixture per run:
#   run_tests --mode playmode --filter CampWorldPlayModeTests   -> 12/12
#   run_tests --mode playmode --filter BattlePlayModeTests      -> 11/11
#   run_tests --mode editor  --filter ArenaEnvironmentTests     -> 29/29
# Editor captures:
#   ProjectSurvive/Tools/1. Capture UI            -> artifacts/ui/ui-report.md, 0 overlaps
#   ProjectSurvive/Spine/2. Create player profiles
#   ProjectSurvive/Spine/4. Create portrait profile
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -ReleaseCandidate -Force
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label stage-17e
node tools/compress-web.mjs --dir UnityProject/Builds/Web --label Release --quiet
node tools/serve-web.mjs --root UnityProject/Builds/Web --port 8765
# tests/web, with BASE_URL=http://127.0.0.1:8765 and PROJECTSURVIVE_BUILD=release:
#   npx playwright test --reporter list smoke.spec.ts battle.spec.ts release.spec.ts   -> 6/6
#   SCREENS=1 npx playwright test screens.spec.ts                                    -> 13/13
```
