# ADR 0037 — Spine actors as Presentation, and how the player figure is loaded, drawn and clocked

- **Status:** Accepted (Stage 17a)
- **Date:** 2026-10-26
- **Related:** [ADR 0008](0008-thai-text-ui-stack.md) (the UI stack the SkeletonGraphic path sits on),
  [ADR 0011](0011-webgl-payload-budget-and-size-measurement.md) (everything under `Resources/` ships,
  and a size is quoted only when measured), [`llmwiki/systems/battle-presentation.md`](../../llmwiki/systems/battle-presentation.md),
  [`llmwiki/systems/unity-shell.md`](../../llmwiki/systems/unity-shell.md),
  [`docs/stages/17a-report.md`](../stages/17a-report.md) (the survey this record is built on).

## Context

Stage 17a takes a Spine actor module that was written for another project (namespace
`MyClone.SpineSystem`) and makes it part of this architecture, then loads the Mor's skeleton
(`Resources/body_1110901/body_1110901_SkeletonData`) as the **player figure**. No scene changes in
this stage (17b–17d attach the figure to Battle, Camp and Title), so every decision here is one that
those three stages will live with.

What the survey measured first (details and links in the report):

- the installed runtime is **spine-unity 3.8.0** (`Assets/Spine/package.json`), the skeleton was
  exported by **Spine 3.8.75** (`SkeletonData.Version`, read from the asset) — same major.minor, so
  no version conflict;
- the export holds **23 animations** (`idle1`…`idle5`, `run`, `walk`, `stand`, `hit`, `dead`,
  `attack1`…`attack8`, `jump1`…`jump3`), **8 skins** in three part groups (`body_1110901_1..3`,
  `head_1110901_1..3`, `face_1110901_1`) plus `default`, one atlas page, and **no** name the project
  could have guessed;
- the atlas material `body_1110901_Material` uses **`Spine/SkeletonGraphic`** — a UI shader;
- rendering the built actor in the Editor through each shader path (URP 17.3, `6000.3.9f1`) showed
  the figure with `Spine/Skeleton` and **nothing at all** with the atlas' own material on a
  `MeshRenderer`.

## Decision

### 1. The actor module is Presentation, in the game's own namespace

`SpineActorBase`, `SpineWorldActor`, `SpineUiActor`, `SpineSkeletonGraphic` and `SpineActorFactory`
live in `Assets/Main/Scripts/Presentation/SpineActors/`, namespace
`ProjectSurvive.Game.Presentation.SpineActors`, compiled by the existing
`ProjectSurvive.Game.Presentation` asmdef (which gained one reference, `spine-unity`). The separate
`MyClone.SpineSystem` asmdef is gone.

The namespace deliberately does **not** end in `.Spine`: inside `ProjectSurvive.Game.Presentation.Spine`
an unqualified `Spine.Skeleton` would resolve to the project's own namespace instead of the runtime's —
the same trap `...Game.Result` hit in
[`llmwiki/systems/loop-and-scopes.md`](../../llmwiki/systems/loop-and-scopes.md) §5. `SpineActors`
cannot collide with `Spine`, and the actor inspector moved into `ProjectSurvive.Game.Editor` with it
(the old `Assets/Editor` script compiled into `Assembly-CSharp-Editor`, which cannot see an
`autoReferenced: false` asmdef).

The module decides no rule: it draws a skeleton, plays the animation it is told to play, and owns no
clock. That is why it sits in Presentation and why nothing in it reads game state.

### 2. The player skeleton is loaded from `Resources` by path, through one loader

`SpineAssetPaths.PlayerSkeletonData` is the resource path
(`body_1110901/body_1110901_SkeletonData`, no extension) and
`SpineSkeletonLoader.TryLoad(path, out asset, out code)` is the only way it is read. The three
refusals are `spine.skeleton_path_empty`, `spine.skeleton_missing` and `spine.skeleton_invalid`
(the last one is `GetSkeletonData(...) == null`, i.e. the asset exists but cannot produce a skeleton —
a broken atlas passes "the file is there" and fails this).

Alternatives weighed:

