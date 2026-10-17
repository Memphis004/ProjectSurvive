# Stage 10t — generic buffers in `Shared`, one builder per scene, one name per global

## 1. Summary

The groundwork for a future `main` / `template` split, done without changing a rule: the two pieces
of battle machinery that never needed to know what an event or an entity is now live in
`Shared` as `EventRing<T>` and `PrevCurBuffer<T>`, and `BattleEventRing` / `BattleFrameBuffer` became
the thin battle-typed adapters over them ([ADR 0024](../adr/0024-shared-generic-buffers.md)).
`ProjectSetup` lost its scene creation — 2 251 lines became 1 029 plus six per-scene builders — so a
new scene no longer means editing one huge file. The three page globals are now named in exactly one
place per side (`WebGlBridge`'s constants, `tests/web/globals.mjs`), with an EditMode test that fails
if the `.jslib` hard-codes a name or the two files drift. Every existing test still passes, and all six
scenes regenerate to the same scene.

## 2. Acceptance, item by item

| # | Requirement | Result |
| --- | --- | --- |
| 1 | `EventRing<T>` in `Shared`, `BattleEventRing` composes it | **PASS** — [`lib/Runtime/Shared/EventRing.cs`](../../lib/Runtime/Shared/EventRing.cs) (147 lines: capacity, strictly increasing `Seq`, per-reader cursor, honest `dropped`, no allocation); `BattleEventRing` is 85 lines (was 130) and keeps the contract's semantics. Nine cases in [`EventRingTests`](../../lib/Tests~/Core.Tests/Shared/EventRingTests.cs), including `Write_and_read_do_not_allocate` |
| 2 | `PrevCurBuffer<T>` in `Shared`, `BattleFrameBuffer` composes it (ADR 0012) | **PASS** — [`PrevCurBuffer.cs`](../../lib/Runtime/Shared/PrevCurBuffer.cs) owns the two slots and the swap; `BattleFrameBuffer` is 187 lines (was 234) and keeps the cap, the HUD and the cooldown list. Seven cases in [`PrevCurBufferTests`](../../lib/Tests~/Core.Tests/Shared/PrevCurBufferTests.cs), incl. `Begin_frame_publish_and_read_do_not_allocate`; the 10 existing `BattleFrameBufferTests` and the "one `IBattleFrame` implementation" reflection test are unchanged and green |
| 3 | `ProjectSetup` split into one builder per scene | **PASS** — `BootSceneBuilder` 50, `TitleSceneBuilder` 126, `ThaiTextSpikeSceneBuilder` 86, `CampSceneBuilder` 471, `BattleSceneBuilder` 465, `ResultSceneBuilder` 140 lines; `ProjectSetup` 2 251 → 1 029 keeps URP/player settings, font baking and the shared UI/entity helpers. The menu paths are unchanged (`Stage 03/2`, `03/3`, `03/4`, `08/3`, `09/5`, `10/6`, `10d/7`), so every document that names one still works |
| 4 | Each browser global named in one place per side | **PASS** — `WebGlBridge.SmokeReportGlobal/SceneGlobal/ThaiSpikeGlobal`; the `.jslib` functions now take the name as an argument; `tests/web/globals.mjs` is the specs' only copy (all six specs and `probe-boot.mjs` import it). `WebGlBridgeTests` 3/3 asserts the published strings, that the plugin carries no global name, and that the JS module agrees |
| 5 | New ADR for the Shared generic buffers | **PASS** — [ADR 0024](../adr/0024-shared-generic-buffers.md), citing ADR 0007 §5 and ADR 0012, with the three alternatives (copy per game, move battle types into `Shared`, make the mechanics generic) and the two things left game-typed |
| 6 | `AGENTS.md`: a commit rule for work that names no game type | **PASS** — rule 14: such commits start with `generic:` so they can be cherry-picked onto the template branch |
| 7 | (DoD) tests for what changed, run for real | **PASS** — `dotnet test` **481 → 497** (16 new), EditMode **144 → 147** (3 new), PlayMode **46**, Playwright **10**. Both counts measured before and after on this machine: `artifacts/stage10t/full-before-*.log` / `full-after-*.log` |
| 8 | (DoD) PlayMode per class | **PASS** — 46/46 in eight class runs, [`artifacts/stage10t/playmode-per-class.md`](../../artifacts/stage10t/playmode-per-class.md). The **bulk** payload is 21/46 **before and after** the change, with the same 25 ambient-log failures (§7 of the test-suites note), so the suite as a suite is still a FAIL and not a delta of this stage |
| 9 | (gate) WebGL build + Playwright | **PASS** — Development build 244.6 s; the harness's five specs **10 passed** in 2.3 min ([`playwright-after-5specs.log`](../../artifacts/stage10t/playwright-after-5specs.log)). The harness's own Full attempt failed one camp spec on the documented 15 s poll timeout (a different one on each attempt; each passes alone in 13.5 s / 7.9 s) — see §3 |
| 10 | Scenes regenerate to the same scene | **PASS** — all six compared object-by-object against a snapshot taken **before** the split: same counts, same serialized values (Boot 9, Title 40, ThaiTextSpike 33, Battle 249, Camp 280, Result 65 objects), [`scene-canon.py`](../../artifacts/stage10t/scene-canon.py). Byte equality only holds when two runs happen in the same Editor session — Unity reassigns local fileIDs and re-orders the documents on every save |

## 3. Not tested, and risks

- **The six `.unity` files in this commit are re-serialisations**, not a semantic change of the stage:
  same objects, new fileIDs and document order (~6 400 lines of churn). Regenerating also restored two
  things the checked-in copies had missed: `Camp.unity` gains the merge chooser's colour fields the
  builder has set since Stage 09g, and `ThaiTextSpike.unity` gains `InputSystemUIInputModule` and
  `RootLifetimeScope._runSeed` instead of a legacy `StandaloneInputModule` and a scope with no seed.
  Both are the builder being right and the file being stale, but they *are* a visible change on the
  Camp screen's chooser, measured only by the 4/4 chooser PlayMode class and the browser's camp specs.
- **The bulk PlayMode suite is still a FAIL as a suite** (21/46, Input System `IndexOutOfRange` and R3
  `Cached unprocessed value` ambient log asserts). It failed identically with the pre-change code and
  with the **HEAD** scenes restored, which is what rules this stage out as the cause; the per-class runs
  are the evidence. Not fixed here.
- **The browser suite is flaky under load.** Two harness runs failed a different camp spec on a 15 s
  poll; both pass alone, and the identical five specs pass 10/10 when run directly. The machine was
  slower than in Stage 10d during this stage (Development build 244.6 s vs 70.2 s), which is the likely
  reason.
- **Nothing visual was re-reviewed.** This stage changed no pixel on purpose; the Camp chooser colours
  above are the one exception and were not looked at. No screenshot is offered as evidence.
- Release build, and the Thai spike scene at runtime, are unchanged and untested (Stage 15's gate).

## 4. Commands run

```powershell
# baseline, before the first edit
powershell -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full        # Editor open
unity close UnityProject; tools/test.ps1 -Suite Full                      # Editor closed (web)
# after the change
powershell -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full        # Editor open
unity close UnityProject; tools/test.ps1 -Suite Full                      # Editor closed (web)
unity command run_tests --mode playmode --async_tests --filter CampBagDragPlayModeTests   # ×8 classes
unity command menu --path "ProjectSurvive/Stage 08/3. Create Battle scene"                # ×6 scenes
python artifacts/stage10t/scene-canon.py artifacts/stage10t/scenes-baseline/Camp.unity UnityProject/Assets/Main/Scenes/Camp.unity
node tools/serve-web.mjs --port 8080 & ; cd tests/web; npx playwright test smoke.spec.ts title.spec.ts battle.spec.ts camp.spec.ts result.spec.ts
```
