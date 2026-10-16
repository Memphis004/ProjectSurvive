# ProjectSurvive — Knowledge Base

Thai-ghost-hunter survivor-like (Vampire Survivors style) with a grid **bag**
inventory (inspired by *Backpack Immortal Tale*). Theme: a Thai folk
ghost-doctor (`Mor`), 1 class, 2.5D. Target: **Web (WebGL2, single-thread) on
desktop browsers.**

This file is the fast on-ramp. Read it first, then follow the links.

---

## Status

| Stage | Title | Status |
| --- | --- | --- |
| **00** | **Discovery & Baseline** | **DONE** — see [`docs/stages/00-report.md`](docs/stages/00-report.md) |
| **01** | **Core library & dotnet harness** | **DONE** — see [`docs/stages/01-report.md`](docs/stages/01-report.md) |
| **02** | **Static data pipeline (Luban, protoc)** | **DONE** — see [`docs/stages/02-report.md`](docs/stages/02-report.md) |
| **02b** | **Battle contract v1 (interfaces + fake + contract tests)** | **DONE** — see [`docs/stages/02b-report.md`](docs/stages/02b-report.md) |
| **03** | **Unity shell, first Web build, Thai text spike** | **DONE** — see [`docs/stages/03-report.md`](docs/stages/03-report.md) |
| **04** | **Prototype data (14 pieces, 5 combos, 5 floors)** | **DONE** — see [`docs/stages/04-report.md`](docs/stages/04-report.md) |
| **05** | **Inventory domain (bag rules, combos, loadout)** | **DONE** — see [`docs/stages/05-report.md`](docs/stages/05-report.md) |
| **06** | **Shop, economy, run, scene flow** | **DONE** — see [`docs/stages/06-report.md`](docs/stages/06-report.md) |
| **07** | **Battle core (headless) & bot sim** | **DONE** — see [`docs/stages/07-report.md`](docs/stages/07-report.md) |
| **08** | **Battle presentation 2.5D** | **DONE** — see [`docs/stages/08-report.md`](docs/stages/08-report.md) |
| **09** | **Camp scene & จัดย่าม UI (walkable sala, world shop)** | **DONE** — see [`docs/stages/09-report.md`](docs/stages/09-report.md), [`09b`](docs/stages/09b-report.md) (the drag diagnosis), [`09c`](docs/stages/09c-report.md) (the walkable camp) |
| **09d** | **Thai text sharpness (font report, 22 px floor, on-screen measurements)** | **DONE** — see [`docs/stages/09d-report.md`](docs/stages/09d-report.md) |
| **09e** | **จัดย่าม layout & compositing (stats strip, panel alpha, tooltip, synergy marks)** | **DONE** — see [`docs/stages/09e-report.md`](docs/stages/09e-report.md) |
| **09f** | **A bag move or turn carries the items standing on it** | **DONE** — see [`docs/stages/09f-report.md`](docs/stages/09f-report.md), [ADR 0020](docs/adr/0020-bag-moves-carry-the-items-on-them.md) |
| **09g** | **Upgrade-choice window on SceneCamp** | **DONE** — see [`docs/stages/09g-report.md`](docs/stages/09g-report.md), [ADR 0021](docs/adr/0021-merge-upgrade-choice.md) |
| 10 | Full loop (vertical slice) | not started |
| 11 | Thai ghost AI, Pob boss, balance | not started |
| 12 | Save, resume, settings | not started |
| 13 | Content complete & UX | not started |
| 14 | Test harness & CI | not started |
| 15 | Web release candidate | not started |