| Option | Why not |
| --- | --- |
| A serialized `SkeletonDataAsset` on the view in each scene | Three scenes (Battle, Camp, Title) would each carry the reference, and 17b–17d would have to remember to wire it in all three. It also makes "the player figure" a scene-local notion, which it is not. |
| Addressables | Not installed, not in the stack, and one small skeleton does not justify a new dependency (AGENTS 12). |
| Loading in an `async` task at boot | The actor builds synchronously in `Begin()`; an await would buy nothing for a 320 KB asset and would add a lifetime to cancel. |

Cost, stated plainly: **everything in `Resources/` ships** (ADR 0011). This stage did **not** run a
WebGL build, so it makes **no claim** about the player's size with this asset; a Release-candidate
stage must measure it with `tools/build-web.ps1 -ReleaseCandidate` + `tools/measure-web-size.mjs`
before quoting a number.

### 3. Materials and shaders are serialized references — never `Shader.Find` at runtime

`Shader.Find` returns `null` in a player build whose shader was stripped, so a lookup turns into the
"Editor green, player broken" bug of
[`llmwiki/gotchas/il2cpp-di-and-reflection.md`](../../llmwiki/gotchas/il2cpp-di-and-reflection.md).
The runtime therefore has:

- `SpineUiActor.SetMaterials(Material normal, Material gray)` — both serialized; a missing one is a
  `Debug.LogError` and a failed build of the renderer, not a fallback to `UI/Default`;
- `SpineWorldActor.SetGrayShader(Shader)` — a serialized reference, loud error when absent;
- `SpineWorldActor.SetMaterial(Material)` — see §4.

`Shader.Find` survives in exactly one place: the Editor tools that *create* those assets
(`ProjectSurvive/Spine/2. Create player profile`, `3. Create UI materials`). `grep -rn
"Shader\.Find[[:space:]]*(" UnityProject/Assets/Main/Scripts/Presentation` returns **0**.

### 4. The player figure needs its own world material, because the export's is a UI shader

