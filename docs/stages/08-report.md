# Stage 08 — Battle presentation 2.5D

- **Date:** 2026-10-15
- **Prompt:** “ทำ Stage 08 เท่านั้น: Battle Presentation 2.5D” — the Battle scene and its MVP, the
  2D→XZ mapping with a ~50° camera (orthographic vs perspective decided by experiment and an ADR),
  `BattleSimulationRunner` + `BattleRenderBridge` + pooled `SpriteRenderer` views, an input adapter
  and a 1×/2× control, an R3 HUD with a floor timeline, the real `CombatLoadoutSnapshot`, pooled
  floating damage numbers on a visual-only RNG, no `Instantiate`/`Destroy` after warm-up, and
  Presentation restricted to the `Domain.Combat.Battle` vocabulary.
- **Result:** the battle is **playable in a browser**. A Release WebGL player, loaded in Chrome by
  Playwright, starts floor 1 from `?scene=Battle`, steps **1800 ticks (60 s at 30 Hz) with 0 dropped
  ticks**, kills 20 ghosts, ends `Won / SurvivedAllWaves` at full HP, and creates **0 views after the
  pool's warm-up** (1793 pooled views, 32 bound, pool capacity 1793) with **0 dropped events** across
  three event channels. The camera question the GDD left open is decided by measurement
  ([ADR 0018](../../docs/adr/0018-battle-camera-projection.md): orthographic at 50°, near÷far sprite
  scale 1.000 against perspective's 0.832). Presentation still does not name the simulation
  assembly — enforced by a test, not by a comment. No `data/` balance number changed; the only
  content added is 13 localization rows for HUD labels.

## 1. What was built

**The scene and who owns what.** `Assets/Main/Scenes/Battle.unity` (build index 3) is authored by
`ProjectSetup` through the Editor API (AGENTS rule 11), not by hand. Three pieces own the frame:
`BattleSceneDirector` (composes, resolves the ports, builds the `BattleSetup`), \
`BattleSimulationRunner` (owns the clock and the session), `BattleViewScene` (owns the pixels: art,
pool, bridge, floor, camera, HUD wiring, VFX). Splitting the runner from the view is what lets an
EditMode test build the view, hand it a snapshot and assert that a view was reused — no simulation
needed.

**The seam.** `Presentation/Battle/**` compiles against `ProjectSurvive.Core.Domain.Combat.Battle`
only: `IBattleFrame`, `IBattleSession`, `IBattleEventSource`, `RenderEntity`, `HudState`,
`BattleResult`, `BattleConstants`, `EntityRef`. It may not reference `ProjectSurvive.Core.Simulation`
— asserted by `Presentation_may_not_reference_the_simulation_assembly`, which strips comments first
so a class may still *explain* the rule it obeys. `BattleSceneScope : RootLifetimeScope` is the one
assembly that names both the port and its implementations, and its `_useDevBattle` flag binds the
Stage 02b dev session instead of `EcsBattleSessionFactory` — the same view, HUD, pools, input and
clock then run against a fake, which is how the prompt's “พัฒนากับ Fake ก่อน” remained true at the
end rather than only during development. `RootLifetimeScope` became non-sealed so the Battle scene
inherits the shell's registrations instead of keeping a parent/child pair in step by hand.

**The clock, and what a stall does.** `BattleSimulationRunner.Update` is the only place in the
project where `Time` meets the battle: `delta × speed` goes into `FixedTicker` (30 Hz,
`MaxStepsPerFrame` cap), the loop runs at most that many `Step`s, and the accumulator's leftover
fraction becomes the interpolation alpha. A stalled tab **drops** ticks (counted in `DroppedTicks`)
instead of replaying them; `OnApplicationPause` resets the ticker, so an inactive browser tab owes
nothing when it returns. `BattleSpeed` (R3 `ReactiveProperty`) scales the delta and never touches the
simulation: 2× buys twice as many ticks per second of wall clock, still bounded per frame.

**The frame copy.** `BattleFrameSnapshot` copies `Entities` / `PreviousEntities` into private arrays
immediately after the last step of a frame and before anything else can step — the acceptance line
“span ของ `Entities`/`PreviousEntities` ถูก copy ก่อน `Step`/`Submit` ครั้งถัดไป” turned into one
object with one obligation. The previous tick is indexed by entity slot and validated by version, so
a recycled slot cannot interpolate a new ghost out of a dead one's position (the EcsLite liveness
lesson, on the view side).

