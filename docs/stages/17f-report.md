# Stage 17f — a second player backend (Unity 2D Animation) beside Spine

Status: **split into three sub-stages, agreed with the user**, and **17f-1 is done**. Step 0 found that
the stage is larger than the project's own limit for one stage (AGENTS: stop and propose a split past
~7 items) and that two of the packs it names as candidates are not in the repository; the user chose
the Student 1 mapping (hurt = `hit light`, death empty) and the split below.

## 1. What exists today (read from the repo, not from the reference document)

**The seam the callers actually use.** `SpinePlayerView` (Presentation/SpineActors, 664 lines) is the
only player figure in both scenes. Two callers drive it, plus one Editor capture helper:

| Caller | Members it touches |
| --- | --- |
| `CampPlayerView.PresentFigure` (every frame) | `IsBegun`, `Pose`, `FaceDirection`, `Present`, `Tick`, `Profile.RunSpeedThreshold` |
| `PlayerFigurePresenter` (Battle, every tick) | `Begin`, `IsBegun`, `ErrorCode`, `SortingOrder`, `SetSpeed`, `SetSortingOrder`, `SetPaused`, `Present`, `PlayHurt`, `Tick`, `CurrentAnimation`, `Pose`, `FaceDirection`, `IsHurt`, `PlayCalls`, `Profile.RunSpeedThreshold`, `transform` (position/rotation) |
| `CampWorldView.BuildPlayerFigure` | `Configure(SpinePlayerProfile)`, `SetSortingOrder`, `Begin`, `IsBegun`, `ErrorCode` |
| `BattleCaptureState.TryBeginPlayerFigure` (Editor) | `Begin`, `ErrorCode` |
| `UiCaptureTool` (Editor) | `Object.FindFirstObjectByType<SpinePlayerView>` |

One thing the interface in the prompt does not list but the callers need: **`RunSpeedThreshold`**
(both `CampPlayerView` and `PlayerFigurePresenter` read it through `Profile` to pick Idle vs Run).
The seam therefore needs that number as its own property, not as a `SpinePlayerProfile`.

**Behaviour of `SpinePlayerView` the second backend must match** (read from the code):
no clock of its own — the Spine component runs at `timeScale = 0` and `Tick(dt)` is the only thing
that advances the animation; `Present` writes nothing when pose and facing are unchanged; `Begin` is
idempotent, `BuildCount` counts successes, a failed `Begin` releases what it built and can be retried;
hurt is one-shot and restores the presented pose (restore happens in `Tick` *and* `LateUpdate`, via a
flag set in the completion callback); `Dead` is non-looping; `CurrentAnimation` is the last animation
asked for; `AnimationTime` stops while paused; the root is scaled once to `TargetHeightWorld` with the
parent's lossy scale compensated, the flip lives on the actor's own transform
(`flip = artFacesRight ? facing < 0 : facing > 0`).

**Profiles from 17e** (both `artFacesRight: 0`, `targetHeightWorld: 1.6`, `runSpeedThreshold: 0.1`):
`SpinePlayerProfile_Camp.asset` → idle `stand`, run `run`, hurt `hit`, dead `dead`;
`SpinePlayerProfile_Battle.asset` → idle `idle5`, the other three the same.

## 2. Package / asset availability (the table step 0 asks for)