`SpinePlayerProfile` names a `worldMaterial`, the profile tool creates it (`Spine/Skeleton` over the
single atlas page's texture, `Assets/Main/Spine/Materials/SpinePlayer-World.mat`), and
`SpinePlayerView.Begin()` fails with `spine.profile_material_missing` when it is absent. A
multi-page atlas is refused (`spine.player_atlas_multi_page`) rather than drawn with one page's
texture.

This is the one place this stage goes past the letter of the brief, and the reason is a measurement,
not a preference: the export's `body_1110901_Material` has shader `Spine/SkeletonGraphic`, and in the
Editor (URP 17.3) a `MeshRenderer` with that material draws **nothing** while the same mesh with
`Spine/Skeleton` draws the figure (`artifacts/spine/player-view.png` vs the empty
`artifacts/spine/world-atlas-material.png`). Without the world material the stage's whole deliverable
— the player's skeleton loaded and on screen — would be a blank renderer that every test still passes.

The override goes through spine's `CustomMaterialOverride` map, **not** a direct
`meshRenderer.sharedMaterials` write: the renderer rewrites its material array from the atlas on the
next mesh update, so a direct write survives exactly one frame (measured — the figure disappeared
again on the next animated tick).

### 5. Spine does not own a clock; the caller does

`SpinePlayerView` drives the animation state with `Tick(deltaTime)` and nothing else: the
`SkeletonAnimation` component's own `timeScale` is pinned to `0` in `Begin()`, so Unity's frame time
(and therefore `Time.timeScale`) cannot advance a paused game behind the caller's back. Pause and
speed go through `Pause()` / `SetSpeed()`, i.e. the actor's own `AnimationState.TimeScale`.

Two alternatives were real:

| Option | Why not |
| --- | --- |
| Let `SkeletonAnimation.Update()` consume `Time.deltaTime` | It couples the figure to `Time.timeScale`, and it double-advances with the fixed 30 Hz battle tick the scene will step in 17b. |
| Set `Time.timeScale` in the scene to pause | A global side effect that would stop the battle tick and the UI too; the runner owns pausing (Stage 13c-2b). |

A consequence the caller must respect, and the reason `SpinePlayerView` keeps the value: `Play(...)`
resets `AnimationState.TimeScale` to the speed handed in, so the view passes the **current** speed
(0 while paused) on every play.

### 6. `PlayerPose` and `PlayerPoseSelector` are pure

`Idle`/`Run`/`Dead`, chosen by `PlayerPoseSelector.Select(speed, current, dead, threshold)` with
hysteresis (enter at the threshold, leave below **half** of it) and `Facing(dx, current, deadZone)`
which keeps its direction inside the dead zone. No UnityEngine, no Spine types, no scene: a rule about
when a figure walks is testable without an Editor and cannot quietly become gameplay.

### 7. One figure draws the player, and the bridge yields to it (Stage 17b)

The player is still one `RenderEntityKind.Player` entity in `IBattleFrame`; the figure only decides
**who draws it**. `BattleRenderBridge.PlayerDrawnElsewhere` is set from whether the figure actually
built, and while it is true the bridge skips acquiring a pooled view for that kind. A floor therefore
cannot show two doctors, and a figure that fails leaves the sprite that was always there.

| Option | Why not |
| --- | --- |
| Draw the figure *and* the pooled player sprite, hiding the latter | The sprite is still acquired, animated and sorted per frame, and a z or sorting bug makes the doctor a pair. |
| Remove `Player` from the render stream | Presentation would stop receiving the player's own position and speed from the frame, and the simulation's contract would change for a cosmetic reason. |
| Drive the figure from its own `Update()` next to the sprite | Two clocks: the pose would lead or lag the interpolated sprites by a frame, and nothing could pause it (see §5). |

**What a failed build does is asymmetric on purpose.** In the Editor and a development build the
failure is an error with the view's `noun.problem` code, because that is where it can be fixed; in a
release build the pooled placeholder sprite draws the doctor and one warning says so, because losing
the player is worse than drawing a stand-in. A silent fallback is the outcome this rule forbids:
`__projectSurviveBattle` publishes `spineReady` / `spineErrorCode` so the browser suite can tell the
two apart, and the Editor capture fails its own run if the figure will not build.

## Consequences

- **17b–17d wire one asset**: a `SpinePlayerView` with the profile assigned, and the figure has its
  skeleton, animations, skins, height, facing and material. Nothing else needs a lookup.
- **The composed look carries no weapon.** Composing `body` + `face` + `head` leaves the four weapon
  slots (`w_dao`, `w_zhang`, `w_qiang`, `w_jian`) empty — `SpineActorBase` says so once per build
  (`Combined skin leaves 4 empty slot(s)`) — so the Mor stands unarmed. Attaching a weapon is art work
  for a later stage, not a defect of this one.
- **One figure, one material.** A second player skin with more atlas pages needs a material per page
  and a loader that hands out the set; the current refusal is deliberately loud instead of silent.
- **The animation names are values, not code.** `idle1`, `run`, `hit`, `dead` live in the profile asset,
  which was filled from the skeleton by the tool; a re-export with new names fails
  `SpinePlayerProfile.TryValidate` with the skeleton's full animation list in the detail line.
- **Licensing is a blocker outside the code** — see [`ATTRIBUTION.md`](../../ATTRIBUTION.md): the Spine
  Runtimes License is in the repository, but the provenance of `body_1110901` is unknown, so it must
  not enter a release build until that is settled.

## Alternatives considered and rejected

- **Keeping the module's own asmdef** (`MyClone.SpineSystem` → renamed). Rejected: it would put a
  second presentation assembly beside `ProjectSurvive.Game.Presentation` and force every consumer
  (and the architecture test that forbids the simulation) to know about both.
- **`Shader.Find` with a fallback chain** (`Spine/Skeleton` → `Spine/SkeletonGraphic` → `UI/Default`).
  Rejected: the fallback is what makes the bug silent, and the failing case is a stripped player build
  where the log is the only witness.
- **Scaling the skeleton transform** to reach the profile's height. Rejected: `ApplyFlip` writes
  `localScale` there, so the flip would erase the scale. The scale goes on the actor's root, and the
  flip scale is asserted to stay unit in `SpinePlayerTests`.
- **Driving the figure from `Update()`** in the view instead of an explicit `Tick`. Rejected: a view
  that advances itself cannot be paused by its owner, and the PlayMode/EditMode tests could not step it
  deterministically.