**The mapping.** `BattleWorldMap` is the single place `(simX, simY)` becomes `(x, 0, y)`, with
simulation +Y running away from the camera. Depth is **authored**: `SortingOrder(kind, simY)` turns Y
into an explicit sprite order (further away sorts behind) plus a per-kind bias so a bolt draws over
the ghost it just hit. That ordering is why the projection cannot decide occlusion, which is half of
the camera ADR.

**Pools.** `EntityViewPool` warms 1793 views (1 player + 512 ghosts + 1024 bolts + 256 drops, mirroring
the simulation's structural caps in `BattleViewCaps`) before the first ghost exists, and counts every
view it creates after warm-up so “ไม่ Instantiate/Destroy ต่อเนื่องหลัง pool warm-up” is **measured**:
the counter is published in the browser report and asserted by both the PlayMode test and
`tests/web/battle.spec.ts` (0 in the finished floor). `FloatingDamagePool` pools 48 world-space labels,
caches `int.ToString()` up to 200, is driven by one `Tick` instead of a `MonoBehaviour` per number, and
takes its jitter from `RandomStreams.Visual` — never the battle stream, never `UnityEngine.Random`.
`BattleTimelineView` pools its markers and re-anchors them.

**Events.** `BattleEventPump` reads `IBattleEventSource` once per tick and fans out to three channels
(HUD, VFX, Audio) with **one cursor each**, so a slow consumer cannot cost another one its events and
“the VFX dropped 4 events” is a number rather than a guess. A channel that falls behind the ring
counts what it lost and logs it in a development build. Events never decide anything.

**The HUD.** `BattleHudState` holds scalars only (HP/MP, bia, elapsed, kills, wave, timeline progress,
end banner) — `Hud_state_carries_scalars_and_never_an_entity` asserts it, which is what makes “HUD
ไม่ subscribe ตำแหน่งทุก entity” true by construction: 300 ghosts cost what one ghost costs.
`BattleHudPresenter` is the only writer, formats numbers and looks every caption up in localization
(AGENTS rule 5), and guards each publish on its **inputs** so a HUD at constant HP builds no string
and R3 publishes nothing. `BattleHudView` mirrors reactive values and computes nothing; its one
interaction is the 1×/2× button, which talks to `BattleSpeed` directly because that control *is* UI
state. `BattleTimeline` reads the elite/boss windows out of `data/` and hands the view normalized
spans; the colours are the view's, the meaning is the model's.

**The real loadout.** `BattleRunSource` resolves the run's actual recorded bag through the application
layer and builds the `BattleSetup` from the real `CombatLoadoutSnapshot`; the HUD lists the equipped
spells by item, so “แสดงคาถาตามไอเทมที่ติดตั้ง” is the same data the simulation is casting from.
`The_bag_the_player_builds_changes_the_loadout_and_the_battle` plays two whole floors (1800 ticks
each) from two different bags and asserts they diverge.

**Art.** `BattlePlaceholderArt` generates every sprite in code (disc, ghost blob, bolt, drop, tiled
floor) — nothing copied from a referenced game, nothing to attribute, no binary asset in the
repository (AGENTS rule 13). Elite and boss are the ghost sprite at exactly
`BattleArena.EliteRadiusScale` (1.4×) and `BossRadiusScale` (2×), so the silhouette is the hitbox; a
Unity test asserts the two pairs still agree, since Presentation may not read them.

**One contract change.** `IBattleSession` is now `: IDisposable`. The GDD's lifetime rule (“leaving
Battle disposes the ECS world”) can only be a property of the seam if the seam can be disposed;
leaving it to a cast at the call site would make the guarantee a convention. The fake implements
`Dispose` too (`DevBattleSessionBinding` reached it first).

## 2. Files that matter