| Thing | Present? | Where |
| --- | --- | --- |
| `com.unity.2d.animation` (SpriteSkin, SpriteLibrary) | **yes**, 13.0.6 (registry) | pulled in by `com.unity.feature.2d: 2.0.2` in `UnityProject/Packages/manifest.json`; resolved in `packages-lock.json` |
| `com.unity.2d.psdimporter` (PSB → rig) | **yes**, 12.0.2 (registry) | same |
| Render pipeline | **URP** | `com.unity.render-pipelines.universal: 17.3.0`, project GraphicsSettings |
| `Generic Cute 2D` pack — `001 Student 1 Character.prefab` | **yes** | `Assets/Generic Cute 2D/Student/001 Student 1/Prefabs/` |
| `Basic.controller` (24 states, **no transitions**, default state `idle`) | **yes** | `.../Demo/AnimationController/Basic.controller` |
| Bone rig in the PSB (bones + `spriteBone` weights → SpriteSkin) | **yes** | `.../PSB/001 Student 1 Skeletal.psb` (1.87 MB authoring file; texture import `maxTextureSize: 2048`) |
| Clips: `idle` (loop), `run` (loop), `hit light`/`hit heavy`/`knockback`/`knockdown` (non-loop) | **yes** | `.../Animations/Basic/` — curve paths are real bones (`Skeletal/bone_1/...`) |
| `WizardChibi` prefab / `CollegeStudentChibi` prefab | **NO** | not anywhere under `UnityProject/Assets` |
| `Wizard Demo.controller` / `AnimationDemo.controller` | **NO** | not anywhere under `UnityProject/Assets` |

### Blocker A — the packs the prompt names as the candidates are not here
The stage says to choose among prefabs that have `Idle/Run/Hurt/Die` and warns that *Student 1 has no
hurt/die*. Only Student 1 is in the repository: the Wizard and College Student packs (and their demo
controllers) are absent. So there is no candidate with a state literally named `hurt` or `die`, and
the stage's own rule — "if a package or asset is not in this project, stop at this step and report the
blocker; do not copy assets from another project or add packages silently" — applies.

What Student 1 *does* offer, with explicit names (no keyword guessing): `idle`, `run`, `hit light`,
`hit heavy`, `knockback`, `knockdown`, … and nothing that means "dead". Two honest readings, and the
caller has to pick: (a) hurt = `hit light`, death = **absent** (the profile's death field stays empty,
and `Dead` keeps drawing the previous pose — exactly what `SpinePlayerView` already does when a profile
leaves an animation empty, so parity still holds); or (b) death = `knockdown`, which is a decision about
what the art means, not a fact in the data.

### Blocker B — the stage is past the project's size limit
AGENTS: a stage with more than ~7 items, or one that touches Core, Unity scenes, UI and Web tests at
once, must stop so a split can be agreed first. This one touches Presentation code (seam + new
backend + refactor of both callers), the Editor tools (profile + scene builders), both scenes through
their builders, EditMode and PlayMode tests, Editor captures in two backends, a per-frame cost
measurement, **two** ReleaseCandidate builds with payload comparison, and `ATTRIBUTION.md`.

Proposed split, each closing with its own report and commit:

1. **17f-1 — the seam, with Spine behind it.** Add `IPlayerFigure` (in the folder that holds
   `PlayerPose`, plus `RunSpeedThreshold`), have `SpinePlayerView` implement it, point the four callers
   at the interface via `GetComponent`, keep every 17e test and capture green. No new art, no scene
   change of behaviour. This is the risky refactor on its own, provable by regression alone.
2. **17f-2 — the GenericCute backend.** `GenericCutePlayerProfile` + `GenericCutePlayerView` + its own
   asmdef, the Editor menu that writes the profile and builds a scene with the chosen backend, the
   backend enum in the serialized settings, and EditMode tests for the profile/state validation
   (`TryValidate` against the real controller: names, loop flags, no exit transition on death).
3. **17f-3 — parity, evidence and numbers.** The PlayMode parity suite over Camp × Battle × two
   backends, the 1920×1080 captures in both backends, the per-frame cost comparison, the two payload
   builds, and the attribution row.

## 3. Decisions taken with the user

| Question | Answer |
| --- | --- |
| Which prefab and state mapping | **Student 1**: hurt = `hit light`; **death left empty**, so `Dead` keeps drawing the previous pose — the same documented option `SpinePlayerView` already has, which keeps parity exact |
| How the stage runs | **Split**: 17f-1 (the seam), 17f-2 (the GenericCute backend), 17f-3 (parity, captures, cost, payload) |

## 4. 17f-1 — the seam, with Spine behind it

**What changed.** One new file, `Presentation/SpineActors/IPlayerFigure.cs`, and the four call sites
pointed at it:

- `IPlayerFigure` carries Begin/Present/Tick/PlayHurt/SetPaused/SetSpeed/SetSortingOrder plus the
diagnostics the battle report prints (IsBegun, ErrorCode, Pose, FaceDirection, IsHurt, PlayCalls,
BuildCount, CurrentAnimation, AnimationTime, DrawnHeightWorld, CurrentTimeScale), `SortingOrder`, and
`RunSpeedThreshold`. Spine types (`SpinePlayerProfile`, `SpineWorldActor`, `Configure`) are not on it.
It sits in the folder and namespace that already hold `PlayerPose` — the one type both backends must
agree on — so the Spine assembly needs no 2D Animation reference and a future backend assembly needs
no Spine reference.
- `RunSpeedThreshold` is the one member the prompt's list did not name: both callers read it through
`Profile` to pick Idle vs Run, so it is now a number on the seam and the pose rule stops touching a
backend's asset type. `SpinePlayerView` answers it from its profile, with 0.1 as the same fallback both
callers used before.
- Unity cannot serialize an interface, so `BattleViewSettings.PlayerFigure` is now a `MonoBehaviour`
and `PlayerFigurePresenter.Attach` resolves the seam with `GetComponent<IPlayerFigure>()`, throwing on a
component that has no seam (a wiring error, not a silent no-op). The presenter keeps `View` (the
component, for the scene-wiring assertions) and gains `Figure` (the seam).
- `CampWorldView` keeps the component it adds and hands out the seam as `PlayerFigure`;
`CampPlayerView` presents and ticks through it; `BattleCaptureState.TryBeginPlayerFigure` takes a
`MonoBehaviour` like the runtime entry point does.

**Why this is not abstraction for its own sake (rule 12).** The user's own requirement is two backends
behind one call contract, chosen from serialized settings — that is the case the rule allows. Nothing
else was abstracted: no factory, no registry, no third backend, no runtime switching.

**Behaviour is unchanged, deliberately.** `SpinePlayerView` gained only `RunSpeedThreshold`; no error
code, asset, GUID or expected value in a 17e test was touched.

**Verified.**

| Check | Result |
| --- | --- |
| `tools/test.ps1 -Suite Fast` | **PASS 13 / FAIL 0** (EditMode **283/283**, xUnit 612 passed, data + wiki steps green) |
| `CampWorldPlayModeTests` | **12/12** — the new case asserts the scene's figure resolves the seam and that `CampWorldView.PlayerFigure` is that same seam |
| `BattlePlayModeTests` | **11/11** — the wiring case now also asserts `PlayerFigurePresenter.Figure` is the seam of the scene's component |

One environment note, the same failure mode as 17e: the first PlayMode attempt aborted with
`SaveCurrentModifiedScenesIfUserWantsTo … This cannot be used during play mode`, caused by Input System
state-format corruption in that Editor session (`State format KEYS … does not match … MOUS`). The Editor
was restarted and both fixtures then passed; this is an Editor-session defect, not a test result.

**Not done in 17f-1 (and deliberately).** No GenericCute asset, profile, asmdef, Editor menu or scene
change; the camp still adds `SpinePlayerView` directly, and the fallback log string is still
`spine.player_unknown` — both move to the backend selection in 17f-2.

## 5. 17f-2 — the GenericCute backend

**What changed.**

