# T1 — Template extraction (core, data, Unity shell, tooling): status report

Worktree `C:\Workspace\ProjectSurvive-template`, branch `template/main`, base `972a655`.
Scope of this pass: all three parts — T1a (`lib/` + `data/`), T1b (Unity shell) and T1c (tools, fixtures,
web specs, docs) — each with the evidence that it is green.

## 1. Summary

The core library is now game-agnostic and compiles warning-free: the battle/inventory/run/shop rules,
the ECS engine and the backpack save contract are gone; `Application/Run` carries a small run skeleton
(`RunSession`, `ReplayLog`) and `Application/Flow` carries `SceneId` plus a tested `SceneFlow` state machine.
Static data is down to two tables (`tbconstant`, `tblocalization`) and regenerates through `tools/gen.ps1`
with a new config hash. 115 core tests pass, the validator reports no findings, the Sim prints three
distinct stream digests, and the Unity suites play the loop end to end. The browser gate is closed too: a
Development WebGL build succeeds and Playwright walks the loop in the built player by pressing the controls
each screen published (4 specs, Chrome).

## 2. What changed

| Area | Change |
|---|---|
| `lib/Runtime/Shared` | added `RandomStreamKind` (`Run`/`Encounter`/`Reward`/`Visual`) and `RandomStreams` over it |
| `lib/Runtime/Domain` | `Domain/Combat`, `Inventory`, `Run`, `Shop` removed; `Domain/Config` reduced to `IGameConfig`, `ConstantConfig`, `LocalizationEntry`, `Language` |
| `lib/Runtime/Application` | new `Flow/SceneId`, `Run/RunSession`, `Run/ReplayLog`, `Run/RunTypes`, `Run/SceneFlow`; `Application/Battle/Fake` removed |
| `lib/Runtime/Simulation` | battle simulation removed; asmdef now references Shared + Domain only, with `SimulationLayerMarker.cs` |
| `lib/Runtime/Infrastructure` | `Config/LubanGameConfig` rewritten for the two tables; `Save/**` (backpack snapshot) removed |
| `lib/third_party/ecslite` | removed, along with its `.csproj`, `versions.lock.json` entry and `dependencies.md` rows |
| `lib/Tests~` | `Core.Tests` rewritten (config, rules, enum parity, architecture, RNG); `Data.Validator` trimmed to the generic rules; `Sim` reduced to one streams mode |
| `data/` | `schema.xml` + `__tables__.csv` + `constant.csv` + `localization.csv` reduced to the template tables; 12 table CSVs deleted |
| generated | `lib/Runtime/Generated/Luban/**`, `lib/Tests~/data/*.bytes`, `Resources/Config/*.bytes` all produced by `tools/gen.ps1` |
| `UnityProject/Assets/Main/Scripts` | `Battle/**`, `Camp/**`, `Presentation/Battle/**`, `Presentation/Camp/**` removed; `Presentation/Hub/**` and `Presentation/Encounter/**` added (state, presenter, view, director, scope); `RootLifetimeScope`, `TitlePresenter`, `Result*`, `Flow/*`, `SceneBoot` adapted |
| `UnityProject/Assets/Main/Scenes` | `Battle.unity`, `Camp.unity` removed; `Hub.unity` and `Encounter.unity` built, `Result.unity` rebuilt — all through the Editor API via `ProjectSetup`/`*SceneBuilder`, never hand-written YAML |
| `UnityProject/Assets/Main/Scripts/Editor` | `HubSceneBuilder`, `EncounterSceneBuilder` added; `ResultSceneBuilder` rebuilt; `BattleSceneBuilder`, `CampSceneBuilder`, `BattleCameraProbe`, `BattleCaptureState`, `UiCaptureTool`, `UiLayoutCheck` removed with what they measured |
| `UnityProject/Assets/Main/Tests` | game-screen suites deleted; `LoopScreenTests` (EditMode, 7) and `LoopPlayModeTests` (PlayMode, 2) added; `ShellPlayModeTests` de-harnessed |
| `tests/web` | `title.spec.ts` retargeted at the Hub, `loop.spec.ts` added; `globals.mjs` stays plain JS and the report shapes moved to `report-types.ts` |

## 3. Acceptance (measured)