| Path | What it is |
| --- | --- |
| `Assets/Main/Scenes/Battle.unity` | the scene, authored by `ProjectSetup` (menu: *Stage 08/3*) |
| `Assets/Main/Scripts/Battle/BattleSceneScope.cs` | composition root + the real-vs-fake switch |
| `Assets/Main/Scripts/Battle/BattleSceneDirector.cs` | starts/stops a floor, publishes the scene marker |
| `Assets/Main/Scripts/Battle/BattleRunSource.cs` | the run's recorded bag → `BattleSetup` |
| `Presentation/Battle/BattleSimulationRunner.cs` | the clock, the frame copy, the browser report |
| `Presentation/Battle/BattleViewScene.cs` | the view: art, pool, bridge, floor, camera, HUD, VFX |
| `Presentation/Battle/BattleFrameSnapshot.cs` | the private copy of a published frame |
| `Presentation/Battle/BattleRenderBridge.cs` | entity → pooled view binding + interpolation |
| `Presentation/Battle/EntityViewPool.cs`, `EntityView.cs` | the sprite pool and its counters |
| `Presentation/Battle/BattleWorldMap.cs` | 2D→XZ, interpolation, sorting order |
| `Presentation/Battle/BattleCameraRig.cs` | 50° rig, both projections |
| `Presentation/Battle/BattleEventPump.cs` | three event channels with their own cursors |
| `Presentation/Battle/BattleInput.cs`, `BattleSpeed.cs` | input frame adapter, 1×/2× |
| `Presentation/Battle/BattlePlaceholderArt.cs` | generated sprites |
| `Presentation/Battle/Hud/*` (4 files) | state / presenter / view / timeline |
| `Presentation/Battle/Vfx/FloatingDamagePool.cs` | pooled damage numbers on the visual RNG |
| `Presentation/Battle/BattleViewCaps.cs`, `BattleViewScope.cs` | structural bounds; owned disposables |
| `Assets/Main/Scripts/Editor/BattleCameraProbe.cs` | the ADR 0018 measurement + menu item |
| `Assets/Main/Tests/EditMode/BattlePresentationTests.cs` | 15 tests: the seam, the copy, the pools, the HUD, the bag |
| `Assets/Main/Tests/PlayMode/BattlePlayModeTests.cs` | 4 tests: scene start, 10 s of ticks, teardown, re-entry |
| `tests/web/battle.spec.ts` | the browser playthrough of floor 1 |
| `lib/Runtime/.../IBattleSession.cs`, `Fake/FakeBattleSession.cs` | `IDisposable` on the contract |
| `lib/Runtime/Application/Battle/Fake/DevBattleSessionBinding.cs` | the dev session, reachable without naming it |
| `data/localization/localization.csv` | +13 `hud.*` rows (labels only) |
| `docs/adr/0018-battle-camera-projection.md` | the camera decision and its measurement |
| `llmwiki/systems/battle-presentation.md` | the system note |
| `llmwiki/gotchas/webgl-size-profile-settings-residue.md` | the build trap found in §6 |

Changed: `RootLifetimeScope` (unsealed), `SceneBoot` (URL allow-list now `ThaiTextSpike`, `Battle`),
`JsonReportWriter` (`BeginObjectProperty`), `ProjectSetup` (+ Battle scene item, + URL allow-list,
+ camera factory), four asmdefs (Presentation gains `Unity.InputSystem`; the test assemblies gain
`Battle`/`Simulation`/`EcsLite`), `EditorBuildSettings` (Battle in the list), `ConfigLoadTests` (the
localization count, 51 → 64).

## 3. Commands actually run

```powershell
# Rule engine + architecture (lib/), Release
cd lib/Tests~ ; dotnet test ProjectSurvive.Core.sln -c Release --nologo

# Unity Editor suites (MCP pipeline)
run_tests --mode editor                 # 47 tests incl. the 15 presentation ones
run_tests --mode playmode --async_tests --filter Battle     # 4 battle scene tests

# The ADR 0018 measurement (Editor menu)
ProjectSurvive/Stage 08/4. Measure camera projection (ADR 0018)

# The real player, in a real browser
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Release
node tools/serve-web.mjs --port 8080
cd tests/web ; npx playwright test          # battle.spec.ts + smoke.spec.ts
```

## 4. Results and where the artifacts are