- **The seam moved to its own assembly.** `PlayerPose`, `IPlayerFigure` and the new
`PlayerFigureBackend` enum now live in `Presentation/Figure`
(`ProjectSurvive.Game.Presentation.Figure`). Presentation, Editor and both test assemblies reference
it. This is what makes the prompt's two asmdef rules true at the same time: the Spine code is in
`ProjectSurvive.Game.Presentation` and does not reference 2D Animation, and the new
`ProjectSurvive.Game.Presentation.GenericCute` references only `…Figure`, so it cannot see Spine.
Namespaces were renamed to `ProjectSurvive.Game.Presentation.Figure` (the only thing 17f-1 left in
`SpineActors` that was not Spine).
- **`GenericCutePlayerProfile`** (`Presentation/GenericCute`) — prefab, controller, exact state names
(`idle`, `run`, `hurt`, `death`), `TargetHeightWorld`, `RunSpeedThreshold`, `ArtFacesRight`. Death and
hurt are documented optional; nothing is invented for a rig that lacks them.
- **`GenericCutePlayerView`** — the second `IPlayerFigure`: instantiates the rig under a uniformly
scaled root and a flip child, **disables the Animator** and steps it only from `Tick`, plays states by
hash with `Animator.Play(hash, 0, 0)` (no parameters, no transitions), refuses a name the controller
does not have, scales once to the profile height with the parent's lossy scale compensated, mirrors on
the flip child alone, and puts the whole figure behind one `SortingGroup`. Hurt ends when its own
normalised time reaches 1 and the presented pose is restored; pause and speed never touch
`Time.timeScale`.
- **One detail worth recording:** a queued `Animator.Play` only applies on the next evaluation, and
that evaluation resets the state's clock. `PlayState` therefore does one `Update(0f)` right after
`Play` (state changes only), so the caller's next `Tick` advances the state it asked for — without it
a 0.2 s tick after a 0.167 s hurt clip left the figure still hurt.
- **Backend selection is Editor-baked.** `PlayerFigureBuildSettings` (Editor assembly) names the
backend and the four profiles; `CampSceneBuilder` and `BattleSceneBuilder` read it and write both the
component and its profile into the scenes. The camp carries `_figureBackend` +
`_spineProfile`/`_genericCuteProfile`; the arena carries whichever component in
`BattleViewSettings.PlayerFigure`. Nothing is chosen at runtime, and a missing profile for the chosen
backend is an error — the other backend never draws in its place.
- **The tool** (`ProjectSurvive/Player Figure/`): `1. Create GenericCute profiles` writes the two
profiles from the pack's prefab and controller (fills only empty fields, logs each change, refuses to
write an invalid one); `2. Use Spine backend` / `3. Use GenericCute backend` write the settings and
rebuild both scenes.
- **`GenericCuteProfileValidator`** (Editor) checks the profile against the real
`AnimatorController`: every named state exists (failure lists all states), `idle`/`run` clips loop,
`hurt`/`death` clips do not, and a death state may have no transition out. A hurt state with
transitions is a warning, because the view decides when the pose returns.

**Facing, measured not guessed.** The un-mirrored idle renders the student facing the viewer's
**right**, so `artFacesRight = true` (`artifacts/stage17f/probe-idle-facing-right.png`); facing −1 is
the mirror with the face still visible and the hair trailing the other way
(`artifacts/stage17f/probe-idle-facing-left.png`). The mirror is the flip child's `localScale.x` only;
the scaled root is untouched, which the facing test asserts.

**Verified.**

| Check | Result |
| --- | --- |
| `tools/test.ps1 -Suite Fast` | **PASS 13 / FAIL 0** — EditMode **295/295** (12 new), xUnit 612 |
| `GenericCuteProfileTests` (5) | shipped profiles validate against the pack's controller; a wrong name fails with the state list; a non-looping run state fails; a looping-required state that cannot loop fails |
| `GenericCutePlayerViewTests` (7) | one build and one idle play; 10 000 identical presents add 0 plays; flip sign follows `artFacesRight` with the root scale unchanged; hurt returns to the presented pose (run, not idle); pause freezes `AnimationTime` and speed 2 turns 0.25 s into 0.5 s; draw order reaches the group; a controller-less profile fails with `generic_cute.player_build_failed`, leaves nothing built and can be begun again |
| `CampWorldPlayModeTests` | **12/12** |
| `BattlePlayModeTests` | **11/11** |

**Known limits of this sub-stage.** No scene has been rebuilt with the GenericCute backend yet, so no
in-scene picture of it exists — that is 17f-3 (captures, parity, per-frame cost, two payload builds,
attribution). The camp scene still carries only the Spine profile reference plus the default
`Spine` enum value, which is why the PlayMode suites are unchanged. Budget: no perf or payload number
is claimed here.