| Check | Result | Evidence |
|---|---|---|
| `dotnet build lib/Tests~/ProjectSurvive.Core.sln` | **PASS** — 0 errors, 0 warnings | see §5 command 1 |
| `dotnet test` Core.Tests | **PASS** — 115 passed, 0 failed | §5 command 2 |
| `pwsh tools/gen.ps1` | **PASS** — 6 `.cs`, 2 `.bytes`, config hash `8CEF044B…` | §5 command 3 |
| `Data.Validator --data lib/Tests~/data` | **PASS** — no findings | §5 command 4 |
| Sim, 3 streams, same seed repeats | **PASS** — `encounterDigest≠visualDigest≠runDigest`; reruns identical | §5 command 5 |
| No `UnityEngine` in Domain/Application/Simulation | **PASS** — `ArchitectureTests` fails the build if one appears | Core.Tests |
| No EcsLite / Leopotam reference left in `lib/` + `tools/versions.lock.json` | **PASS** — `git grep` finds no tracked hit (stale `bin/`/`obj/` artifacts from the old build remain on disk, ignored by Git) | §5 command 6 |
| `tools/test.ps1 -Suite Full` (Editor closed) | **PASS** — 10 PASS, 0 FAIL, 2 NOT RUN (the two Unity suites, which need a connected Editor) | §5 command 10 |
| `tools/test.ps1 -Suite Full` (Editor open) | **PASS** for the Unity half — `unity: EditMode` 30/30 and `unity: PlayMode` 6/6; the web half reports NOT RUN because an Editor holding the lock cannot build | §5 commands 7–8 |
| `tools/sim.ps1` (rewritten for the streams mode) | **PASS** — runs, writes `artifacts/sim-streams.txt` | §5 command 8 |
| `tools/check-fixtures.ps1` | **PASS** — 6 PASS / 0 FAIL, both fixtures rejected for the right reason | §5 command 9 |
| WebGL build (Development, `tools/build-web.ps1 -Force`) | **PASS** — exit 0, 0 `error CS`, `index.html` + 1 wasm, `build-info.json` config hash `C12A4424…` | §5 command 10; `artifacts/dotnet/web-build.log` |
| Playwright (`smoke`, `title`, `loop`) | **PASS** — 4 passed in Chrome 8080 (0 failures) | §5 command 10; screenshots in `artifacts/web/` |

## 4. How T1 was split, and what each part closed

AGENTS.md asks for a halt and a proposal when a stage passes roughly seven items or touches Core, Unity
scenes, UI and web tests at once. T1 does exactly that, so it was split into three parts and each was closed
on its own evidence: T1a (core + data), T1b (Unity shell), T1c (tools, fixtures, web specs, docs).

### T1a — core + data (done, green)

1. Application run/flow skeleton, typed refusals, `SceneFlow` edges.
2. Config trimmed to two tables, domain mirror enum + parity test.
3. EcsLite removed everywhere it was listed.
4. Validator, fixtures and Sim reduced to the generic rules.
5. Docs: `dependencies.md` rows, this report.

### T1b — Unity shell (done)

1. Deleted `Scripts/Battle`, `Scripts/Camp`, `Presentation/Battle`, `Presentation/Camp`, their asmdefs, scenes and suites.
2. Generalized the survivors: `RootLifetimeScope` (no `DevCheats`), `TitlePresenter/View`, `Result*`, `SceneLoader`, `SceneBoot`.
3. Added the Hub ⇄ Encounter placeholder screens, one file per responsibility each, and wired `SceneFlow` into them (it now has a caller, not just tests).
4. Rebuilt every scene through the Editor API: `Hub.unity` and `Encounter.unity` are new, `Result.unity` was rebuilt for the trimmed state, and Boot's URL allow-list now names the loop.
5. Deleted `UiCaptureTool` / `UiLayoutCheck` with the camp/battle layout they measured; screenshots are taken from the Game view instead (see §6).
6. Replaced the game-screen suites with `LoopScreenTests` (7) and `LoopPlayModeTests` (2); ShellSmoke/ShellPlayMode adapted.

Two real defects surfaced while running it, and both are fixed: a press on the frame a transition is still
fading in was logged as an error instead of being retried (`transition.in_progress`), and the Encounter scene
loaded on its own could not start because the run had never been started.

### T1c — tools, web, docs (done, except the loop browser spec)

Done in this pass:

1. `tools/sim.ps1` rewritten for the single streams mode (`--seed/--ticks/--tick-hz/--frame-hz/--run-interval-ticks/--trace`).
2. `tools/test.ps1` — the bot step is gone, the stream check reads `run/encounter/visual`, and the Playwright
   list is `smoke` + `title` + `loop` (the third spec joined once a build could be reached).
3. Fixtures rebuilt against the two surviving tables: `out-of-range` (Luban rejects `max_hp = 0`) and
   `blank-text` (the validator's `localization.blank_text`). `duplicate-id` could not be kept: Luban's indexed
   table collapses a duplicate key before the loader sees it, so no end-to-end fixture exists for it — the rule
   is still covered by `ConfigRulesTests` on a snapshot. The fixtures dir had to put the localization overlay at
   `localization/localization.csv`, not `tables/` (the first attempt silently regenerated the control data —
   the identical config hash gave it away).
4. `tests/web`: `battle`, `camp`, `result` and `screens` specs deleted with their screens; `title.spec.ts`
   rewritten for the template's own handover (Title → Hub) and `loop.spec.ts` added — Title → Hub → Encounter
   → Hub → Result → Title, every step a click at the point the screen published, every assertion the game's
   own answer.
5. `HubReport` / `EncounterReport` added so those two screens publish what they show and where their controls
   are (the title and the summary already did). They publish from the view's first `LateUpdate`: the presenter
   fills the state in its own `Start`, the order of the two is undefined, and a click point wants the final
   layout.
6. Docs: `AGENTS.md` (template identity, Simulation layer, four streams), `knowledge.md` (template status
   block), `docs/testing/test-suites.md` (template note + trimmed step table), `llmwiki/stage-log.md`.

Still open: the `llmwiki` systems/gotchas move, and `tools/rename.ps1` + `tools/check-links.mjs` from the
Stage 3 plan.

## 5. Commands actually run

```bash
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -v q --nologo
dotnet test  "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" --nologo -v q
pwsh -NoProfile -File tools/gen.ps1
dotnet run --project "lib/Tests~/Data.Validator/ProjectSurvive.Data.Validator.csproj" -- --data "lib/Tests~/data"
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 7 --ticks 300
git grep -n "EcsLite\|Leopotam" -- lib tools/versions.lock.json docs/architecture/dependencies.md
pwsh -NoProfile -File tools/sim.ps1 --seed 7 --ticks 300
pwsh -NoProfile -File tools/check-fixtures.ps1
pwsh -NoProfile -File tools/test.ps1 -Suite Full          # 7. Editor open: Unity suites green, web NOT RUN
# (Editor closed, and after npm install in tests/web)
pwsh -NoProfile -File tools/build-web.ps1 -Force           # 9.
pwsh -NoProfile -File tools/test.ps1 -Suite Full           # 10. PASS 10 | FAIL 0 | NOT RUN 2
cd tests/web && npx playwright test smoke.spec.ts title.spec.ts loop.spec.ts   # 11. 4 passed
pwsh -NoProfile -File tools/doctor.ps1
```

## 6. Risks

- `SceneFlow.CurrentScene` starts at `Title` and each director calls `SyncTo` when it starts. A scene that is
  entered by some path nobody wrote a director for would leave the flow one edge behind — the loop has five
  scenes and five directors, so a sixth scene needs its own call.
- `UiCaptureTool`/`UiLayoutCheck` are gone: the layout predicate that kept the camp's panels apart has no
  template equivalent yet, so panel-overlap regressions in the new screens would only show in a screenshot.
- The template's Encounter is a placeholder with a fixed "1 of 1" progress line (`EncounterPresenter.EncounterProgressTotal`);
  a game must replace it with its own schedule.
- The domain `Language` mirror now carries `1/2` (the generated values). A future renumbering in `schema.xml`
  is caught by `ConfigEnumParityTests`, not by the compiler.
- The browser gate needs two runs, not one: a connected Editor is what the Unity suites need and what a
  batch build cannot have. `tools/test.ps1` says so on the NOT RUN line instead of passing quietly, so a
  stage report must quote both runs — one of them always has two NOT RUN rows.
- `loop.spec.ts` walks **one** Hub ⇄ Encounter round. The twenty-round memory claim stays a PlayMode test
  (`LoopPlayModeTests`): twenty browser round trips would be twenty scene loads in a player that has to
  re-download nothing but still spends seconds per fade.