| Gate | Result | Artifact |
| --- | --- | --- |
| `dotnet test` (lib/, Release) | **413 / 413 passed**, 497 ms | §3 command; console |
| EditMode (Unity, all) | **47 / 47 passed**, 4.13 s — 15 of them `BattlePresentationTests` | MCP result payload; log below |
| PlayMode (`filter Battle`) | **4 / 4 passed**, 7.92 s | `artifacts/stage08/playmode-editmode-testresults.xml` |
| WebGL build (Release, CLI) | **Succeeded** — 64,071,218 bytes, **0 errors, 1 warning**, 154.7 s, 4 scenes incl. Battle | `artifacts/unity-build-web.log`, `artifacts/stage08/build-info-release.json`, `build-report-release.json` |
| Browser suite (Release player, Chrome, 127.0.0.1:8080) | **4 / 4 passed** (2 battle + 2 smoke) | `artifacts/stage08/playwright-final-release.log` |
| Floor 1 played to its end in the browser | **`Won / SurvivedAllWaves`**, tick 1800, stepsRun 1800, droppedTicks 0, kills 20, finalHp 100 | same log |
| Nothing created after warm-up | **`viewsGrownDuringPlay: 0`** of `poolCapacity: 1793`, `viewsBound: 32`, `droppedEvents: 0` | same log |
| Smoke self-check in the same player | **7 / 7 probes, `passed: true`**, scene reaches `Title` | same log |
| Camera decision | logged in ADR 0018 with the raw numbers | `artifacts/stage08/camera-projection.json` |
| Editor console after the runs | **0 errors, 1 warning** — the Input Manager deprecation notice, which follows from the project's `activeInputHandler` setting (unchanged in Stage 08; `ProjectSettings.asset` has no diff) | MCP `console` payload |

The browser evidence, verbatim from `playwright-final-release.log`:

```jsonc
// start (the report published at Begin, before the first step)
{"scene":"Battle","phase":"Running","tick":"0","stepsRun":"0","droppedTicks":"0","speed":1,
 "viewsCreated":1793,"viewsGrownDuringPlay":0,"viewsBound":0,"poolCapacity":1793,
 "timelineMarkers":1,"droppedEvents":"0","ended":false}

// end (floor 1, 60 s of game time at 1×)
{"scene":"Battle","phase":"Won","tick":"1800","stepsRun":"1800","droppedTicks":"0","speed":1,
 "viewsCreated":1793,"viewsGrownDuringPlay":0,"viewsBound":32,"poolCapacity":1793,
 "damageNumbers":0,"timelineMarkers":1,"droppedEvents":"0","ended":true,
 "hp":100,"maxHp":100,"mp":0,"bia":0,"kills":20,"waveIndex":0,"remainingSeconds":0,
 "outcome":"Won","endReason":"SurvivedAllWaves","finalHp":100}
```

Screenshots: `artifacts/stage08/web-battle-mid.png`, `web-battle-end.png` (the browser's own view of
the floor), `battle-scene-editor.png`, `battle-floor1-won.png` (the Editor, Stage 08 development).

The one build warning is pre-existing and not this stage's: the Unity Pipeline package logs
`No RuntimePipelineConfig asset found … Pipeline will be disabled in Player builds` on every build
(it is an *Editor* pipeline feature; the player is meant to be a player). It is present in the
Stage 03b log too (`artifacts/size-build-optimize-size.log`), so it is a property of the package,
not of the Stage 08 code.

## 5. What was not tested, and why

- **Frame timing / FPS in the browser.** No timing number is claimed anywhere in this stage. The
  browser run proves the floor completes and that the frame never had to grow a pool, not how fast a
  frame was; a profiler capture in a Release player on the ADR 0003 reference machine is the
  follow-up (`profiler-*` / WebGL `webGLShowDiagnostics`).
- **The camera measurement on other browsers/GPUs.** ADR 0018's numbers come from the Editor probe at
  1280×720 on the recorded machine. The *relative* result (a parallel projection has no scale
  gradient) is geometry, not hardware, but no second browser was measured.
- **A damage number appearing in the browser.** `damageNumbers` is published, and the mid-floor sample
  read 0 — the labels live 0.75 s, so a 5-second sample says nothing either way, and the sampled
  moment had no fresh damage event. The pool is exercised in the PlayMode floor run and its RNG
  isolation is asserted (`A_rendered_floor_hashes_the_same_as_an_unrendered_one`), but **no test
  asserts that a damage event becomes a visible label**; that assertion is the one I would add next.
- **Audio.** `BattleEventChannel.Audio` exists and reads at its own pace, with no listener: silent in
  Stage 08 by design.