## 6. 17f-3 (started) — the switch, and the panel's bust follows it

The user asked to run the GenericCute backend and, in the same breath, for the battle HUD's portrait
to be the pack's `001 Student 1 Portrait.png` instead of the Spine halfbody.

**What changed.**

- **The switch edits the scenes instead of rebuilding them.** `2. Use Spine backend` /
`3. Use GenericCute backend` now open `Camp.unity` and `Battle.unity`, write the backend, the profiles
and the panel's portrait, and save. A rebuild was measured to drop the arena's environment, lighting
and capture props (~38k lines of `Battle.unity`), so the switch cannot go through the builders. The
arena swaps the `IPlayerFigure` component on `PlayerFigure` (old one destroyed first, so the object
never carries two figures) and repoints `BattleViewSettings.PlayerFigure`; the camp goes through
`CampWorldView.ConfigurePlayerFigure`.
- **`BattleHudPortrait` is backend-aware.** New serialized `backend` + `spritePortrait`; with
`GenericCute` it builds one `Image` of the sprite in the same 80 × 110 box (`preserveAspect`, so the
512 × 512 art letterboxes to 80 × 80), builds it once, plays nothing, and fails as
`generic_cute.portrait_sprite_missing` when the sprite is unassigned instead of drawing the Spine
bust beside a rigged figure.
- **A destroyed asset is not a null reference, and it bites the switch.** `OpenScene` unloads assets
nothing else references, so the `PlayerFigureBuildSettings` instance the switch held came back with
`== null` true while its profile fields read empty — the act of saving the settings asset, then
opening a scene, was enough. Both `ApplyTo…` methods now re-read the settings *after* the scene opens
(`PlayerFigureBuildSettings.Load()`), and the run logs `camp=True battle=True` with the profiles
written.
- **The scene tests were Spine-shaped.** `BattlePlayModeTests` (three cases), `CampWorldPlayModeTests`
(one) and `SpinePlayerTests.The_battle_scene_names_the_arena_profile_for_its_figure` found
`SpinePlayerView` by type and read its profile and skeleton transform; they went red the moment the
arena shipped the rig. New `Assets/Main/Tests/PlayMode/FigureProbe.cs` finds whichever component the
scene carries and returns its own profile's idle/run/hurt state, height, facing rule and mirror child,
so the same assertions hold for both backends. `BattleHudPortraitTests` gained two cases (the sprite
portrait builds one `Image`; a missing sprite fails loudly) and one scene case (the panel's backend is
the project's backend), and its halfbody case now builds the Spine path on a host of its own.

**Verified.**

| Check | Result |
| --- | --- |
| `tools/test.ps1 -Suite Fast` | **PASS 13 / FAIL 0** — EditMode **298/298**, xUnit 612 |
| `BattlePlayModeTests` | **11/11** with GenericCute baked into both scenes |
| `CampWorldPlayModeTests` | **12/12** |
| In-scene picture | `artifacts/stage17f/battle-genericcute-portrait-1920x1080.png` (1920 × 1080, Play mode, GenericCute) and its 3× crop `…-crop.png` |
| Switch | `ProjectSurvive/Player Figure/3` run twice with no error in the console; `Battle` and `Camp` both report the GenericCute profile |

**Known limits.** No WebGL build and no Playwright run (that is the Full suite, and the two halves
need opposite Editor states); no per-frame cost number; the pack's portrait has an opaque dark
background, so the 80 × 80 square reads slightly lighter than the panel — an art call, not a bug, and
the letterboxing leaves ~15 px above and below inside the 110 px column.

## 7. What is still open

17f-3: the parity PlayMode suite over Camp × Battle × both backends, the 1920×1080 captures in both
backends (Spine side still missing), the per-frame cost comparison, the two ReleaseCandidate payload
builds (and the assertion that a Spine build carries no GenericCute asset), plus the attribution row
for the pack.
