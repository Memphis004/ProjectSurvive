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