**Current state:** Stages 00–09, plus **09d** (a measurement pass over 09's Thai text: the font every
canvas uses is `Sarabun-Regular SDF`, the ≥ 22 px floor is a **1080p-base** statement — at 1280×720
every canvas label is drawn at two-thirds of it, measured — and the world-space shelf labels are
12 px tall at best, which is a different rule and still open), **09e** (the จัดย่าม layout and
compositing pass: a `Stats` strip that stops the panel's controls landing on the bar's readouts, a
tested 0.92 alpha floor, the tooltip as a second root canvas at sorting order 100, and synergy marks
chosen from the rule's own `synergy.kind`) and **09f** (a bag now **carries** the items standing on
it — one rigid assembly through `FootprintRotation.Rotate(..., out shift)`, a new
`inventory.item_spans_bags` refusal, one revision and one undo step per gesture, and a ghost per
rider drawn from the run's plan; see [ADR 0020](docs/adr/0020-bag-moves-carry-the-items-on-them.md)), and **09g** (the camp screen now asks the question the merge raised: dropping one piece on another with the same id and tier opens a **dimmed window** over ศาลาพัก when that step has upgrade variants, with the item's current lines on the left — the same five lines the tooltip publishes — and one card per choice, each naming its variant plus the item, its ระดับ N → N+1 and one line per stat it changes in green when the change helps and red when it hurts; a card is picked with a click or `1 2 3`, `Escape` and the back button close the window without changing anything, the board is locked and the floating tooltip suppressed while it is up, and a pair with **no** variants still folds on the drop. The offer behind the window is the second half of 09f — `OfferMerge` / `ConfirmMerge`, per-step variant ids, [ADR 0021](docs/adr/0021-merge-upgrade-choice.md) — and it is a pure function of the run, so closing and reopening the window offers the same cards; the drop preview therefore *accepts* the merge gesture instead of refusing `inventory.merge_needs_variant`; the 20 new localization rows take `data/localization/localization.csv` to **179**, EditMode is **94/94**, and the two camp PlayMode fixtures are green on their own — the drag suite **12/12** and the new chooser suite **4/4** — with real pointer and keyboard events). `lib/` is a real UPM package
(`com.projectsurvive.core`) with six hand-written layer `.asmdef`s plus two
generated ones, the vendored EcsLite, the deterministic `Shared`/`Infrastructure`
primitives (`IRandomSource` / `Xoshiro128`, `RandomStreams`, `IClock` /
`SystemClock`, `FixedTicker`, `Result` / `Error`), and the Stage 02 data pipeline:
fourteen Luban tables under `data/` (the two upgrade tables landed in 09f's second half), generated
C# under `lib/Runtime/Generated/`,
the `IGameConfig` port with its Infrastructure adapter, and the config hash in
build metadata. Stage 02b added the **battle contract v1**
(`Domain/Combat/Battle`: `IBattleSession`, `IBattleFrame`, `IBattleEventSource`,
`BattleEventRing`, `BattleReplay`, the loadout/result/setup types), a deterministic
`FakeBattleSession` in Application, and the executable
`BattleSessionContractTests<TFactory>`. Its amendment (ADR 0012) turned
`BattleFrameBuffer` into a **Prev/Cur double buffer** — `IBattleFrame` carries
`Entities` and `PreviousEntities` so Stage 08 interpolates between two ticks through
the one port it is allowed to read, and exactly one `IBattleFrame` implementation is
allowed (the fake and Stage 07 share it, enforced by a reflection test).
**466 xUnit tests pass** (57 Stage 01, 73
Stage 02, 42 Stage 02b incl. that amendment, 10 Stage 03, 12 Stage 04, 147 Stage 05,
44 Stage 06, 28 Stage 07, 11 Stage 09, the 8 the carried-item rule added in Stage 09f, and the 33
the upgrade choice added — 31 for the engine and the chooser's own rules, 2 for the stat-direction
rule the window colours a card line from). The bag rules, the combo rules, the economy, the run/scene flow
and the **battle simulation** are real code now: `Domain/Combat/Battle` and `Application`
hold the battle **contract** (v1) — which the fake implements as a contract exercise — and
`lib/Runtime/Simulation` holds the real EcsLite battle that implements it, reached only
through `IBattleSessionFactory`.

Stage 03 built the **Unity shell** (`UnityProject/`, Unity `6000.3.9f1` + URP,
referencing `lib/` as a local package): a composition root, three scenes
(Boot / Title / ThaiTextSpike — Stage 08 added the fourth, Battle) built through the Editor API,
a 7-probe smoke suite,
and a WebGL2 build that a Playwright suite asserts on through page globals. It
settled the Thai text question in [ADR 0008](docs/adr/0008-thai-text-ui-stack.md)
(uGUI + TextMeshPro, **Static** baked font assets, 182 characters verified in the
player). Evidence: 12/12 Unity EditMode, 5/5 PlayMode, 2/2 Playwright on the real
build, `totalMissingGlyphs=0` in the browser. The two failures it caught are written
down in [`llmwiki/gotchas/`](llmwiki/gotchas/):IL2CPP strips constructors only reflection can see, and TMP's pre-build processor
empties **Dynamic** font assets at build time.

Stage 04 filled `data/` with the **prototype content and nothing else** (no gameplay
code): 14 pieces — 3 bags, 4 arcane weapons (one per cast behaviour), 3 charms, 1 tome,
2 offerings, 1 familiar — every one of them merging to tier 3, the 5 GDD combo rules
matched by a real **tag vocabulary**, 4 Thai ghosts (incl. the 2-phase Pob boss), the
5-floor run with its wave timeline, a 14-row shop pool covering every piece, and 51
localization rows (th + en). Bag and item shapes are a **picture string** parsed in
Domain (`"XXX|XXX|XXX"`, `"X.|X.|XX"`) — the format the canonical GDD specifies, which
supersedes the Stage 02 cell-token choice ([ADR 0009](docs/adr/0009-footprint-picture-format.md)).
Five new cross-table rules came with it (item footprint, unknown tag, incomplete merge
ladder, unreachable piece, wave spawn window), each proven by a real broken-data
fixture; the chain is now **17 fixtures / 21 checks**.

Stage 05 built the **bag rules** — no UI, no Unity, still no battle sim. `Domain/Inventory`
now holds `Board` (9×7, with a bag-owner and an item-owner map per cell), `Chest` (10),
the piece instances whose permanent `DefId` is separate from the run's `InstanceId`,
footprint **rotation with re-normalisation**, the `Backpack` aggregate (state + `Revision`)
and `BackpackEditor` (atomic `Move`/`Rotate`/`TransferToChest`/`Merge`/`Recycle`/`Undo`/
`Reset`/`SetLocked`, every one returning a typed `Result` and a stable `inventory.*`
code). Undo and “คืนสภาพตอนเข้าศาลา” are **MessagePack snapshots** encoded by an
Infrastructure adapter over a `Contracts` DTO, so the rule engine still never sees a
serializer. `SynergyEvaluator` applies the five GDD combo rules to a layout (adjacency
counted once per pair, row **and** column kinds, decay on both pieces, the bag-area bonus)
and `LoadoutResolver` turns the layout into the **Battle v1** `CombatLoadoutSnapshot`,
cached on `Backpack.Revision` so combos are recomputed only when the set changes. The
weight → speed rule got its curve ([ADR 0013](docs/adr/0013-weight-speed-penalty-curve.md),
5 % per unit capped at 40 %, both from `constant`), the editing/ownership rules are
[ADR 0014](docs/adr/0014-backpack-editing-undo-and-placement-ownership.md), and the
“row or column” ambiguity is settled by [ADR 0015](docs/adr/0015-same-line-synergy-kinds.md).
Confidence comes from 105 dotnet cases over the **real** tables — including a 10 000-command
randomized invariant run — **90.92 %** line coverage of the backpack rule set, and 9 Unity
EditMode cases that place real pieces and hand a loadout to the battle contract.

Stage 06 built the **run** around it — still no battle simulation. `Domain/Run` holds the
`Wallet` (the only place bia changes), the phase enum `RunFlowState` with its explicit
`SceneId` mapping, the recorded command vocabulary `RunCommand`, the presentation event kinds,
the hashable `RunState`, and the typed outcomes; `Domain/Shop` holds the four-slot camp shop
(`ShopStock` / `ShopSlot`) and the weighted, floor-banded `ShopRoller` that reads its pool
from `shop_pool`. `Application/Run/RunSession` is the **only** command surface — `StartRun`,
`Purchase`, `Sell`, `RefreshShop`, `SetSlotLocked`, `OpenShop`, the Stage 05 bag commands
(now recorded), `StartFloor` (the departure gate with its `run.no_arcane_weapon` reason code
and the bag lock during Battle) and `ApplyBattleResult` (one settlement per floor, healing
`economy.heal_percent_on_clear` capped at max HP) — each validating completely before its
first mutation. `SceneFlow` drives Camp ↔ Battle and is the only place a session is created,
through the `IBattleSessionFactory` it was handed; `ReplayLog` rebuilds a whole run from its
seed plus the accepted commands plus one Stage 02b `BattleReplay` per floor, and asserts the
same `RunState` hash. Three boundaries are settled in
[ADR 0016](docs/adr/0016-run-settlement-and-shop-commit-boundaries.md). Evidence: 42 dotnet
cases over the real tables (a scripted five-floor run that ends at `Result`, a run that loses
the boss, and a replay of both), 2 architecture tests (no economy rule in a Presenter; the
battle reachable only through its factory), and 4 Unity EditMode cases over the **shipped**
`Resources/Config` tables.

Stage 07 built the **battle** — the thing Stages 05–06 kept deferring. `lib/Runtime/Simulation/Battle`
is an EcsLite world of ghosts, projectiles and drops driven one fixed 30 Hz tick at a time behind the
Stage 02b contract: `EcsBattleSimulation` + `EcsBattleSessionFactory`, fourteen components and fourteen
systems in the GDD's order (`WaveSpawn → PlayerMove → EnemySteer → AutoCast → ProjectileMove →
SpatialHash → Collision → DamageStatus → Death → Drop → Pickup → FloorEnd`), a uniform-grid spatial hash,
and `BattleDefIds` (a def id is its table row's 1-based position). It uses the **same** `BattleEventRing`
and `BattleFrameBuffer` the fake uses, so a Presentation sees one behaviour; it names no Unity type
(architecture test); it casts at most one spell per tick — ready, affordable, shortest base cooldown,
nearest ghost; and it owns **no clock**, so paused is “not stepped” and a stalled tab has its backlog
dropped and counted rather than replayed. Evidence: **28 new xUnit cases (413 total)**, every case of the
shared contract suite the fake passes, plus a rules suite (movement per tick, cooldown, MP, projectile
hit + despawn, one reward per kill, one floor end per death, the boss win, the zero-allocation tick);
a **bot harness** — `tools/sim.ps1 --seeds 200` plays the kiting bot and a greedy camp policy through
all five floors into `artifacts/balance.csv` (floor 1 200/200, floor 2 198/200, floor 3 91/198, floor 4
71/91, floor 5 **71/71**); and an **acceptance benchmark** of 300 ghosts + 500 projectiles measuring
**0 bytes allocated per tick** (avg `0.114 ms`, p95 `0.126 ms` per tick, Release, .NET 9, machine named
in the report). Three numbers the prototype wants are **not in `data/`** — elemental status magnitude, a
kill's `enemy.drop_value`, `constant.magnet_speed` — so none was invented in code: the status re-uses
the applying skill's own damage, a pickup collects instantly and pays `0` (which is why a run earns no
bia in battle and no build can grow after the first camp), and each gap names the column Stage 11 adds
([ADR 0017](docs/adr/0017-ecslite-battle-simulation.md)).

Stage 08 put that battle **on screen and in a browser**. `Assets/Main/Scenes/Battle.unity` is
authored through the Editor API like the other scenes; `Presentation/Battle/**` reads the battle
through `IBattleFrame` / `IBattleEventSource` and is **forbidden to name the simulation assembly**
(an EditMode test, not a convention). A fixed-step accumulator (`FixedTicker`, 30 Hz, capped
`MaxStepsPerFrame`) steps the session and copies the frame before anything can step again; the
view then interpolates `PreviousEntities → Entities` with the accumulator's leftover alpha, binds
pooled `SpriteRenderer` views through `BattleRenderBridge`, and maps `(x, y) → (x, 0, y)` with an
authored sprite sorting order in `BattleWorldMap`. The camera question the GDD left open is closed
by measurement — **orthographic** at 50°, near÷far scale 1.000 against perspective's 0.832
([ADR 0018](docs/adr/0018-battle-camera-projection.md)). Evidence: a **Release WebGL player in
Chrome** plays floor 1 to `Won / SurvivedAllWaves` — 1800 ticks, 0 dropped ticks, 0 views created
after the pool's 1793-view warm-up, 0 dropped events; 413 xUnit, 48 Unity EditMode (15 new
presentation cases), 4 PlayMode (scene start, 10 s of ticks, teardown, re-entry) and 4 Playwright
cases pass. See [`llmwiki/systems/battle-presentation.md`](llmwiki/systems/battle-presentation.md)
and the [report](docs/stages/08-report.md) — which also records a build trap worth knowing: a
size-profile build persists player settings, and the Release player it poisoned halted at boot with
`Uncaught undefined` ([gotcha](llmwiki/gotchas/webgl-size-profile-settings-residue.md)).

Stage 09 built the **camp** (`Assets/Main/Scenes/Camp.unity`, fifth scene) and the **จัดย่าม**
screen: `CampSceneDirector` publishes two documents to the browser (`__projectSurviveCamp`, the run and
the screen; `__projectSurviveCampGeometry`, every cell, chest slot, shelf and the figure as on-screen
rectangles) because a WebGL player cannot run Unity's test framework, and `CampPresenter` is the only
thing that calls Application — a drag's verdict is `RunSession.PreviewDrop`, a **dry run of the same
`BackpackEditor` predicates the accepted command asks**
([ADR 0019](docs/adr/0019-camp-screen-read-model-and-gesture-preview.md)). Since 09c the camp is a
*place*: a figure that walks with WASD under a following oblique camera, one shelf per shop slot
standing in the sala with its piece, price and lock pin, the เซียมซี drum, `E` at the station the
figure stands at, and every control still clickable without walking (the phone and browser path).
425 xUnit, 75 Unity EditMode and 8 Playwright cases pass; the 27 PlayMode cases are green class by
class, while a **bulk** PlayMode run still fails on ambient Input System log asserts (no failed
assertion — [`docs/testing/test-suites.md`](docs/testing/test-suites.md) §7). The browser flow runs
rent → place → rotate → merge → give back → restore against the real UI. See
[`llmwiki/systems/camp.md`](llmwiki/systems/camp.md) and the [report](docs/stages/09-report.md).


**Data is the only source of balance numbers** (AGENTS rule 5): `data/` →
`tools/gen.ps1` (Luban 5.1.0 + protoc 33.6) → generated C# + `.bytes` →
`IGameConfig`. See [`llmwiki/systems/data-schema.md`](llmwiki/systems/data-schema.md).

**Engine lock:** Unity **`6000.3.9f1`** (revision `7a9955a4f2fa`), Unity Personal
license. Locked in [`tools/versions.lock.json`](tools/versions.lock.json).

---

## Environment (verified 2026-10-13)

| Tool | Version |
| --- | --- |
| OS | Windows 11 `10.0.26300` x64 |
| Git / Git LFS | `2.55.0.windows.3` / run `tools/doctor.ps1` |
| PowerShell | Windows PowerShell `5.1.26100.9444`; PowerShell `7` (preview) |
| .NET SDK | `6.0.428`, `9.0.311` |
| Node.js / npm | `v25.6.1` / `11.9.0` |
| Unity Editor | `6000.3.9f1` + WebGL Build Support |
| Browsers | Chrome `154.0.8037.98`; Edge `145.0.3800.70`; **Firefox not installed** |
| Playwright | not installed (added Stage 03/14) |

Reference machine for all performance work: Intel Core i5-14400F, 32 GB RAM,
NVIDIA RTX 5060 Ti — see [ADR 0003](docs/adr/0003-performance-baseline-machine-and-browser.md).

---

## Commands

```powershell
# Environment + dependency health check (reliable exit code)
pwsh -NoProfile -File tools/doctor.ps1

# Clone setup (once, after cloning)
git lfs install

# The two suites (docs/testing/test-suites.md has the timings and the complete test -> suite mapping)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1                    # Fast: dotnet + Sim + data gates + Unity EditMode
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full         # + PlayMode + WebGL Development + Playwright
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full -Release # a gate: Release build (ADR 0003)
# Fast is the working loop and needs the Editor open for its EditMode step; Full needs the Editor
# CLOSED for its WebGL step, so the two runs together give the complete verdict.
# Both write artifacts/test-summary.md (PASS / FAIL / NOT RUN per step, with real seconds).

# Core alone: build + xUnit + Sim determinism + the data gates, with no Unity at all
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
pwsh -NoProfile -File tools/verify-generated.ps1

# Static data pipeline (one-time setup downloads Luban + protoc into tools/vendor/)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/setup-tools.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1            # data/ + proto/ -> generated
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
                                                                             # ^ game rules over the .bytes
                                                                             #   (generated tables live here, not in data/)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1   # regeneration is identical
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1     # broken data is rejected

# Focused runs
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter FullyQualifiedName~Xoshiro128
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 12345 --trace 5

# WebGL build + browser tests (the Editor must be closed; a build takes minutes)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1                      # Development
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Profile Release     # Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -SizeProfile optimize-size
                                                                                             # ^ a measured size configuration;
                                                                                             #   baseline | strip-high | optimize-size | minimal
node tools/serve-web.mjs --port 8080          # then, in tests/web: npx playwright test

# UI layout without a WebGL build (the process stage + Stage 09e): open the scenes, shoot them at
# the two sizes and fail when two panels overlap. Editor open, menu `ProjectSurvive/Tools/1. Capture UI`:
unity command menu --path "ProjectSurvive/Tools/1. Capture UI (Camp + Battle, 1920x1080 + 1280x720)"
#   -> artifacts/ui/{camp,battle}-{1920x1080,1280x720}.png, camp-closed-* and camp-tooltip-* (the
#      tooltip's draw order is proved in pixels: the same frame off vs on), + ui-report.md with
#      every rectangle AND every region's alpha (the opacity floor is a measured column)
# Scripted form (Editor closed): Unity.exe -batchmode -quit -projectPath UnityProject \
#   -executeMethod ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit
# Proof the check can fail: menu `ProjectSurvive/Tools/2. Capture UI self-test (expected to FAIL)`
#   -> ui-report-selftest.md with the deliberate collision listed.


# Thai text sharpness (Stage 09d): how big the letters really are, from the font and from the player
python tools/font-metrics.py --size 22                                    # 22 px metrics + comparison PNGs
node tests/web/scripts/collect-geometry.mjs http://127.0.0.1:8080/ 1920 1080 \
  artifacts/stage09d/camp-geometry-1920x1080.json 1                       # the layout behind a shot
python tools/screen-text-metrics.py --image artifacts/stage09d/screens/web-camp-1920x1080-dsf1.png \
  --geometry artifacts/stage09d/camp-geometry-1920x1080.json --region openBag --region shelfLock1
SCREENS=1 npx playwright test screens.spec.ts   # in tests/web: the Stage 09d screenshots (opt-in,
                                                # 13 player boots; not a gate)

# Coverage of the bag rules (Stage 05 acceptance: >= 85 % of Domain/Inventory + Footprint)
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" --collect:"XPlat Code Coverage" `
  --settings "lib/Tests~/coverage-domain.runsettings" --results-directory artifacts/coverage-05
node tools/coverage-report.mjs artifacts/coverage-05/*/coverage.cobertura.xml "Inventory/" "Config/Footprint.cs"

# Size: what does a build weigh, and which assembly is inside the .wasm
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label mine   # raw / gzip -9 / Brotli q11
node tools/measure-il2cpp-size.mjs --artifacts UnityProject/Library/Bee/artifacts/WebGL
                                                                             # per assembly, from IL2CPP's C++
```

Size numbers only come from `-SizeProfile` builds run through this script: a build started in the
Editor's Build Settings window applies platform profile defaults over the project's player
settings. See [`docs/build/size-baseline.md`](docs/build/size-baseline.md) and
[ADR 0011](docs/adr/0011-webgl-payload-budget-and-size-measurement.md).

Exit codes are part of the contract: `0` = all required checks passed,
`1` = a required check failed, `2` = the tool itself could not run.

New commands are added as their stage lands (`tools/build-web.ps1` /
`serve-web.mjs` at Stage 03; `measure-web-size.mjs` / `measure-il2cpp-size.mjs`
at Stage 03b).

---

## Repository map

```text
AGENTS.md              Master Prompt / permanent rules
knowledge.md           ← you are here
proto/                 external contracts (app.proto) — Stage 02
lib/                   local UPM package (com.projectsurvive.core): all game rules
  Runtime/{Shared,Domain,Application,Simulation,Infrastructure,Contracts,Generated}
                       one .asmdef per layer, all noEngineReferences: true
  third_party/ecslite/ vendored Leopotam EcsLite (MIT-Red) at a pinned revision
    Generated/{Luban,Proto}/   machine output (Luban readers, protoc App.cs) - never edited
  third_party/luban-runtime/ vendored Luban C# runtime used by the generated readers
  Tests~/              dotnet harness: one csproj per layer + xUnit Core.Tests
                       + Sim console + Data.Validator + fixtures/ (broken data)
data/                  Luban tables, Defines, localization (th + en in one table)
docs/                  gdd/ architecture/ adr/ stages/ testing/ build/
llmwiki/               index.md glossary.md systems/ (data-schema) gotchas/
tools/                 versions.lock.json doctor.ps1 setup-tools.ps1 gen.ps1
                       validate-data.ps1 verify-generated.ps1 check-fixtures.ps1 test.ps1
                       build-web.ps1 serve-web.mjs measure-web-size.mjs measure-il2cpp-size.mjs
                       vendor/ (downloaded CLI toolchain, never committed)
tests/web/             Playwright specs — Stage 03+
artifacts/             untracked logs/reports/screenshots
UnityProject/          Unity shell — Stage 03
```

---

## Architecture in one breath

`Shared ← Domain ← Application ← Presentation`. `Simulation` uses
`Shared/Domain/EcsLite`. `Infrastructure` implements Application ports.
**Domain / Application / Simulation never touch `UnityEngine`; Domain and
Simulation never touch R3, VContainer, or serializers.** Enforced by asmdef.

Battle runs at a **fixed 30 Hz**, randomness only through `IRandomSource` with
separate **shop / battle / visual** streams. Every mutating command is **atomic**
and returns a **typed result**.

Full detail: [`docs/architecture/overview.md`](docs/architecture/overview.md).

---

## Document links

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | Master Prompt + permanent rules for every stage |
| [`docs/gdd/prototype-v0.3.md`](docs/gdd/prototype-v0.3.md) | **Canonical GDD** (v0.3 combined) |
| [`docs/architecture/overview.md`](docs/architecture/overview.md) | Layers, data lifetimes, command/event flow |
| [`docs/architecture/dependencies.md`](docs/architecture/dependencies.md) | Dependency matrix (Verified / Proposed / Blocked) |
| [`tools/versions.lock.json`](tools/versions.lock.json) | Engine + tool version lock |
| [`docs/adr/`](docs/adr/) | Architecture decision records |
| [`llmwiki/systems/data-schema.md`](llmwiki/systems/data-schema.md) | **Static data schema + pipeline commands** |
| [`llmwiki/systems/battle-contract.md`](llmwiki/systems/battle-contract.md) | **Battle contract v1 — the interface between the battle and the rest** |
| [`llmwiki/systems/backpack.md`](llmwiki/systems/backpack.md) | **Bag/board rules, editing commands, combos, bag → battle loadout** |
| [`llmwiki/systems/run-shop.md`](llmwiki/systems/run-shop.md) | **Run phase machine, wallet, camp shop, settlement, `ReplayLog`** |
| [`llmwiki/index.md`](llmwiki/index.md) | Wiki index |
| [`llmwiki/glossary.md`](llmwiki/glossary.md) | Code names ↔ Thai terms |
| [`docs/stages/`](docs/stages/) | Per-stage reports |
| [`docs/testing/`](docs/testing/) | Test strategy (fills in from Stage 01) |
| [`docs/build/`](docs/build/) | Build instructions (fills in from Stage 03) |

---

## Known gaps carried forward

- `UI.MVP Lite` package has **no repository** → project-owned thin MVP for now
  ([ADR 0002](docs/adr/0002-thin-mvp-ui-approach.md)).
- Versions of VContainer, R3, UniTask and MessagePack "that work with Unity 6.3"
  are **proposed, not verified** — confirmed in Stage 03. (EcsLite is settled:
  vendored source at a pinned revision, Stage 01. Luban 5.1.0 and protoc 33.6
  are settled: pinned, installed by SHA-256, and exercised end to end, Stage 02.)
- Generated code and the `.bytes` payloads are only ever **compiled outside Unity**
  so far. That `.bytes` in `Resources/Config` load in a real Web build, and that
  the asmdef graph resolves as the csprojs do, is proven at Stage 03.
- The data schema has **no versioned migration story** yet: `SchemaVersion` in
  `ConfigBuildMetadata.g.cs` is bumped by hand in `tools/gen.ps1`, and no save
  format reads these tables yet (Stage 12).
- Nothing in `lib/` has been compiled by Unity yet. The `dotnet` harness mirrors
  the Unity compiler surface (C# 9 / netstandard2.1), but asmdef resolution
  itself is only proven in Stage 03.
- IL2CPP stripping/AOT behaviour of MessagePack, Protobuf, R3, VContainer on Web
  is **unproven** — Stage 03.
- IndexedDB flush method on Unity 6.3 — Stage 12.
- Firefox not installed → cross-browser runs are NOT RUN until it is.
- **Charm passives and offering consumable effects have no columns yet.**
  `item` records a charm's tags but not its own buff magnitude, and an offering's
  heal/bia/uses likewise. Deliberate at Stage 04
  ([ADR 0010](docs/adr/0010-item-local-effects-deferred.md)): Stages 05–07 have landed and the
  item-local effects are **still** unmodelled, because neither the bag rules, the economy nor the
  battle needed a magnitude a designer had not written. **The numbers must land in `data/` when a
  consumer does — never in code** (rule 5).
- **A piece's weight and price do not depend on its tier.** `item`/`bag` carry one
  `weight` and one `price`, so merging to tier 3 makes a piece stronger without making it
  heavier or more valuable ([ADR 0013](docs/adr/0013-weight-speed-penalty-curve.md),
  [ADR 0014](docs/adr/0014-backpack-editing-undo-and-placement-ownership.md)). If either
  should scale, the column has to land in `data/` first — never as a code constant.
- **`tools/validate-data.ps1`'s default `-DataDir` is `data/`, which holds no `.bytes`.**
  Always pass `-DataDir "lib/Tests~/data"` (the pipeline's generated tables). Running it
  bare reports twelve `config.table_missing` findings that are not data bugs.
- **The only measured performance is the headless .NET battle benchmark** (Stage 07: 300 ghosts +
  500 projectiles, Release, `0` bytes allocated per tick, avg `0.114 ms` / p95 `0.126 ms` per 30 Hz
  tick on the machine quoted in [`docs/stages/07-report.md`](docs/stages/07-report.md)). It is **not**
  a Web number: the player is IL2CPP/WASM and single-threaded, so every frame-time and size budget
  elsewhere in this document is still a target until a **build** is measured
  ([ADR 0011](docs/adr/0011-webgl-payload-budget-and-size-measurement.md)).
- **The Unity composition root still binds nothing run-level.** `RootLifetimeScope` is
  scene-scoped and registers only the shell's services (config, clock, localization, UI
  state, presenters); `RunSession`/`SceneFlow`/`EcsBattleSessionFactory` are constructed by whoever
  drives the run today (the bot harness and the EditMode tests) and Stages 08–10 will add the scope
  that survives scene loads together with the `IBattleSessionFactory` binding. Deliberate at
  Stage 06 ([ADR 0016](docs/adr/0016-run-settlement-and-shop-commit-boundaries.md)): the
  fake must not become the shipped battle by accident in a registration nobody reads — and as of
  Stage 07 there is a second implementation to choose between, which is exactly why the binding is
  a decision and not a default.