- **The dev session in a browser.** `_useDevBattle` is exercised in the Editor only; the shipped scene
  has it off, and the browser runs the real EcsLite battle.
- **The `Lost` arm of acceptance 1.** The shipped run wins at full HP. The `Lost` path is covered in
  `lib/Tests~` (the contract suite and the bot's floor runs) and by the EditMode/EcsLite tests, not by
  a browser run.
- **Re-entering the Battle scene inside a browser session.** Covered by a PlayMode test
  (`Re_entering_the_battle_scene_starts_a_fresh_floor`); the browser test enters it once, because the
  run flow that would take it back to camp is Stage 09/10.

## 6. Blockers and risks

**A size-profile build poisoned a Release build, and it looked like a code bug.** Worth reading in
full: [`llmwiki/gotchas/webgl-size-profile-settings-residue.md`](../../llmwiki/gotchas/webgl-size-profile-settings-residue.md).
`WebSizeProfiles.ApplyAndPersist` (behind `tools/build-web.ps1 -SizeProfile` and the Stage 03b menu
items) *persists* player settings, and they were still in `ProjectSettings.asset` when this stage
built its Release player. That build inherited `managedStrippingLevel: High`,
`exceptionSupport: None`, `il2cppCodeGeneration: OptimizeSize` — and halted in the boot scene's first
managed code with `Exception at:` (empty message), `Uncaught exception from main loop`,
`Uncaught undefined`, `Halting program`, publishing no page global at all. With exception support off,
a managed exception cannot be materialized, so the message and the stack are gone; the build itself
reported `0 errors, 1 warning`. Evidence: `artifacts/stage08/browser-boot-crash-size-profile-residue.log`
beside `build-report-size-profile-residue.json` (the `sizeSettings` that produced it). Restoring
`ProjectSettings.asset` to the authored values (stripping **Low**, exceptions
**ExplicitlyThrownExceptionsOnly**, codegen **OptimizeSpeed** — what `ProjectSetup` sets and
`WebSizeProfilesTests.Baseline_is_the_configuration_the_project_already_had` asserts) and rebuilding
fixed it with no other change. **This is not Stage 08's bug, but no earlier stage could have caught it
either:** Stage 03's browser gate ran a *Development* build, and Stage 03b only measured bytes, so a
Release player had never been opened in a browser before this stage. The durable fix — a baseline
reset between profiles, or `build-web.ps1` refusing a settings/profile mismatch — belongs to the stage
that owns the build tooling; I did not make it here.

**Two scene files carry a large, semantically empty diff.** Re-running `ProjectSetup`'s authoring step
re-creates the scenes, and `EditorSceneManager` assigns fresh fileIDs, so `Title.unity` and
`ThaiTextSpike.unity` show ~520 lines changed while their content (scripts, references, hierarchy) is
the same. They are committed because they are what the tool produced and what the three suites ran
against. A scene-authoring step that loads and patches instead of recreating would make this diff
disappear.

**`?scene=Battle` needed a second half.** `SceneBoot`'s allow-list is what makes a URL override
possible, and it shipped with `ThaiTextSpike` only; the browser test's first run timed out waiting for
the Battle scene to identify itself. Both halves are now in place (allow-list **and** a scene marker
published by `BattleSceneDirector`, matching how `TitleView` and `ThaiSpikeReporter` report
themselves) — but the shape of the trap is that a browser test can only observe what a scene
*volunteers*, so a new scene needs both.

**Risks carried into Stage 09/10.** The Battle scene is a one-floor scene: `BattleViewScene.Begin` is
called once per visit, and Stage 10's “reuse the scene for floor N+1” will call it again (the floor,
the pool and the art are idempotent; the session is replaced). The HUD mirrors `startingBia +
battle-earned` itself because no run scope is in the scene yet — Stage 10 hands it the real one. The
prototype still earns no bia inside a battle (ADR 0017), so the bia line in the HUD is a running total
of what the player walked in with.

## 7. Acceptance criteria

