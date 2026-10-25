# Stage 17a — Spine actor module + the player skeleton (no scene touched)

**Date:** 2026-10-26

## 1. Summary

The Spine actor module that arrived from another project (`MyClone.SpineSystem`) is now
`Presentation/SpineActors` inside `ProjectSurvive.Game.Presentation`, in the game's namespace, with no
`Shader.Find` left in runtime code, and `SpinePlayerView` loads `Resources/body_1110901` through one
loader with `noun.problem` codes. The survey answered the questions the brief said not to guess: the
runtime is spine-unity **3.8.0** and the skeleton **3.8.75** (same major.minor), the export's 23
animations / 8 skins / 1 atlas page were read by the dump tool, and the one thing nobody could assume —
that a Spine shader draws in URP 17.3 — was measured in the Editor. It does, **but not with the export's
own material**: `body_1110901_Material` uses the UI-only `Spine/SkeletonGraphic` shader and draws
nothing on a `MeshRenderer`, so the profile now names a world material (`Spine/Skeleton` over the single
atlas page) and `Begin()` refuses to run without it. Editing is green: EditMode **262/262** (12 new),
`-Suite Fast` **PASS 13 | FAIL 0 | NOT RUN 3**. The art's licence is an open **blocker**
(`ATTRIBUTION.md`), and no WebGL build was run, so the player's size with this asset is **NOT
MEASURED**.

