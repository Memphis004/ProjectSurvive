# Stage 10 — the full loop (vertical slice)

## 1. Summary

The game now plays end to end: **Boot → Title → Camp → Battle (floor 1) → Camp → … → floor 5 →
Result**, and back out to the title. Nothing new is simulated — Stage 07's battle and Stage 09's camp
are untouched — what the stage adds is *where a run lives*: three lifetimes with three scopes (the
**App** scope that Boot moves into `DontDestroyOnLoad` and that every scene scope becomes a child of,
the **Run** scope that `RunScopeOwner` creates once and disposes on the way back to the title, and the
scene's own scope), so walking between the camp and the arena no longer rebuilds the wallet, the bag
and the shop — which is the bug the acceptance names (\"ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene\"). The
crossing itself is one place, `SceneLoader`: one transition at a time behind a fade, typed refusals
instead of a half-applied move, and \"arriving at the title disposes the run scope\" as a property of
the transition rather than a habit of each screen. A settled floor writes `BattleResult.FinalHp` back
through the run (the 15 % clear-heal belongs to the run and is not applied to a loss), the summary
screen reports `RunSession.Summary()` with **Restart** and **กลับ Title**, and the dev menu speaks only
`BattleCommand` behind two gates so a release player cannot cheat. The 20-round-trip test this stage
was asked for found a **real leak** — the fade overlay's fallback `EventSystem` — and it is fixed and
measured.

## 2. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| 1 | Boot → Title → Camp → Battle ชั้น 1 → Camp → … → ชั้น 5 → Result | **PASS** — `SceneFlowPlayModeTests` drives the real scenes (Boot's shell, the camp's own departure, the real loader, the real EcsLite session); `ResultScenePlayModeTests.A_run_can_be_won_floor_by_floor_and_ends_on_a_victory_summary` plays five floors to the victory screen. Evidence: [`artifacts/stage10/result-victory.png`](../../artifacts/stage10/result-victory.png), `…-defeat.png` |
| 2 | Scope App / Run / Scene ชัด: `RunSession` ไม่ถูกสร้างใหม่ทุกครั้งที่กลับ Camp; กลับ Title แล้ว dispose Run scope | **PASS** — `RunScopeLifetimeTests` (3: the run is created once and replaced only by its owner's disposal; a run after a disposal is a new object; a scene scope inherits the App scope's run), `SceneFlowPlayModeTests.The_run_carries_its_purse_bag_and_hp_into_the_battle_scene` (the same `RunSession` object, same seed, same purse/chest/HP across the handover), `Leaving_the_battle_scene_disposes_its_session_and_its_run_scope` (title → 0 run scopes, 0 camp scopes, 0 arena scopes) |
| 3 | ใช้ Battle scene เดิม เปลี่ยน `FloorDefinition`; ล้าง temporary combat state แต่คง HP, เบี้ย, ย่าม | **PASS** — the arena plays `RunSession.PendingSetup` (the floor the run is pointing at) and rebuilds its world per visit: `BattlePlayModeTests.Re_entering_the_battle_scene_starts_a_fresh_floor` + `Leaving_the_battle_scene_disposes_the_world_and_the_subscriptions`, and the 20-loop asserts one scene scope, one run scope and one session per visit |
| 4 | Result: Victory และ Defeat พร้อมสรุปชั้นที่ไปถึงและผีที่ปราบ; Restart สร้าง Run ใหม่ ไม่คง state จากรอบก่อน | **PASS** — `ResultScreenTests` (7) + `ResultScenePlayModeTests.A_lost_floor_lands_on_the_summary_screen_and_restart_starts_a_new_run` (floor 1, `Economy.StartBia`, empty bag, `GhostsDefeated` 0) and `Leaving_the_summary_for_the_title_leaves_no_run_behind` |
| 5 | SceneLoader รับ transition ทีละรายการ พร้อม fade และ loading/error recovery ขั้นต่ำ | **PASS** — `The_scene_loader_refuses_a_second_transition_and_a_scene_that_is_not_built`: the refusal is typed (`transition.in_progress`, `transition.scene_missing:99`), nothing is hidden, the screen stays put, and the accepted transition still lands; the fade is one `DontDestroyOnLoad` overlay for the whole game |
| 6 | DevMenu (dev build เท่านั้น) อมตะ / เพิ่มเบี้ย / ข้ามชั้น / spawn ผี, ตั้งชื่อ test-only; commands ผ่าน `BattleCommand` เท่านั้น; `DevCheatsAllowed=false` ใน release (มีเทสต์ยืนยัน) | **PASS** — `DevCheatTests` (8) asserts the gate and every refusal through the real session; the menu is authored **inactive** and named `DEV MENU (test only)` on its own canvas; its whole vocabulary is `BattleCommand` through `IBattleSession.Submit`. Evidence: [`artifacts/stage10/battle-devmenu.png`](../../artifacts/stage10/battle-devmenu.png) |
| 7 | dispose `IBattleSession` ทุกครั้งที่ออกจาก SceneBattle; เทสต์ 20 loop ต้องไม่มี session ค้าง | **PASS** — `Leaving_the_battle_scene_disposes_its_session_and_its_run_scope` and the 20-loop's per-iteration `runner.Session Is.Null` after leaving the arena |
| 8 | เล่นตั้งแต่เริ่มจนชนะและแพ้ได้ | **PASS** — both outcomes are produced by the real scene flow (above), and the *player* half of the loop is exercised in a browser: after a won floor the built player hands the run back to the camp (`__projectSurviveScene` → `Camp`, camp report `floor` 2, `hp` = the arena's `finalHp`, `canDepart` true) — `artifacts/stage10/loop-camp-player.png` |
| 9 | ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene | **PASS** — the PlayMode handover test compares the same run object's purse/chest/HP/bag/seed across the crossing; in the built player the camp's own report shows the run past the floor the arena played |
| 10 | Camp ↔ Battle วน 20 รอบแล้วไม่มี presenter หรือ ECS world ซ้ำ และ memory ไม่โตต่อเนื่อง | **PASS** — `Twenty_camp_and_battle_visits_leave_nothing_behind_and_do_not_grow_the_heap`: each of the 20 iterations asserts exactly one scene scope and one run scope, 20 sessions created and every one disposed; managed growth over 18 round trips **729 088 B** (budget 3 MiB) and allocated **1 277 912 B** (budget 24 MiB). Before the `SceneLoader` repair the same test measured **5 660 672 B** — linear, ~314 KB per iteration. Numbers, both runs and the cause: [`artifacts/stage10/loop-memory.md`](../../artifacts/stage10/loop-memory.md) |
| 11 | Title ไม่มี Run scope เก่าค้าง | **PASS** — `SceneFlowHarness.Count<RunLifetimeScope>() == 0` at the title in two separate tests (`RunScopeOwner.HasRun` false as well) |
| 12 | HP ข้ามชั้นผ่าน `BattleSetup.CurrentHp` → `BattleResult.FinalHp`; การฟื้น 15 % เป็นหน้าที่ Run ไม่ใช่ Battle | **PASS** — `A_settled_floor_writes_its_result_back_into_the_run` asserts `run.Hp == result.FinalHp`, `settlement.HealedHp == 0` and `RewardBia == 0` on a **lost** floor; the clear path's heal is asserted in the EditMode settlement tests |
| 13 | (DoD) automated tests for the changed rules, run for real | **PASS** — 29 new Unity test methods: EditMode 21 (`DevCheatTests` 8, `ResultScreenTests` 7, `RunScopeLifetimeTests` 3, `ResultScreenLayoutTests` 3) taking the assembly 94 → **115**, and PlayMode 8 (`ResultScenePlayModeTests` 3, `SceneFlowPlayModeTests` 5) taking it 33 → **40** as the runner reports it (five `ShellPlayModeTests` methods are declared, four appear in a run — §5). `-Suite Fast` **PASS 10 / FAIL 0 / NOT RUN 3** in 76.0 s (dotnet **466**, EditMode **115/115**) |
| 14 | (gate) WebGL build + Playwright | **PASS** — `artifacts/stage10/full-suite-editor-closed.log`: `web: WebGL build (Development)` **PASS** 195.6 s, `web: Playwright` **PASS** 8/8 (120.7 s); the whole suite **PASS 11 | FAIL 0 | NOT RUN 2** in 390.2 s, and the browser suite was re-run after the spec change (`npx playwright test` → 8 passed, 13 skipped) |
| 15 | (DoD) ภาพหน้าจอจากเกมจริง + layout ตรวจจาก capture | **PASS** — screenshots of the real game in `artifacts/stage10/` (victory, defeat, the dev menu, the camp after a won floor in the player); the summary screen's own rectangles are judged by `ResultScreenLayoutTests` with the project's `UiLayoutCheck` (pairwise disjoint, inside the column, ≥ 22 px in ≥ 32 px boxes, reading order) — [`artifacts/stage10/result-layout.md`](../../artifacts/stage10/result-layout.md) |

## 3. Commands run

```pwsh
pwsh -NoProfile -File tools/test.ps1 -Suite Fast            # 3 runs: FAIL-free; closing run PASS 10 | FAIL 0 | NOT RUN 3, 76.0 s
pwsh -NoProfile -File tools/test.ps1 -Suite Full            # Editor open  -> PASS 10 | FAIL 1 | NOT RUN 2, 313.2 s (the FAIL is the known PlayMode flake, §5)
pwsh -NoProfile -File tools/test.ps1 -Suite Full            # Editor closed -> PASS 11 | FAIL 0 | NOT RUN 2, 390.2 s (WebGL + Playwright)
unity command run_tests --mode editor   --filter ResultScreenLayoutTests      # 3/3
unity command run_tests --mode playmode --async_tests --filter Camp           # 25/25 (three camp classes + the 20-loop)
unity command run_tests --mode playmode --async_tests --filter SceneFlowPlayModeTests   # 5/5
unity command run_tests --mode playmode --async_tests                          # 20/40, all 20 = ambient log asserts (§5)
node tools/serve-web.mjs --port 8080 ; npx playwright test                     # 8 passed / 13 skipped (screens)
unity command screenshot --view game                                           # the Stage 10 captures
```

## 4. Evidence

- `artifacts/stage10/full-suite-editor-open.log` — the open-Editor Full run, **PASS 10 | FAIL 1 |
  NOT RUN 2** in 313.2 s (`RESULT: FAILED`, and the one FAIL is the PlayMode step: EditMode
  **115/115**, PlayMode **20/40** with the ambient-log flake; both web steps NOT RUN by design).
- `artifacts/stage10/full-suite-editor-closed.log` — the closed-Editor Full run: WebGL **PASS**,
  Playwright **8 passed in Chrome**, `RESULT: OK (Full suite)`.
- `artifacts/stage10/fast-after-fix.log`, `…/fast-after-layout-tests.log` — the post-repair Fast runs.
- `artifacts/stage10/loop-memory.md` — the 20-loop's per-iteration numbers before and after the fix,
  and what the leak was.
- `artifacts/stage10/playmode-bulk-status.json`, `…-after-fix.json`,
  `artifacts/stage10/playmode-after-fix.md` — the full PlayMode payloads and the per-class runs.
- `artifacts/stage10/result-layout.md`, `result-victory.png`, `result-defeat.png`,
  `battle-devmenu.png`, `loop-camp-player.png` — the summary screen measured and shot (three from
  Editor play mode, one from the WebGL player).
- `llmwiki/systems/loop-and-scopes.md` — the systems note: the three scopes, the floor loop, the
  summary screen, the dev menu, and the facts that cost time (the `Game.Result` namespace trap, the
  generated-scene churn, `DevCheats.Allowed` being process-wide).

## 5. Not tested, and risks

> **Update (2026-10-16, the same day).** The two gaps this section opened have been closed by a
> follow-up the user asked for, recorded in §7 below: the summary screen is now rendered by
> `ProjectSurvive/Tools/1. Capture UI` at both GDD sizes and reached in a browser by
> `tests/web/result.spec.ts`, and the "the dev menu is not reachable in a browser" half of the sentence
> below was **wrong**: the menu is a Development-build feature and the suite's build *is* Development
> (`DevCheats.Allows(isDebugBuild: true, isEditor: false)`), so its controls are on screen and a browser
> test can press them. The paragraphs are left as they were written, with the correction here rather
> than edited into them.

- **The Result screen is never reached in a browser.** The suite's floor 1 is *won* by a standing
  player, so the run continues to the camp; reaching the summary in the player would need five floors
  (or a loss the browser cannot ask for — surrendering is not an input the suite has). IL2CPP stripping
  is exactly the class of bug a Mono test cannot see, so "the summary screen's DI survives a real
  build" is **NOT RUN**, and the following full-run check is the cheapest way to close it: play a run
  to a loss in the browser (`?scene=Battle` + a dev-only surrender would be enough if the dev menu were
  reachable in a browser, which it is not by design).
- **The bulk PlayMode suite still fails on ambient log asserts** (20/40; twelve `IndexOutOfRangeException`
  inside `InputSystem.InputManagerStateMonitors` during `InputSystemUIInputModule.OnEnable` + eight R3
  `Cached unprocessed value` messages, **no failed assertion of ours**, unchanged by this stage and
  recorded in `docs/testing/test-suites.md` §7 and `llmwiki/stage-log.md`). The classes this stage owns
  were run on their own and are green. One further oddity of the payload is recorded rather than
  explained away: it lists four of `ShellPlayModeTests`' five `[UnityTest]`s, and the missing one is not
  reported as a failure either.
- **`data: broken fixtures rejected` is now the slowest Fast step** (55–58 s of the ~76 s total), which
  is worth fixing in Stage 14 if the working loop starts to feel slow.
- **Restart disposes and rebuilds the run scope**, so a restart is not free of allocation; the 20-loop
  measures the disposal path, not 20 restarts.
- **The screenshots are Editor play-mode captures at the Game view's size** (plus one real player shot);
  the summary screen's layout rules are asserted at the canvas reference resolution only — other aspect
  ratios are not covered, the same gap Stage 09e recorded.
- **`Boot.unity` carries two of this stage's own lines**: `Result` added to the published URL scene
  allow-list (`ProjectSetup.UrlSceneAllowList`, refreshed by the scene menu item) and `_runSeed`
  serialized onto the Boot `RootLifetimeScope` because the field is new. `ThaiTextSpike` was already in
  the tracked file.
- Unrelated working-tree file left untouched and **not** part of this stage's commit:
  `UnityProject/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
  (a Unity 6 re-serialisation, as the 09f report recorded).
- **No ADR.** The stage had one real choice — where a run lives — and AGENTS rule 3 and the GDD's own
  item 2 name the answer (the three scopes the architecture doc already describes as App / Run / Scene),
  so the decision is recorded in `llmwiki/systems/loop-and-scopes.md` rather than restated as an ADR.

## 7. Follow-up (2026-10-16, same day): the last leg of the loop, twice over

Asked for after the stage closed, in three steps: **(a)** render the summary screen with the Editor
capture tool the way Camp and Battle are rendered, so its look is judged from tool output; **(b)** add
a browser check that reaches the Result screen and asserts its summary, so the player build covers the
last leg of the loop; **(c)** give the summary column the camp panels' own background — asked for once
(a) had judged the bare column legible but plain (row 9). All three are follow-ups to this stage, not
a new one — and the one rule that did change (row 8) was not redesigned here: the browser check found a
dev command whose defeat did not match the defeat the rules produce, and the command was corrected to
match them.

| # | Item | Verdict |
| --- | --- | --- |
| 1 | `ProjectSurvive/Tools/1. Capture UI` renders Camp, Battle **and Result** at 1920×1080 and 1280×720, measures the summary screen and fails on an overlap or an escaped region | **PASS** — 10 images, **1 098 ms** on 2026-10-16 and **1 265 ms** when re-shot from the final scene on 2026-10-17, 0 failures in both; the summary screen measures 8 regions at each size (its 1200×1000 column, five lines, two 400×76 buttons), 0 overlaps, every region inside the canvas — `artifacts/ui/result-{1920x1080,1280x720}.png` + `ui-report.md` / `.json` |
| 2 | The summary screen publishes what it shows and where its two controls are (`__projectSurviveResult`) | **PASS** — `ResultReport.Publish` is called by `ResultSceneDirector` right after `Presenter.Begin()`, so the document carries the strings the player reads; `ResultReportTests` (4, Fast) asserts them against the shipped tables for both endings, and `ResultScenePlayModeTests` asserts the same document where the real scene was reached |
| 3 | The dev menu publishes where its controls are (`__projectSurviveDevMenu`), and a build that hides it publishes nothing | **PASS** — `DevMenuView.Update` publishes on show and on a canvas resize; `DevMenuReportTests` (2, Fast) asserts every named control is wired in the shipped scene, that the panel is authored hidden, and that a hidden panel publishes an empty document. This is the missing piece the old "the dev menu is not reachable in a browser" assumed away: it *is* reachable in a Development build by design, which is what the suite builds |
| 4 | A browser check reaches the Result screen and asserts its summary | **PASS** — `tests/web/result.spec.ts` now runs inside the harness's own web step (`tools/test.ps1` lists it beside smoke/battle/camp) and the closing run reports `web: Playwright (smoke + battle + camp + result)` **9 passed in Chrome**, 140.0 s — `artifacts/stage10/full-followup-editor-closed-web-final.log`, `artifacts/dotnet/playwright.log`. In the player: the dev menu's `loseNow` ends floor 1 (`phase` Lost, `endReason` PlayerDied, `finalHp` 0), the arena hands over, and `__projectSurviveResult` reads `outcome` Defeat, `floor` 1/5, `floorsCleared` 0, `hp` 0 of `maxHp` 100, `kills` = the arena's own `kills`, and the five localized lines the player reads — `พ่ายแพ้`, `ไปถึงชั้น 1/5`, `ปราบผี 0 ตน`, `เบี้ยคงเหลือ 6`, `HP คงเหลือ 0/100` — with both controls named and inside the canvas (restart 480,248; title 480,195 in 960×600). Pressing **Restart** brings the camp back with a run that is new: `flow` Camp, `floor` 1, `hp` 100 = `maxHp`, `bia` 15, `canDepart` |
| 5 | (DoD) automated tests for the changed rules, run for real | **PASS** — 6 new Unity test methods (`ResultReportTests` 4 + `DevMenuReportTests` 2) take the EditMode assembly 115 → **119**; `-Suite Fast` **PASS 10 / FAIL 0 / NOT RUN 3**, 75.7 s, EditMode **119/119**, dotnet **466** (`artifacts/stage10/fast-followup.log`); `ResultScenePlayModeTests` **3/3** and `BattlePlayModeTests` **4/4** with the new assertions. After the rule fix in row 8 the closing runs are `artifacts/stage10/full-editor-open-result.log` (Editor open: EditMode **119/119**, PlayMode per class green — `playmode-per-class-followup.md`) and `artifacts/stage10/full-followup-editor-closed-web-final.log` (Editor closed: **PASS 11 | FAIL 0 | NOT RUN 2**, 245.9 s, WebGL **PASS** + Playwright **9 passed**), with dotnet **466 → 468** for the two contract cases row 8 adds |
| 6 | (DoD) docs and commands | **PASS** — `knowledge.md`, `docs/testing/test-suites.md` (the tool's command and coverage, the browser spec table and the suite's step name), `llmwiki/systems/loop-and-scopes.md` (the two new documents), `llmwiki/systems/battle-contract.md` (the `DevLoseNow` rule), `llmwiki/systems/camp.md`, `llmwiki/stage-log.md`, and this section |
| 7 | The summary screen's *look* is judged from the capture tool's output, not from play-mode shots | **PASS** — the 10 images are re-shot from the shipped scene at **00:29 on 2026-10-17** (10 shots, **PASS**, 0 failures, 0 overlaps) and the tool's measured rectangles are cross-checked against a play-mode capture of the same size: the white-text bands in `artifacts/stage10/result-victory.png` (1280×720) sit inside the label rects the report measures — headline 592–651 in the 573–667 box, floor 535–557 in 531–560, kills 496–516 in 491–520, bia 456–476 in 451–480, HP 415–436 in 411–440, and the two button labels 288–302 / 221–235 inside the 269–320 and 203–253 button boxes. What the screen *says* is still absent from the images (empty authored labels), which the report states |
| 9 | The summary column carries a background at the camp panel's alpha (asked for after row 7 had judged the bare column) | **PASS** — `CreateResultScene` gives the column an `Image` at `new Color(0.07f, 0.09f, 0.12f, 0.92f)` — the camp `Board`'s own colour and alpha, not a value chosen here — with `raycastTarget = false` so a background cannot swallow either button's click; the scene was rebuilt from the builder (`ProjectSurvive/Stage 10/6. Create Result scene`), with the pre-change scene kept at `artifacts/stage10/Result-before-panel.unity` for the comparison. The capture re-shot at **02:18 on 2026-10-17** reports `Summary (Surface, alpha 0.92)` at both sizes, **PASS**, 0 failures, 1 884 ms, 0 overlaps. In pixels (`artifacts/stage10/measure-result-panel.ps1`): the panel's colour is **54.9 %** of the 1920×1080 frame and **55.0 %** of the 1280×720 one — the column's 1200×1000 area, minus its text — reading 22,22,28 inside the column (700,500) against 13,15,20 outside it, where those pixels used to be the scene's clear colour alone (the bare column's only other marks were the two buttons, 2.8 % of the frame). Enforced, not just measured: `UiLayoutCheck.OpaqueRegions` now names `Summary` beside the camp's `Board` and `ChestPanel`, so the capture tool fails on a column below the 0.9 floor, and two new EditMode tests hold it — `ResultScreenLayoutTests.The_summary_column_carries_the_camp_panels_own_background` reads the camp `Board`'s alpha out of `Camp.unity` and compares it with the column's own `Image` (equal within 0.001, at or above the floor, `raycastTarget` off) and `UiLayoutCheckTests.The_summary_column_is_held_to_the_same_floor_as_the_camp_panels`. EditMode 119 → **121**; `-Suite Fast` **PASS 10 / FAIL 0 / NOT RUN 3**, 85.9 s (`artifacts/stage10/fast-summary-panel.log`). The WebGL gate was then run on the same day with the Editor closed: `web: WebGL build (Development)` **PASS**, 20.7 s (→ `UnityProject/Builds/Web`) and `web: Playwright (smoke + battle + camp + result)` **PASS**, **9 passed in Chrome**, 127.0 s — `artifacts/stage10/full-panel-web.log`, `artifacts/dotnet/playwright.log`; `PASS 11 | FAIL 0 | NOT RUN 2`, 393.8 s. That run's two NOT RUNs are EditMode and PlayMode (a closed Editor holds no seat for them), both measured minutes earlier with the Editor open (EditMode **121/121**, `ResultScenePlayModeTests` **3/3**). **The panel is in the real player, in pixels**: the browser check's own shot of the summary screen (`tests/web/test-results/web-result-summary.png`, 1280×720, written 02:31) is **33.8 %** the panel's colour `22,22,28` and **26.2 %** the scene's clear colour `13,15,20`, and its middle scan line reads white (the page) → clear colour → the panel from x≈340 to x≈940 → clear colour → white, which is the column's own rectangle at this canvas size. `22,22,28` is the very value the Editor capture measures inside the column, so the surface the layout test and the capture tool judge is the one the player paints — and the clicks the spec makes are what says the background is not in their way: `Restart` was reached and the camp came back with a new run. The A/B is the Editor's (`artifacts/stage10/Result-before-panel.unity`), because the browser check overwrites its own shots on every run |
| 8 | A forced loss is the loss the rules produce (found by the gate, fixed) | **PASS** — `loseNow` used to finish as `Lost` / `PlayerDied` with the player still at full HP, so the run kept 100 HP through `ApplyBattleResult` and the summary screen printed a defeat at full health. The cheat now spends the HP first, in both sessions (the ECS simulation and the fake), and `BattleSessionContractTests.Dev_lose_now_reports_the_death_it_stands_for` pins `FinalHp == 0` for both factories (dotnet 466 → **468**). The first run of the browser check failed on exactly this (`Expected: 0, Received: 100`) and its own artifacts are kept: `artifacts/stage10/playwright-result-first-run/error-context.md`, `test-failed-1.png`, `trace.zip` |

### How the web gate found the two things it found

The first `-Suite Full` with the Editor closed (`full-followup-editor-closed-web.log`) built the player
and ran the new spec — and failed it, twice over, both times on a fact rather than a flake:

1. `Expected: 0, Received: 100` on `finalHp`. The dev menu's `loseNow` ended the floor as `Lost` /
   `PlayerDied` with the player still at full health, so the run kept 100 HP through the settlement and
   the summary screen printed `HP คงเหลือ 100/100` under a defeat headline. That is a **rule** defect,
   not a test one, and it is fixed in row 8. The failing run's artifacts are kept in
   `artifacts/stage10/playwright-result-first-run/` (`error-context.md` carries the message).
2. `Expected: 0, Received: 6` on `bia` — this one was **the spec's own mistake**: it compared the
   summary's purse (`RunSession.Wallet.Bia`, the run's 6 เบี้ย) with the arena's `bia`, which is
   `hud.Currency` — the currency *earned inside the arena* (`BattleSimulationRunner.PublishReport`),
   and 0 because no drop column pays yet. The assertion was replaced with what the screen can honestly
   be held to (the purse is a real number and `biaLine` carries it), and the corrected spec passes in
the player (1/1, 8.5 s). Neither the one nor the other was left green by loosening a check that should
have held: the first fix tightened the game, the second deleted a wrong comparison and said why.

### What changed

- `UiCaptureTool` (Editor): a `Result` section beside Camp and Battle — `CaptureResult` reads the
  screen's own serialized fields, measures the column it lives in plus its five lines and two buttons,
  and shoots both sizes; `SceneKind.Result`, the menu item name and the report title follow. Its
  pre-existing `<para>        /// A <b>third</b>` doc-comment typo was fixed while the block was open.
- `Presentation/ResultReport.cs` (new) + `ResultView.ResultButton` / `TryGetButtonScreenPoint`: the
  summary screen's document, mirroring `CampReport`'s contract — the strings the player reads, the run's
  own numbers, and screen-pixel centres for the two controls. It reuses `CampScreenRects.TryCentre`
  rather than copying the corner-pivot conversion.
- `Presentation/Battle/DevMenuReport.cs` (new) + `DevMenuView.DevMenuButton` / `TryGetButtonScreenPoint`
  / `Update`: the same for the dev panel. Coordinates only, never a permission.
- `lib/Runtime/Simulation/Battle/EcsBattleSimulation.cs` + `lib/Runtime/Application/Battle/Fake/FakeBattleSession.cs` (**a rule fix**, not a UI one): `DevLoseNow` spends the player's HP (a one-call `KillPlayer` / a `_hp = 0`) before it finishes, so the result is `Lost` / `PlayerDied` / `FinalHp` 0 in both sessions. `lib/Tests~/Core.Tests/Battle/BattleSessionContractTests.cs` gains `Dev_lose_now_reports_the_death_it_stands_for`, which the shared suite runs against **both** factories (dotnet 466 → 468). The web gate is what found it: the summary screen's `hp` line was asserted against the arena's `finalHp`, and a forced defeat was reporting 100 of 100. Applied to the fake as well because a fixture that manufactures a different defeat than the rules do is how a preview and the game drift apart.
- `tests/web/result.spec.ts` (new): the browser check, listed in `tools/test.ps1`'s web step beside
  smoke/battle/camp so the step's own name (`… + result`) is true.
- `ProjectSetup.CreateResultScene` + `Result.unity` (rebuilt from the builder, not by hand): the
  summary column's background `Image`. `UiLayoutCheck` grows the `Summary` region name and lists it in
  `OpaqueRegions` beside the camp's two panels, and `UiCaptureTool` uses that constant instead of its
  own copy of the string. `ResultScreenLayoutTests` gains the cross-scene comparison (it opens
  `Camp.unity` additively and closes it again, as it already did with `Result.unity`) and
  `UiLayoutCheckTests` the floor case.

### Commands run (this follow-up)

```pwsh
# the Editor UI capture, re-shot from the shipped scene at the two GDD sizes
unity command menu --path "ProjectSurvive/Tools/1. Capture UI (Camp + Battle + Result, 1920x1080 + 1280x720)"
#   -> 10 images, PASS, 0 failures, 1 265 ms; artifacts/ui/{camp,battle,result}-*.png + ui-report.md

# (c) the column's background: rebuild the scene from the builder, then judge it again
unity command menu --path "ProjectSurvive/Stage 10/6. Create Result scene"
unity command menu --path "ProjectSurvive/Tools/1. Capture UI (Camp + Battle + Result, 1920x1080 + 1280x720)"
#   -> PASS, 0 failures, 1 884 ms; Summary (Surface, alpha 0.92) at both sizes, 0 overlaps
pwsh -NoProfile -File artifacts/stage10/measure-result-panel.ps1 -Image artifacts/ui/result-1920x1080.png
#   -> panel colour 54.9 % of the frame (55.0 % at 1280x720); inside the column (700,500) 22,22,28
#      against 13,15,20 outside it
unity command run_tests --mode editor --filter Layout --async_tests               # 25/25, 0.7 s
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                                   # PASS 10 | FAIL 0 | NOT RUN 3, 85.9 s,
                                                                                   # EditMode 121/121, dotnet 468

# (c) the WebGL gate: the Editor has to be closed — tools/test.ps1 refuses the build while it is open
unity command eval "UnityEditor.EditorApplication.Exit(0);"                        # the MCP `quit` command
                                                                                   # cannot run in edit mode
pwsh -NoProfile -File tools/test.ps1 -Suite Full                                   # PASS 11 | FAIL 0 | NOT RUN 2, 393.8 s;
                                                                                   # WebGL build PASS 20.7 s; Playwright
                                                                                   # 9 passed in Chrome 127.0 s (result.spec.ts 5.7 s);
                                                                                   # EditMode/PlayMode NOT RUN (Editor closed) — both
                                                                                   # run earlier with it open (see row 9)
pwsh -NoProfile -File artifacts/stage10/inspect-browser-shot.ps1 -Image tests/web/test-results/web-result-summary.png
#   -> 1280×720: panel 22,22,28 = 33.8 %, clear colour 13,15,20 = 26.2 %; row 360 reads white (page)
#      -> clear colour -> panel x≈360..920 -> clear colour -> white: the column's own rectangle

# Unity, Editor open
pwsh -NoProfile -File tools/test.ps1 -Suite Full            # PASS 10 | FAIL 1 | NOT RUN 2, 222 s; EditMode 119/119,
                                                            # PlayMode 20/40 = the ambient-log flake (playmode-per-class-followup.md)
unity command run_tests --mode playmode --filter Camp --async_tests               # 25/25, 61.1 s
unity command run_tests --mode playmode --filter ResultScenePlayModeTests --async_tests  # 3/3, 10.0 s
unity command run_tests --mode playmode --filter SceneFlowPlayModeTests --async_tests    # 5/5, 48.4 s
unity command run_tests --mode playmode --filter BattlePlayModeTests --async_tests       # 4/4, 8.3 s
unity command run_tests --mode playmode --filter ShellPlayModeTests --async_tests        # 4/4, 0.2 s

# the rule fix, then the web gate with the Editor closed
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter FullyQualifiedName~Dev_lose_now   # 2/2 (both factories), 39 ms
npx playwright test result.spec.ts                           # against the live player: 1 passed, 8.5 s
pwsh -NoProfile -File tools/test.ps1 -Suite Full              # Editor closed -> PASS 11 | FAIL 0 | NOT RUN 2, 245.9 s,
                                                             # WebGL PASS + Playwright 9 passed in Chrome (140.0 s)
```

### Not tested, and risks (this follow-up)

- **The summary column used to draw no panel, and the capture is what made that visible; it now does (row 9).** Before: `Result.unity`'s `Summary` column is a bare `RectTransform`: unlike every other named panel in the project (the camp's `StatusPanel` 0.88, `Board` 0.92, the tooltip 0.95, the battle's dev panel 0.88 — `ProjectSetup` gives each of them an `Image`), it carries no background, so a capture of it is 97 % one flat colour with the two buttons (2.8 %) and the text as the only other marks, and the live screen agrees (the same measurement on `result-victory.png`: 96 % background, 2.8 % buttons, white text). That was **legible and within the GDD's wording** for the screen (victory/defeat, floor, ghosts, ways out) and it was left that way rather than restyled on the way past — the one screen of the three whose look was "white text on the clear colour". The look is no longer that: the column was given the camp `Board`'s own background at the camp's alpha (row 9), so all three screens now draw their content on a panel. It is still the same trade-off, named rather than settled by taste: the panel is a colour and an alpha with a measurement behind them (54.9 % of the frame), and the strings on top of it are unchanged.
- **A stray server on port 8080 costs a run.** The first closed-Editor Full run reported the Playwright
  step **NOT RUN** — `tools/serve-web.mjs did not start on port 8080` — because a `node` from an
  earlier manual run was still listening; the harness's own server then exits on `EADDRINUSE` and the
  step is correctly refused rather than reporting the *old* build as tested. One server at a time, and
  the message means exactly that (`knowledge.md` now says so).
- **The captured Result images hold no text.** Every label in `Result.unity` is empty until the runtime
  fills it from the localization tables, exactly as the camp's and the battle's shots are — so the tool
  proves the screen's geometry and its *look* as a layout, and what the screen says is proved by
  `ResultScreenTests` / `ResultReportTests` (the table's strings) and by the browser check (the words in
  a real build). Making the tool's own shots carry text would mean either writing strings into a scene
  the tool is not allowed to change, or a Play-Mode entry point like the merge chooser's; neither is in
  this follow-up.
- **The browser check needs a Development build** — that is what a dev menu means
  (`DevCheats.Allows(isDevelopmentBuild, isEditor)`), and it is the configuration `tools/build-web.ps1`
  produces by default and the one the suite's web steps run. Pointed at a Release build, the check fails
  with the dev-menu document missing rather than passing quietly, which is the honest outcome; a
  Release-build browser check of the summary screen would need the run to be *played* to its end.
- **The Result screen's own DI is exercised, but the stage's IL2CPP question is only narrowed.** The
  check proves the scene's scope builds and its strings render in a real WebGL player; it does not
  claim anything about a Release (stripped, non-development) build, which no browser step in this
  project runs.