| # | Acceptance (Stage 08 prompt) | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | เล่นชั้น 1 จนชนะหรือแพ้ได้ใน Web | **PASS** | Release WebGL player in Chrome: `outcome: Won`, `endReason: SurvivedAllWaves`, tick 1800, kills 20 (`playwright-final-release.log`); the `Lost` arm is covered off-browser (§5) |
| 2 | เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง | **PASS** | `The_bag_the_player_builds_changes_the_loadout_and_the_battle` (full floors from two bags diverge); the loadout is the real `CombatLoadoutSnapshot`, and the HUD lists the equipped spells |
| 3 | HUD ไม่ subscribe ตำแหน่งทุก entity | **PASS** | `Hud_state_carries_scalars_and_never_an_entity`; `BattleHudState` has no entity, position or list; the browser run binds 32 views with a constant number of HUD subscriptions |
| 4 | ออก Battle แล้ว ECS world และ subscription ถูก dispose | **PASS** | `Leaving_the_battle_scene_disposes_the_world_and_the_subscriptions` (world destroyed, R3 `ReactiveProperty.IsDisposed`, pool released); `IBattleSession : IDisposable` makes it a contract |
| 5 | PlayMode: เข้า Battle แล้ว tick 10 วินาทีไม่มี exception | **PASS** | `Ten_seconds_of_ticks_raise_no_exception_and_bind_no_new_view`; the browser run went 60 s / 1800 ticks with no page error |
| 6 | Presentation อ่านเฉพาะ `IBattleFrame` / `IBattleEventSource`; span copy ก่อน `Step`/`Submit` ถัดไป | **PASS** | `Presentation_may_not_reference_the_simulation_assembly`; `Snapshot_copies_the_prev_cur_pair_and_survives_the_next_publish`; `BattleFrameSnapshot.CopyFrom` is called inside the frame that stepped |
| 7 | ไม่ Instantiate/Destroy ต่อเนื่องหลัง pool warm-up | **PASS** | `Pool_warms_up_once_and_hands_the_same_view_back` + `viewsGrownDuringPlay: 0` of 1793 in the browser |
| 8 | กล้อง: ทดลอง orthographic/perspective แล้วเลือกด้วย ADR | **PASS** | [ADR 0018](../../docs/adr/0018-battle-camera-projection.md) + `artifacts/stage08/camera-projection.json`; occlusion and scale measured, orthographic chosen |
| 9 | fixed tick + interpolation (TickDelta, MaxStepsPerFrame, Prev→Cur) | **PASS** | the runner's accumulator + `Bridge_reuses_one_view_per_entity_and_interpolates_between_ticks`; `droppedTicks: 0` over a full browser floor |
| 10 | ปุ่มความเร็ว 1x/2x; HUD timeline มี marker elite และบอส | **PASS** | `Speed_control_toggles_between_one_and_two_and_the_presenter_labels_it`; `Timeline_marks_the_elite_window_and_the_boss_window_from_the_tables`; `timelineMarkers: 1` in the browser |
| 11 | event cursor ของตัวเอง; dropped > 0 ให้ log ใน dev build แต่ state ต้องไม่เพี้ยน | **PASS** | `BattleEventPump` (one cursor per channel, `DroppedTotal`); `droppedEvents: 0` in the browser; no listener can affect state (events are one-way) |
| 12 | พัฒนา UI กับ Fake ได้ก่อนต่อ sim จริง (สลับผ่าน LifetimeScope) | **PASS** | `BattleSceneScope._useDevBattle` ↔ `DevBattleSessionBinding`; the shipped scene has it off, and nothing else in the scene changes |
| 13 | floating damage number แบบ pool; RNG ภาพแยกจาก RNG เกม | **PASS (with a gap)** | `FloatingDamagePool` (48 pooled labels, `RandomStreams.Visual`, cached text); `A_rendered_floor_hashes_the_same_as_an_unrendered_one` proves rendering cannot move the battle — but a *label appearing on a damage event* is not asserted (§5) |
| 14 | Compile through; no new unexplained warning | **PASS** | Release build `Succeeded`, 0 errors, 1 pre-existing warning (§4) |
| 15 | Tests added/adjusted and actually run | **PASS** | 413 dotnet + 47 EditMode (15 new) + 4 PlayMode (4 new) + 4 browser (2 new), all green |
| 16 | Docs + test commands updated; report recorded | **PASS** | this file, ADR 0018, `llmwiki/systems/battle-presentation.md`, the gotcha note, `llmwiki/index.md`, `stage-log.md` |