## 2. Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| A1 | Files found, asmdef situation, installed spine-unity version vs the export's version, blocker if major.minor differ | **PASS** | module was `Assets/Scripts/SpineSystem/*` with its own `MyClone.SpineSystem.asmdef`; runtime `3.8.0` (`Assets/Spine/package.json`), skeleton `3.8.75` (`SkeletonData.Version`) → same major.minor, no blocker. Recorded in [`artifacts/spine/player-skeleton.md`](../../artifacts/spine/player-skeleton.md) |
| A2 | The `.asset` is in `Resources` where claimed; `Resources.Load("body_1110901/body_1110901_SkeletonData")` works | **PASS** | `SpinePlayerTests.The_player_skeleton_loads_and_produces_a_skeleton`; the dump tool loads it by that exact path |
| A3 | Do the Spine shaders work in URP 17.3 — from a real render, not a guess | **PASS (with a finding)** | `Spine/Skeleton` draws the figure ([`player-view.png`](../../artifacts/spine/player-view.png), [`world-skeleton-shader.png`](../../artifacts/spine/world-skeleton-shader.png)); the UI path draws with `Spine/SkeletonGraphic` ([`ui-graphic.png`](../../artifacts/spine/ui-graphic.png)); the export's own material on a `MeshRenderer` draws **nothing** ([`world-atlas-material.png`](../../artifacts/spine/world-atlas-material.png)). Fixed by the world material below |
| A4 | Licences: Spine Runtimes License, and `body_1110901`'s provenance | **FAIL (blocker, documented)** | runtime notice ships (`Assets/Spine/LICENSE.txt`); the team's Spine Editor license is unevidenced; `body_1110901` has **no** provenance → `ATTRIBUTION.md` blocker + [`known-issues.md`](../release/known-issues.md) §1b + a release-checklist item. Works in dev; **must not** enter a release build |
| B1 | Namespace `ProjectSurvive.Game.Presentation.SpineActors`, not ending in `.Spine` | **PASS** | the five module files + the inspector; `grep` for the old namespace returns nothing |
| B2 | Presentation / Editor / Tests.EditMode reference the real runtime asmdef; Presentation still off `Core.Simulation` | **PASS** | `spine-unity` added to the three asmdefs; `BattlePresentationTests.Presentation_may_not_reference_the_simulation_assembly` passes in the 262 |
| B3 | No `Shader.Find` in shipped code; materials/shaders are serialized references; missing ones log an error instead of falling back | **PASS** | `grep -rn "Shader\.Find[[:space:]]*(" UnityProject/Assets/Main/Scripts/Presentation` → **0**; `Shader.Find` survives only in the two material-creating Editor tools; `SpineUiActor.SetMaterials`, `SpineWorldActor.SetGrayShader`/`SetMaterial` error loudly |
| B4 | Source-project comments removed; actors state they are Presentation only | **PASS** | `SpineActorBase` / `SpineUiActor` / `SpineWorldActor` / `SpineSkeletonGraphic` / the inspector reworded; `grep -i "myclone\|the game\|the original"` in the module → 0 |
| B5 | No dependence on `Time.timeScale`; pause/speed through `Pause()`/`SetSpeed()`; `Play` passed the current speed every time | **PASS** | `SpinePlayerView` pins the spine component's `timeScale` to 0 and drives `Tick`; `CurrentSpeed()` is passed to every `Play`; covered by the hurt test's tick loop |
| C1 | `SpineAssetPaths.PlayerSkeletonData` + `SpineSkeletonLoader.TryLoad` with the three codes | **PASS** | `An_empty_path_is_its_own_refusal`, `A_path_with_nothing_behind_it_is_missing_not_invalid`, `The_player_skeleton_loads_and_produces_a_skeleton` |
| C2 | `SpinePlayerProfile` asset with real animation/skin names, `TryValidate` failing loudly | **PASS** | [`SpinePlayerProfile.asset`](../../UnityProject/Assets/Main/Spine/SpinePlayerProfile.asset) (`idle1`/`run`/`hit`/`dead`, body+face+head skins, 1.6 u, 0.1 threshold); `The_created_profile_validates_against_the_real_skeleton`, `…refuses_an_animation…`, `…refuses_a_skin…` |
| C3 | `PlayerPose` + pure `PlayerPoseSelector` with hysteresis and facing dead zone | **PASS** | `Dead_wins_over_a_moving_speed`, `Run_starts_at_the_threshold_and_stays_until_half_of_it`, `Facing_keeps_its_direction_inside_the_dead_zone` |
| C4 | `SpinePlayerView` API; `Present` zero-allocation and one `Play` per pose change; height from the mesh bounds, scale on the actor root; `Begin` fails on empty bounds / missing animation / unrendered skin | **PASS** | `Presenting_the_same_pose_ten_thousand_times_plays_once_and_allocates_nothing` (PlayCalls = 1, `GC.GetAllocatedBytesForCurrentThread` delta = **0** over 10 000 calls), `Begin_builds_one_figure_at_the_profiles_height_and_is_idempotent` (1.6 u drawn, root scale 1.05, flip scale on the skeleton transform stays 1, second `Begin` builds nothing), `The_hurt_animation_returns_to_the_pose_it_interrupted` |
| D1–D3 | Three Editor tools that work on the real asset | **PASS** | ran through `unity command menu`; outputs: [`player-skeleton.md`](../../artifacts/spine/player-skeleton.md), the profile asset, three materials under `Assets/Main/Spine/Materials/`; log in [`tool-log.txt`](../../artifacts/stage17a/tool-log.txt) |
| E | The listed EditMode tests, against the real asset, and the existing architecture tests | **PASS** | `SpinePlayerTests` **12/12** ([status JSON](../../artifacts/stage17a/editmode-spine.json)); whole EditMode **262/262** in the Fast run |
| F | ADR 0037, dependencies row, ATTRIBUTION, stage log | **PASS** | [ADR 0037](../adr/0037-spine-actors-and-the-player-figure.md), [`dependencies.md`](../architecture/dependencies.md) (§2.2 Spine row), `ATTRIBUTION.md`, `llmwiki/stage-log.md`; no new wiki note was created (INGEST: a new page is the user's call) |
| — | Fast suite not red | **PASS** | `PASS 13 \| FAIL 0 \| NOT RUN 3 \| WARN 0`, 291.1 s ([`test-summary.md`](../../artifacts/test-summary.md)) |
| — | WebGL build + Playwright | **NOT RUN (deliberate)** | the brief forbids a Web build in this stage; therefore **no** claim about the player's payload with `Resources/body_1110901` (ADR 0011: measure before quoting) |

## 3. What was not tested, and the risks

- **No WebGL player, no Playwright, no size number.** The skeleton ships from `Resources/`, so it *will*
  be in the player's payload; nothing here says how much. Measure it at the next Release-candidate gate
  (`tools/build-web.ps1 -ReleaseCandidate` + `tools/measure-web-size.mjs`, ADR 0011/0035).
- **IL2CPP stripping of the spine runtime is unproven.** The module has no reflection, but
  `SkeletonDataAsset`/`SkeletonRenderer` are large serialized types and the release `link.xml` does not
  name `spine-unity` yet (the link.xml test only checks the six assemblies it names). First Release
  build must pass the browser suite.
- **The licence is a blocker, not a warning** (`ATTRIBUTION.md`, known issues §1b). The art works in
  dev; a release candidate may not ship it.
- **The composed look carries no weapon.** `body`+`face`+`head` leaves the four weapon slots empty, so
  `SpineActorBase` logs `Combined skin leaves 4 empty slot(s): w_dao, w_zhang, w_qiang, w_jian` once per
  build. That warning is explained, not a defect; attaching a weapon is art work for a later stage.
- **One material covers one atlas page.** A future multi-page skin is refused loudly
  (`spine.player_atlas_multi_page`) rather than drawn with the wrong texture.
- **Nothing is attached to a scene.** The figure has only been rendered by the (deleted) Editor probe;
  17b–17d will find out whether the profile's 1.6 world units and 0.1 run threshold suit the camp and
  the arena, and the two numbers are art values that may move.
- **`GC.GetAllocatedBytesForCurrentThread` worked** under Unity's Mono in EditMode; had it not, the
  fallback would have been a `ProfilerRecorder` byte counter. This is the measurement the 0-byte claim
  rests on, and it is per-thread — a future PlayMode test should repeat it in a real frame.

## 4. Commands actually run

```powershell
# Unity, through the open Editor's Pipeline connection
unity status
unity command recompile; unity command recompile_status
unity command menu "ProjectSurvive/Spine/1. Dump player skeleton"
unity command menu "ProjectSurvive/Spine/2. Create player profile"
unity command menu "ProjectSurvive/Spine/3. Create UI materials"
unity command menu "ProjectSurvive/Spine/9. Probe shaders (temporary)"   # deleted afterwards
unity command run_tests --mode editor --filter SpinePlayerTests --async_tests

# the stage's gate
pwsh -File tools/test.ps1 -Suite Fast

# the Shader.Find evidence
grep -rn "Shader\.Find[[:space:]]*(" UnityProject/Assets/Main/Scripts/Presentation   # 0 matches
```

No WebGL build and no Playwright run: `NOT RUN` by the stage's own scope.
