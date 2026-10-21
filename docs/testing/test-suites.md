# Test suites — Fast, Full, and every test's home

`tools/test.ps1` runs one of two suites. This file is the contract between the two: which test lives
where, what each step really costs on this machine, and what has to be missing before a step is
allowed to report NOT RUN.

## 0. Why the split

Every Stage from 07 to 09d paid `dotnet` + Unity EditMode + PlayMode + a WebGL build + Playwright for
every check of every change, and then wrote it up. The cost was in the *build* and the *browser*: a
WebGL Development build measured 258 s on 2026-10-15 (Unity's own `buildSeconds`) and the browser suite
another 2 min, none of which can see a change in a rule engine that `dotnet test` answers in 2.3 s.
Nothing about the checks changed here — the same tests run — they are now divided by how often they
need to run, and the Web build moved behind the Full suite.

## 1. Which suite runs what

| Step | Suite | What it covers |
| --- | --- | --- |
| `toolchain: .NET SDK` | Fast | an SDK ≥ 8 is present |
| `dotnet build` | Fast | every `lib/` layer, the xUnit project and the Sim compile |
| `dotnet test (xUnit)` | Fast | 571 tests: the rule engine, the architecture guards, the contract suites, the Sim |
| `sim: determinism` | Fast | the same seed prints the same `determinismDigest` twice, a different seed does not |
| `sim: stream separation` | Fast | shop / battle / visual digests differ, `ticksProduced=300` |
| `sim: bot smoke run` | Fast | the kiting bot plays 2 seeds and writes balance rows |
| `data: generated tables validate` | Fast | `tools/validate-data.ps1` over the real tables |
| `data: generated code reproducible` | Fast | `tools/verify-generated.ps1` — `data/` + `proto/` reproduce the tracked output |
| `data: broken fixtures rejected` | Fast | `tools/check-fixtures.ps1` — 21 broken data sets rejected for the right reason |
| `wiki: content reproducible` | Fast | `tools/verify-wiki.ps1` — `llmwiki/content/` regenerates byte-identically from `data/`, every relative link and anchor resolves, every data id has a section, no `systems/`/`gotchas/` orphan, and a WARN-only heuristic for hand-written pages that restate skill numbers |
| `unity: EditMode` | Fast | 169 tests in `ProjectSurvive.Game.Tests.EditMode` |
| `unity: PlayMode` | **Full** | 62 tests in `ProjectSurvive.Game.Tests.PlayMode` (scenes, pooling, pointer drags, walking, the upgrade-choice window, the title screen's start control, the battle HUD's mini ย่าม — its cooldown plates and its stacked-under-the-caption geometry — the three scopes and the 20-round-trip loop) |
| `web: WebGL build (Development)` | **Full** | `tools/build-web.ps1 -Configuration Development -Force` |
| `web: Playwright (smoke + title + battle + camp + result)` | **Full** | 10 browser tests against that build, in Chrome (13 more are the opt-in screenshot generator, skipped unless `SCREENS=1`) |

`-Release` moves the dotnet build and the WebGL build to Release. It is for a Stage gate or a
measurement — a Development build's numbers may not be quoted ([ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md)).

Commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1              # Fast (default)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full  # Full
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full -Release
```

Rules around the two suites, from `AGENTS.md`: the working loop is Fast; Full is for closing a Stage
or committing one; a Stage's own gate (stages 10, 12, 14, 15) may name Full with `-Release`. A layout
change is not a reason to build Web — `ProjectSurvive/Tools/1. Capture UI` renders the scene in the
Editor at both sizes instead (§6).

Measured on 2026-10-16 for Stage 10, both runs **PASS 10 | FAIL 0 | NOT RUN 3**: **85.3 s** after the
`SceneLoader` repair (`artifacts/stage10/fast-after-fix.log`) and **76.0 s** once the three Result-screen
layout tests landed (`artifacts/stage10/fast-after-layout-tests.log`) — the same WARN 0 result, the
spread being the cold/warm behaviour §2 records. The three NOT RUN steps are
`unity: PlayMode`, `web: WebGL build` and `web: Playwright`, which Fast does not request; `dotnet test`
466 passed and `unity: EditMode` 115/115 in both runs.

Stage 08b (2026-10-17) moved one count and no Unity one: `dotnet test` **468 → 481** (the thirteen
cooldown/frame cases, four of them run twice because the shared suite binds both factories), EditMode
**130/130** unchanged (the stage touched the contract and the simulation, not the shell) and its
`-Suite Fast` run is `artifacts/stage08b/fast-suite.log` — **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**,
218.8 s.

Stage 08d (2026-10-19) moved two counts and no dotnet one: EditMode **147 → 155**
(`BattleMiniBagTests` **14 → 18** — the top-left origin, the L-shaped bag, two bags sharing a box, and
 the caption gap — and
`UiLayoutCheckTests` **20 → 24** — the mini bag's own stacking rule) and PlayMode **46 → 47** (the
battle class 5 → 6, the scene's own grid-under-caption geometry), while `dotnet test` stayed **497**
and Playwright stayed **10**. Its `-Suite Fast` run is `artifacts/stage08d/fast.log` —
**PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**, 86.6 s (the final green run; an earlier attempt hit the
environment-sensitive Result-report case recorded in the stage report §6). WebGL and Playwright were
not run, by the brief.

Stage 07b (2026-10-19) moved two counts: `dotnet test` **504 → 510** (the kill→drop→collect value,
sum-and-once pickup, lost-floor clear reward and three data rules) and the broken-fixture set
**20 → 21** (`enemy-drop-value-negative`); EditMode stayed **155** because the HUD assertion was
added to an existing case. Its `-Suite Fast` is `artifacts/stage07b/fast.log` — **PASS 10 | FAIL 0 |
NOT RUN 3 | WARN 0**, `dotnet test` **510 passed** and EditMode **155/155**. The Full run made with
the Editor open is red only on the documented bulk PlayMode ambient-log flake (22/47); the classes
this stage can affect pass alone (`BattlePlayModeTests` 6/6, `SceneFlowPlayModeTests` 5/5,
`ResultScenePlayModeTests` 3/3), and WebGL/Playwright are NOT RUN (not a gate stage, Editor lock).

Stage 06b (2026-10-19) is a **bug fix** and moved one count: `dotnet test` **497 → 504** (seven cases
in `ShopStockTests` for "a new sala fills its shelves again"), while EditMode stayed **155** and
PlayMode **47** (the camp-2 condition was added to an existing case, `ResultScenePlayModeTests`'
victory loop, rather than to a new one). Its `-Suite Fast` runs are `artifacts/stage06b/fast.log` and
the closing `artifacts/stage06b/fast-final.log` — **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**, 153.1 s and
178.5 s, `dotnet test` **504 passed** and EditMode **155/155** in both. The Sim's own digest is
**unchanged** by the fix, measured both ways (`determinismDigest=0x841b4e46` at seed 20261013). The new PlayMode condition was run per class (`ResultScenePlayModeTests` **3/3**,
`artifacts/stage06b/playmode-resultscene-after-fix.json`) and was **red-checked** by reverting the fix
and re-running the same class — it failed with *sala 2: only 3 of 4 shelves are rentable*
(`artifacts/stage06b/playmode-resultscene-red-check.json`) — so the condition is not vacuous. 06b's own
closing Full run (`artifacts/stage06b/full-editor-open.log`, Editor open) is **PASS 10 | FAIL 1 |
NOT RUN 2**: the one FAIL is the bulk PlayMode flake of §7 — **22/47** reported, its 25 non-passes the
same 12 `IndexOutOfRangeException` + 8 `Cached unprocessed value` ambient messages, with the extended
`ResultScenePlayModeTests` case **Passed inside that same payload** — and both web steps are NOT RUN
because the Editor held the project lock. WebGL and Playwright were not run: 06b is not a gate stage
and nothing in Presentation changed.

Stage 08c (2026-10-17) moved two counts and left the rest: EditMode **130 → 144** (`BattleMiniBagTests`
14, and `BattlePresentationTests` 15) and PlayMode **45 → 46** (the battle class 4 → 5), while
`dotnet test` stayed **481** (the four new `hud.*` localization rows moved the load test's pinned row
count 188 → 192) and Playwright stayed **10**. Its `-Suite Fast` run is
`artifacts/stage08c/fast-after-minibag-fix.log` — **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**, 88.1 s.
Its PlayMode evidence is **per class** like Stage 10's, because the bulk assembly run is the flake in §7:
`BattlePlayModeTests` **5/5** in `artifacts/stage08c/playmode-battle-class.json`, and the bulk payload
(`artifacts/stage08c/playmode-bulk-after-minibag-fix.json`) reports **20/46** with every failure an
ambient log assert or its cascade.

Stage 10d (2026-10-17) moved all four counts: `dotnet test` **468** (the localization row count the
title's one new string moved), EditMode **130/130** (`TitleScreenTests` 5 + `TitleScreenLayoutTests` 4),
PlayMode **45** (`TitleScenePlayModeTests` 5) and Playwright **10**. Its `-Suite Fast` run is
`artifacts/stage10d/fast-after-scene-anchor.log` — **PASS 10 | FAIL 0 | NOT RUN 3**, 81.6 s — and its
closed-Editor Full run is `artifacts/stage10d/full-editor-closed-web.log` — **PASS 11 | FAIL 0 |
NOT RUN 2**, 268.4 s, WebGL **PASS** 70.2 s + Playwright **10 passed in Chrome** 130.7 s.

Stage 10t (2026-10-18) moved two counts and left the other two: `dotnet test` **481 → 497**
(`EventRingTests` 9 + `PrevCurBufferTests` 7) and EditMode **144 → 147** (`WebGlBridgeTests` 3), while
PlayMode stayed **46** and Playwright stayed **10**. It ran the Full suite **before** its first edit
(Editor open: `artifacts/stage10t/full-before-editor-open.log`, Editor closed:
`artifacts/stage10t/full-before-editor-closed.log`) and again after
(`artifacts/stage10t/full-after-editor-open.log`, `artifacts/stage10t/full-after-editor-closed.log`), so
the four counts above are a delta between two measurements of the same machine, not a claim from memory.
Its PlayMode evidence is per class again, for the reason in §7: the bulk payload is **21/46** with 25
ambient-log failures both before and after the change, and the eight classes are **46/46** on their own
(`artifacts/stage10t/playmode-<Class>.json`). The pre-change bulk run matters here: a refactor that
claims "no behaviour change" is only as good as the failure set it inherited.

Stage 09h (2026-10-19) moved three counts: `dotnet test` **510 → 519** (`PieceReadoutTests` 8 +
one case in `ConfigLoadTests` for the three new localization rows), EditMode **155 → 156** (the
tooltip's resolved-row test in `CampPresenterTests`) and PlayMode **47 → 48** (the resolved-numbers
case in `CampBagDragPlayModeTests`), while Playwright stayed **10**. Its `-Suite Fast` run is
`artifacts/stage09h/fast.log` — **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0**, 107.9 s, `dotnet test`
**519 passed** and EditMode **156/156** — and its Editor-open Full run
(`artifacts/stage09h/full.log`) is **PASS 10 | FAIL 1 | NOT RUN 2**: the one FAIL is the bulk PlayMode
flake of §7 (**22/48**), and the two fixtures this stage can affect were run on their own and are green
— `CampBagDragPlayModeTests` **12/12** (`artifacts/stage09h/playmode-camp.json`) and
`CampMergeChooserPlayModeTests` **4/4** (`artifacts/stage09h/playmode-chooser.json`). WebGL and
Playwright are NOT RUN (Editor lock; 09h is not a gate stage).

Stage 10e (2026-10-19) is documentation and tools only: it adds the
`wiki: content reproducible` step, so Fast is now **11 steps** (`PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0`,
237.4 s, a cold run — the step itself is steady) while every other count is unchanged — `dotnet test` **525**, EditMode **157/157**, PlayMode
**48** and Playwright **10**. The new step's own row reports **1.4 s** and the tool itself **1.15 s**
against the ~5 s warm budget; the three-fixture self-check runs separately
(`tools/verify-wiki.ps1 -Fixtures`, **5.69 s**, 3/3 rejected) and is deliberately not in Fast. With the Editor open its Full run is **PASS 11 | FAIL 1 | NOT RUN 2**, 221.7 s: the one FAIL is the
bulk PlayMode ambient-log flake of §7 (**22/48**, and the stage changed no Unity code), and both web
steps are NOT RUN because the Editor holds the project lock. With the Editor **closed** it is
**PASS 12 | FAIL 0 | NOT RUN 2**, 225.8 s — WebGL Development build **17.5 s**, Playwright **10/10**
in Chrome **130.6 s** — leaving only the two Unity steps NOT RUN. Evidence:
`artifacts/stage10e/fast-final.log`, `artifacts/stage10e/wiki-fixtures.log`,
`artifacts/stage10e/full-editor-open.log`, `artifacts/stage10e/full-editor-closed.log`.

Stage 10's own gate is Full, and it is **two runs**: Full with the Editor **open**
(`artifacts/stage10/full-suite-editor-open.log`) reports EditMode and PlayMode and NOT RUNs both web
steps, and Full with the Editor **closed** (`artifacts/stage10/full-suite-editor-closed.log`) reports
the WebGL build and Playwright and NOT RUNs both Unity steps. Neither alone is the gate; together they
are.

## 2. Measured timings

Reference machine (the one [ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md) names,
re-quoted here for these runs): Intel Core i5-14400F (16 logical cores), 31.8 GiB RAM, Windows 11 Home
10.0.26300, NVIDIA GeForce RTX 5060 Ti, WD Blue SN5000 1 TB NVMe SSD. Unity 6000.3.9f1 (locked),
`dotnet` SDK 9.0.311, Node/`@playwright/test` 1.64.0 on the Chrome channel.

**Fast**, 2026-10-15, `artifacts/proc10/fast-run1.log` (cold: first run in a fresh shell and after a
clean `lib/` output tree) and `artifacts/proc10/fast-run2.log` (warm, immediately after):

| Step | Cold (s) | Warm (s) |
| --- | --- | --- |
| `toolchain: .NET SDK` | 0.1 | 0.0 |
| `dotnet build` | 10.2 | 1.4 |
| `dotnet test (xUnit)` | 2.7 | 2.5 |
| `sim: determinism` | 36.5 | 1.3 |
| `sim: stream separation` | 0.1 | 0.1 |
| `sim: bot smoke run` | 49.0 | 2.6 |
| `data: generated tables validate` | 62.7 | 3.9 |
| `data: generated code reproducible` | 4.7 | 3.4 |
| `data: broken fixtures rejected` | 52.3 | 48.2 |
| `unity: EditMode` | 10.8 | 13.6 |
| **total of steps** | **229.1** | **77.0** |

The cold column is what a machine pays once per boot (first JIT, first assembly load, first file read
of the tables); the warm column is the loop a developer actually feels. Nothing in the cold column is
a check doing more work — the same 17 fixtures are rejected in both.

Warmth is not something the harness controls: a fourth run in the same session
(`artifacts/proc10/fast-default-run.log`, the default suite invoked with no arguments) came back at
**241.2 s** — the cold profile again (`dotnet build` 8.4 s, `sim: determinism` 38.1 s,
`data: generated tables validate` 65.3 s). An Editor restart and a busy machine were enough to lose the
caches, so budget Fast at **1–4 minutes** rather than at the warm number.

Stage 10e measured its new step on the same machine: **1.4 s** in the harness row and **1.15 s** inside
`verify-wiki.ps1` (8 pages regenerated and diffed, 498 links checked), against a **~5 s** budget. The
`-Fixtures` proof is a separate, slower command (**5.69 s**).

**Fast with `-Release`** (2026-10-15, warm machine, `artifacts/timings/proc10-release/fast-release.log`)
— this run is what verifies the flag does what §1 says it does: the summary header reads
`configuration: Release (web: Release)` and `dotnet build` reports `Release configuration`:

| Step | Seconds |
| --- | --- |
| `toolchain: .NET SDK` | 0.0 |
| `dotnet build` | 5.6 (Release) |
| `dotnet test (xUnit)` | 2.4 — 425 passed |
| `sim: determinism` | 36.0 |
| `sim: stream separation` | 0.1 |
| `sim: bot smoke run` | 48.8 |
| `data: generated tables validate` | 15.6 |
| `data: generated code reproducible` | 1.9 |
| `data: broken fixtures rejected` | 59.9 |
| `unity: EditMode` | 10.6 — 65/65 passed |
| **total of steps** | **180.9** (PASS 10, FAIL 0, NOT RUN 3) |

The flag's **WebGL half is Full-only and was not re-run** for this measurement; it is the same
`tools/build-web.ps1 -Configuration Release` Stage 09 measured (201.4 s, 64,426,152 B,
`artifacts/stage09/build-report.json`). A Stage gate that names Full with `-Release` still needs that
run — `-Release` is exercised there, not here.

**Full only** (2026-10-15, Editor closed, `artifacts/proc10/full-editor-closed.log` and the
`test-summary.md` it wrote):

| Step | Seconds | Note |
| --- | --- | --- |
| `unity: PlayMode` | 42.7 | 24 tests; **12 failed** in this run — see §7 |
| `web: WebGL build (Development)` | 269.3 cold / **16.5** warm | cold: a fresh IL2CPP translation (Unity's own `buildSeconds: 258.3`, 119,647,484 B, 5 scenes). warm: the second build minutes later reused the IL2CPP C++ cache (Unity: `buildSeconds: 6.7`, 119,647,478 B) |
| `web: Playwright (smoke + title + battle + camp + result)` | 121.4 (2026-10-16: 9 tests) / **130.7** (2026-10-17, the 10th test) | 10 tests, 1 worker, Chrome channel |

`unity: EditMode` measured 9.2–13.6 s across the five runs above and `dotnet test` 2.2–2.7 s; the
numbers quoted per run are the runs of record, not averages.

## 3. Every test, and the suite it belongs to

Nothing was dropped when the suites were split: this table lists every test that existed before the
split and where it runs now. "Fast" means the working loop, "Full" the closing one; a test cannot be in
neither.

### dotnet — `lib/Tests~/ProjectSurvive.Core.sln` (481 tests) → **Fast**

Run by `dotnet test` in the `dotnet test (xUnit)` step. The project is the *same source* as the `lib/`
asmdefs ([ADR 0004](../adr/0004-dotnet-layer-enforcement.md)), so these are the rule-engine tests.

| Where | What it covers |
| --- | --- |
| `ArchitectureTests.cs` | layer references, `Domain`/`Simulation` free of Unity/R3/VContainer/serializers |
| `RepoLayout.cs` | the repo's own layout contract |
| `Xoshiro128Tests.cs`, `RandomStreamsTests.cs` | the RNG and the three streams |
| `FixedTickerTests.cs`, `ResultTests.cs`, `InfrastructureTests.cs` | ticker, `Result`/`Error`, adapters |
| `ConfigLoadTests.cs`, `ConfigRulesTests.cs`, `ConfigEnumParityTests.cs`, `ConfigFixture.cs` | the generated tables, their rules and pinned counts |
| `LocalizationServiceTests.cs` | localization lookup |
| `ProbeSerializationTests.cs`, `EcsLiteSmokeTests.cs` | MessagePack/protobuf round trips, the vendored ECS |
| `Battle/*.cs` | the battle contract suite over the fake and the real ECS session, the frame buffer, the event ring. Includes `Dev_lose_now_reports_the_death_it_stands_for` (both factories, 2026-10-17): the cheat spends the player's HP before it finishes, so a forced loss is `PlayerDied` with `FinalHp` 0 rather than a defeat at full health. **Stage 08b** added four cases to the shared suite, so each runs against **both** factories — `The_frames_cooldowns_are_the_loadout_entries_with_the_board_pieces_they_came_from`, `A_cooldown_runs_down_one_tick_at_a_time_and_resets_to_its_full_period`, `A_ready_caster_the_player_cannot_afford_is_flagged_mana_short` and `Step_with_a_caster_loadout_does_not_allocate_after_warm_up` — plus `The_state_hash_of_a_seeded_floor_with_casters_is_unchanged` in each derived class, a literal recorded on the pre-change build in a clean `git worktree` of `HEAD`, `A_cooldown_list_is_not_visible_until_publish_ran` and `The_cooldown_list_stops_at_the_loadout_cap` in `BattleFrameBufferTests`, and `Every_caster_entry_names_the_board_piece_it_was_resolved_from` in `LoadoutResolverTests` (the board id's only producer) |
| `Inventory/*.cs` | Board/Backpack/BackpackEditor/Merge/Recycle/Rotation/Synergy/Loadout + the randomized bag tests. **Stage 09k-data** made the tome rule a touch rule (ADR 0031): `SynergyEvaluatorTests` gained a four-edge `[Theory]`, the one-cell gap, the corner touch, two weapons on different rows, the three-tome cap and a move-and-recompute case; `BackpackRandomizedTests` gained a 10 000-command walk that re-derives the touching pair from the cells and now also re-checks every `AdjacentPair` record the wide walk reports |
| `Run/*.cs` | RunSession economy and flow, shop stock, the drop preview, replay. **Stage 06b** added seven cases to `ShopStockTests` (`A_new_camp_refills_a_slot_rented_in_the_previous_one`, four of them through `[Theory]`, plus several purchases, the whole panel, and a pin left behind): every new sala offers four rentable shelves again, none of them still marked rented or locked, `RefreshCount` 0 |

### Unity EditMode — `ProjectSurvive.Game.Tests.EditMode` (169 tests) → **Fast**

| Class | Tests | What it covers |
| --- | --- | --- |
| `ShellSmokeTests` | 12 | the 7-probe smoke suite, config hash, R3/UniTask/serialization in the Editor |
| `BattlePresentationTests` | 16 | the battle HUD/state/presenter seam without a scene; since Stage 07c the sixteenth draws a real floor through a real sweep and checks `viewsGrownDuringPlay = 0` while the drops are pulled in |
| `BattleMiniBagTests` | 18 | **Stage 08c** — the mini ย่าม's arithmetic against hand-made frames: a plate is `RemainingTicks / PeriodTicks` (four cases over the period and the remaining ticks), the three tones (counting / ready-but-MP-short / ready), the bounding box the bag's cells actually cover and the cell size that fits it into the panel, the read-only rule (no piece is moved, no rule is recomputed), and the update-on-change rule (a frame identical to the last writes nothing). **Stage 08d** added the origin and the stacking: the first box starts at the content area's top-left at both sizes, the L-shaped `bag.wicker_basket_l` hugs the corner of its own bounding box, two bags anchor to the corner of their union box, and the grid's area starts exactly one named gap under the caption on the caption's left edge |
| `CampPresenterTests` | 34 | every camp command through the presenter, the preview/command agreement, the 0-byte drag, the drawn-link kinds, and (Stage 09g) the upgrade-choice window: dropping a matching pair opens it with nothing changed, the cards are the offer's own rows in order with their data lines, the left panel is the tooltip's own five lines, confirming raises the tier in the same cell and reaches the loadout, cancelling changes nothing and reopening offers the same cards, a pair with no choices still folds on the drop, and the board is locked while the window is up. **Stage 09k-data** re-points the line-drawing cases at `SameRow` themselves (since ADR 0031 no shipped row is a same-line rule) and adds one case pinning the shipped truth: a touching tome→weapon pair is a working combo that draws **no** permanent mark until Stage 09l gives `AdjacentPair` its own |
| `CampTooltipPlacementTests` | 4 | the tooltip stays on screen, flips at the edges and never covers the pointer |
| `RunFlowEditModeTests` | 4 | the run flow against the shipped `Resources` tables |
| `WebSizeProfilesTests` | 8 | the size profiles and their recorded configuration |
| `BackpackRulesTests` | 10 | the bag rules as the Editor sees them |
| `UiLayoutCheckTests` | 24 | the overlap rule the UI capture command applies, the overlay exemption, the opacity floor and (Stage 09g) the modal rules: a window may cover the panel it is asking about, two cards sharing pixels are still reported, a card inside its window passes and one outside it does not; includes the deliberate-overlap case and the summary column's own floor case (the Result screen's column is held to the same 0.9 as the camp's panels). **Stage 08d** added the mini bag's stacking rule: a grid below the caption on its left edge passes, one lifted over the caption is reported (twice: overlap and stack), one inset from the caption's left edge is reported, and a screen with no mini bag invents nothing |
| `DevCheatTests` | 8 | **Stage 10c** — `DevCheats.Allowed` is false until the root decides it and false in a release player, `Reset` puts it back; every dev command through the real ECS session: a floor with cheats off refuses each one with `RejectedNotAllowed` and changes nothing (HP, bia, entity count, phase), `DevGodMode` only takes 0/1, `DevAddBia` only takes a positive amount, and a refused command leaves the menu's own toggle telling the truth |
| `ResultScreenTests` | 7 | **Stage 10b** — the summary a cleared run and a lost one produce (headline, floor reached, ghosts, bia, HP), a run that has not ended, the strings coming through the localization table, one way out at a time, `ReturnToTitle` being the run's own transition, and every `result.*` row existing in both languages |
| `RunScopeLifetimeTests` | 3 | **Stage 10a** — the run is created once and replaced only by its owner's disposal, a run after a disposal is a new one and not the old object, and a scene scope inherits the App scope's run instead of building its own |
| `ResultScreenLayoutTests` | 4 | **Stage 10b** — the Result scene read from disk and judged with `UiLayoutCheck`: the seven regions are pairwise disjoint and inside the summary column, every label is ≥ 22 px in a ≥ 32 px line box, the reading order (headline → lines → restart → title) holds, and the column carries a background at the camp panel's own alpha (read out of `Camp.unity`, not copied). Measured rectangles in [`artifacts/stage10/result-layout.md`](../../artifacts/stage10/result-layout.md) |
| `TitleScreenTests` | 5 | **Stage 10d** — the screen's one action through the presenter and the shipped tables: the start caption and the heading come from the localization table (and every key exists in both languages), the control starts the run **once** and refuses a second press while the first move is in flight, a run that is already live is taken to the camp rather than started again (`RunSession.StartRun` refuses `WrongState`), and after a disposal the same control builds a **new** run |
| `TitleScreenLayoutTests` | 4 | **Stage 10d** — `Title.unity` read from disk and judged with `UiLayoutCheck`: the heading, the build line and the control are pairwise disjoint and inside the canvas, every label is ≥ 22 px in a ≥ 32 px line box, the control is ≥ 44 px tall with a raycastable target, and the reading order (heading → build line → control) holds |

### Unity PlayMode — `ProjectSurvive.Game.Tests.PlayMode` (62 tests) → **Full**

| Class | Tests | What it covers |
| --- | --- | --- |
| `BattlePlayModeTests` | 6 | the battle scene starts, ticks, re-enters and disposes; **Stage 08c** added the scene's own wiring of the mini ย่าม (it is built, it matches the loadout's casters, and it creates nothing after its build) and the two claims a run can only make through the real scene — every plate equals the frame's own `RemainingTicks / PeriodTicks` with the tone its flags ask for, and those plates move as the floor runs. **Stage 08d** added the scene's own geometry: the grid's rectangle is below the caption and shares its left edge, the first box is at the content's top-left, and `CreatedAfterBuild` / `viewsGrownDuringPlay` stay 0 over a second of ticks |
| `CampBagDragPlayModeTests` | 16 | the จัดย่าม screen driven by real pointer events, plus the tooltip's draw order, the two-name rule and the line/badge kinds; **Stage 09k** added the grab offset — a piece grabbed by its lower-right cell drops where the ghost showed — and the selection being cleared by a refused drop |
| `CampSynergyLinePlayModeTests` | 10 | **Stage 09j/09k** — where a combo mark is drawn at all: only a touching tome→weapon pair on one row gets a line, the line is level and joins the two cells it names, every rotation keeps it on the gap, the names are drawn above it, and the 720p scale factor keeps the same geometry |
| `CampMergeChooserPlayModeTests` | 4 | **Stage 09g** — the upgrade-choice window through real pointer/keyboard events: drag onto the twin opens it (measured geometry: the dim covers the canvas, the cards do not overlap), a tap on the second card upgrades in the same cell with that card's variant id, `Escape` and the back control close it with the run's hash byte-identical and the same cards offered again, the board behind it stays locked and no tooltip is drawn, and `2` picks the second card |
| `CampWorldPlayModeTests` | 8 | WASD walking, the walk clamp, `E` at a shelf, clicking without walking |
| `SceneFlowPlayModeTests` | 5 | **Stage 10a** — the whole loop through the real scenes: the purse, bag and HP that went into the arena come back out of it (same `RunSession` object, same seed), a settled floor writes `BattleResult.FinalHp` back into the run and heals only what the run's own settlement heals, leaving the arena disposes its `IBattleSession`, arriving at the title leaves no run scope and no scene scope behind, the loader refuses a second transition and a scene that is not built, and **20 camp/battle round trips** leave exactly one scene scope and one run scope each time and grow the managed heap by ≤ 3 MiB (measured: 0.70 MiB; [`artifacts/stage10/loop-memory.md`](../../artifacts/stage10/loop-memory.md)) |
| `ResultScenePlayModeTests` | 3 | **Stage 10b** — a lost floor lands on the summary screen and **Restart** starts a new run at floor 1 with the starting purse and an empty bag, a run can be won floor by floor and ends on the victory summary, and leaving the summary for the title leaves no run behind. **Stage 06b** extended the victory loop (the one case that really crosses camp → battle → camp in one run): shelf 0 is rented and shelf 1 pinned in the first sala, and the second sala's panel must be full, rent-free, unpinned and re-roll counter 0 |
| `TitleScenePlayModeTests` | 5 | **Stage 10d** — the loop's first screen through the real scenes: Boot hands over and **stops** there (the scene marker, the absent run and the screen's own report are read after the handover the boot scene performed by itself), pressing เริ่มเล่น with the mouse starts a run and walks into the camp as **one** run and one scene scope on floor 1, `Enter` and `Space` reach the same control (the focus is asserted first, because "the key did nothing" and "the button was never focused" are different defects), a second press while the move is in flight is refused by the presenter and by the loader with its typed reason, and playing a run to its summary, going back to the title and pressing again starts a run that is **new** (floor 1, starting purse, the starting bag as a `RunSession` built from scratch on the same seed) |
| `ShellPlayModeTests` | 5 | scene loading, the title, the Thai spike's two stacks, UniTask cancellation in play mode. **Note:** the assembly's bulk payload reports four of these five — `Thai_spike_scene_measures_both_stacks_without_missing_glyphs` was absent from the payload of every run since Stage 10 while the file still declares it (see §7) |

### Playwright — `tests/web` (10 gate tests) → **Full**

| Spec | Tests | What it covers |
| --- | --- | --- |
| `smoke.spec.ts` | 2 | the player boots, passes its own 7-probe self-check, hands over to Title; the Thai spike's glyph coverage |
| `battle.spec.ts` | 2 | floor 1 played to a win or a loss in a real browser; a mid-floor report. Since Stage 10 the first test also rides the handover that follows: a **won** floor does not end the run, so the arena carries it back to the camp and the camp's own report has to be the same run — `__projectSurviveScene` becomes `Camp`, `floor` is 2 (a new run would be 1), `hp` is the arena's `finalHp`, `bia` is the run's purse and `canDepart` is true |
| `camp.spec.ts` | 4 | the จัดย่าม flow end-to-end, the sala's controls, WASD + `E`, the layout at 1280×720 and 1920×1080. Its merge step presses `1` to pick the first card of the Stage 09g window after the drop (the camp report publishes no card rectangle, so the key path is the honest one) — that edit was unverified when it landed in 09g and **is verified now**: the Stage 10 Full run with the Editor closed ran this spec **4/4** against the Development player (`artifacts/stage10/full-suite-editor-closed.log`) |
| `result.spec.ts` | 1 | **the last leg of Stage 10's loop in a built player** (2026-10-16): `?scene=Battle`, the dev menu's แพ้ทันที / lose now pressed through the centres `__projectSurviveDevMenu` publishes, then the handover to the summary screen, where `__projectSurviveResult` has to agree with the arena's own last report (`kills`, `bia`, `hp` = `finalHp`, `maxHp`), carry non-empty Thai lines with the run's numbers in them, and name both controls with a point inside the canvas. The player then presses **Restart** — the control the scene drew — and the camp has to come up with a run that is new: `flow` Camp, `floor` 1, `canDepart`, `hp` = the dead run's `maxHp`. Needs a Development build (that is what a dev menu means); a Release build publishes no menu document at all. Measured in a real player (2026-10-17, Full, Editor closed): the step reports **9 passed in Chrome, 140.0 s**, this spec 1/1 of them — after the `loseNow` fix below, and after its own second assertion was corrected (it compared the summary's *purse* with the arena's earned-currency counter; the two are different numbers and the spec now says so) |
| `title.spec.ts` | 1 | **Stage 10d** — the first screen of the loop **in a built player**: the boot self-check hands over and the marker becomes `Title`, `__projectSurviveTitle` carries the two Thai strings the screen drew (heading and เริ่มเล่น) plus the build line, names exactly one control (`start`) inside the canvas, and the control is **at the canvas' centre** — 480 of 960 at the template's own size and 640 of 1280 after the canvas is resized to the GDD's second size, which is asserted by polling the published point so that a document frozen at the old size cannot pass. Then it clicks where the screen said the control is and asserts the game's own answer: `__projectSurviveScene` becomes `Camp` and the camp's report has `flow` Camp, `floor` 1, `canDepart` and a live purse and HP. The two facts that had to change to make this pass are in [10d's report](../stages/10d-report.md) §5 |
| `screens.spec.ts` | 13 | **not a gate**: the screenshot generator, opt-in with `SCREENS=1` (`test.skip(process.env.SCREENS !== "1")`), skipped in a plain run |

### Process-level checks inside the harness → **Fast**

`sim: determinism`, `sim: stream separation` and `sim: bot smoke run` are not test-framework tests: they
run `ProjectSurvive.Sim` as a process and compare its output, which is the only place determinism across
*processes* is observable at all. They stay in Fast because they are cheap (52.3 s cold, 4.0 s warm).

## 4. NOT RUN, and what makes it happen

NOT RUN is never a pass and never silent: `artifacts/test-summary.md` carries the reason per step.

| Situation | Steps affected | Reported |
| --- | --- | --- |
| No Editor connected to this project (`unity status` has no row for `UnityProject`) | `unity: EditMode`, `unity: PlayMode` | NOT RUN with the connection requirement |
| A Unity Editor holds the project lock | `web: WebGL build`, `web: Playwright` | NOT RUN: batch mode cannot share a project |
| `-Suite Fast` | `unity: PlayMode` and both web steps | NOT RUN: "the Full suite was not requested" |
| The build step failed or reported NOT RUN | `web: Playwright` | NOT RUN: there is no player to test |
| A Unity test run produced no result within `-UnityTimeoutSeconds` (default 900) | that Unity step | NOT RUN with the timeout named |
| `node`/`npx` missing, or `tests/web/node_modules` absent | `web: Playwright` | NOT RUN with the missing prerequisite |
| `tools/build-web.ps1` exits 2 (no Editor install, locked project) | `web: WebGL build` | NOT RUN with the script's own error line |

Because the Unity steps need the Editor open and the WebGL build needs it closed, **one Full run cannot
report all three of PlayMode, the build and Playwright.** Two runs do, and both write their own
`test-summary.md`; keep them apart by copying the file (the run of record for this change is
`artifacts/proc10/full-editor-open-summary.md` and `…-closed-summary.md`).

## 5. The slow steps, and the honest way to shorten them

Measured above, in order of what they cost:

1. **`web: WebGL build` — 269 s cold, 16.5 s warm.** The cost is IL2CPP's C++ translation, not the
   build itself: the second Development build of the same code, minutes later in the same cache,
   finished in 16.5 s. Nothing in the working loop needs the player at all, which is why the step is in
   Full; a Release-with-`OptimizeSize` profile is a *different* configuration (not a like-for-like
   swap) and is what `-SizeProfile` builds are for.
2. **`data: broken fixtures rejected` — 48.2 s warm, 52.3 s cold** (60.9 s for the 20 fixtures of
   2026-10-16's run). The step is stable but expensive: it validates every broken fixture set, one
   process each, so it is N × (process start + table load). A single process that walks all of them
   would remove most of it — the tool already has the loop; it spawns per fixture for isolation. This
   is the first thing to fix if Fast must get shorter.
3. **`sim: bot smoke run` and `sim: determinism` — 49.0 s / 36.5 s cold, 2.6 s / 1.3 s warm.** Cold-only
   cost (JIT + first config load); nothing to do.
4. **`unity: EditMode` — 9–14 s.** Already a bridge call into the running Editor, no batch spawn.
5. **`unity: PlayMode` — 42.7 s** for the scene tests. Fine.
6. **`web: Playwright` — 121.4 s** for 8 tests, one worker (2.0 min of browser time, several player
   boots). Parallel workers would break the single-player-at-a-time assumption of the server, and the
   tests drive one canvas.

Cold total for Fast is 229 s and warm is 77 s, so "a few minutes" holds for both.

## 6. The Editor UI capture (no WebGL build)

`ProjectSurvive/Tools/1. Capture UI (Camp + Battle + Result, 1920x1080 + 1280x720)` — or
`-executeMethod ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit` — opens Camp, Battle and the
summary screen (Result) in the Editor, renders each at both sizes into `artifacts/ui/`, measures every
named panel and fails when two that must stand apart share pixels. Measured: 0.9–1.1 s for six images
through the menu item (**1 098 ms** on 2026-10-16 and **1 265 ms** / **1 884 ms** on 2026-10-17 for the ten images it writes now, the Result section included, all three PASS with 0 failures),
and 9 s wall clock for the batch `-executeMethod` form (the Unity launch dominates) — both were run,
and they produced **byte-identical images** and the same rectangles. A third entry point,
`ProjectSurvive/Tools/3. Capture the merge chooser in Play Mode (Camp)`, shoots the Stage 09g
upgrade-choice window, which does not exist in the authored scene at all — see the last bullet below.

- Coverage: **10 images** — `artifacts/ui/{camp,battle,result}-{1920x1080,1280x720}.png` plus the
  closed camp (`camp-closed-*`) and the tooltip state (`camp-tooltip-*`) — and `ui-report.md` /
  `ui-report.json` with every rectangle in pixels **and the alpha of every region**, so the opacity
  floor is a measured column rather than a claim (Board and ChestPanel 0.92, the Result summary column
  0.92, Instructions 0.85, Stats 1, Tooltip 0.95). The summary screen is measured the same way: its 1200×1000 column, its five lines and
  its two 400×76 buttons, eight regions at each size, 0 overlaps — and its rectangles are the ones
  `ResultScreenLayoutTests` judges in the column's own units, so the two checks agree rather than
  duplicate a rule that could drift. What the screen *says* is not in these images: every label in
  `Result.unity` is empty until the runtime fills it from the localization tables. That the measured
  rectangles describe the **live** screen was checked rather than assumed (2026-10-17): the white-text
  bands of a play-mode capture at 1280×720 fall inside the label boxes this report gives (headline
  592–651 in 573–667, floor 535–557 in 531–560, kills 496–516 in 491–520, bia 456–476 in 451–480, HP
  415–436 in 411–440, the two button labels 288–302 / 221–235 in the 269–320 and 203–253 button boxes).
  A `Surface` here carries no background unless the scene author gave it one, and the one that used to
  have none was the summary's column — a bare `RectTransform` shown as one flat colour behind the text.
  It is now authored like the camp's panels (the camp `Board`'s colour at the same **0.92**, which is
  named in `UiLayoutCheck.OpaqueRegions` so a column below the floor is a failed capture rather than a
  note), and its paint is measured in pixels rather than inferred from the component
  (`artifacts/stage10/measure-result-panel.ps1`: 54.9 % of the frame at 1920×1080, 55.0 % at 1280×720,
  22,22,28 inside the column against 13,15,20 outside it).
- The tooltip shot carries a **pixel proof of draw order**: the same frame is rendered with the overlay
  off and on, and five points inside the overlay's rectangle must land closer to the overlay's own
  background colour with it on (measured: 0.016 at four points, 0.188 at the one over a button). It is
  a direction, not a magnitude: the first version asked for a ≥ 0.05 change and failed, because a dark
  panel over a dark panel only moves a pixel by 0.024.
- The overlay is exempt from the overlap rule by design (`UiLayoutCheck` excuses `Overlay` regions only)
  and is itself covered by a test that it never excuses a *real* collision underneath it.
- The rectangles are the screen's own: at 1280×720 the board measures
  `{x: 26.667, y: 213.333, width: 493.333, height: 386.667}`, which is what the browser reported for the
  same screen in Stage 09 — the Editor shot and the player agree.
- What the images do **not** contain: text. Every label in `Camp.unity` is empty until the runtime fills
  it from the localization tables, and the board's cells are built at runtime; `ui-report.md` says so,
  and the PlayMode suite is where that runtime layout is measured. Since Stage 09g the edit-mode report
  also **says** that the merge-chooser window is built at runtime, that it therefore has no rectangle to
  measure without Play Mode, and that entry point 3 is what shoots it — a gap named in the report rather
  than left silent.
- **`ProjectSurvive/Tools/3. Capture the merge chooser in Play Mode (Camp)`** (added at Stage 09g) — the
  window is created at runtime by `CampBagScreenView`, so it is photographed from Play Mode: the run
  opens `Camp.unity`, enters Play Mode, starts the camp, opens จัดย่าม, adds and places a second copy of
  the camp's first item and raises the window through the **real rule** (`DragStart` / `DragMove(Board, …)`
  / `Drop` — the calls the pointer handlers make, so a run that stops offering a choice fails instead of
  photographing an empty panel), then measures, shoots the window open and closed, and judges: the dim
  layer covers the canvas, the window is nested inside it, the cards are pairwise disjoint and in `1 2 3`
  order left to right, the back button is inside the window, every region is inside the canvas, and five
  points inside the window must change when it closes. It writes `artifacts/ui/camp-merge-chooser-<w>x<h>.png`
  plus `...-closed-<w>x<h>.png` and `artifacts/ui/ui-report-playmode.md` / `.json`. It is **interactive
  only** (entering Play Mode needs real frames, and a window that is not in the foreground does not
  render), and **the Game View's own resolution is the layout size** — so the two sizes the GDD names are
  taken by resizing the Game View between runs; for Stage 09g that was the open Game View's
  `UnityEditor.GameView.SetCustomResolution(Vector2, string)`, and the four images the stage quotes
  (`stage09g-chooser-{1920x1080,1280x720}.png` and `stage09g-chooser-closed-{...}.png`) were taken that
  way with the entry point's own steps **by hand**, not by running the menu item.
- What entry point 3 cannot measure, and what Stage 09g measured instead: the view hands back rectangles,
  not labels or images, so the modal's **font sizes** and the dim layer's own alpha are not in its report.
  They were read from the live Editor at 1920×1080 (canvas scaler reference 1920×1080, scale factor 1):
  the window's title 30 px, a card's title 24 px, and every other modal line — ระดับ, the stat changes, the
  hint, the back caption and the left panel's lines — 22 px, the project's own floor; and the dim layer
  is a 0.72 black image that raycasts, which is also what makes the board behind it unclickable.
- Why this mechanism: a canvas in `ScreenSpaceOverlay` is laid out to the game view's size, and the
  Editor cannot be asked for that size from a script without undocumented `GameView` internals. The
  documented alternative — `Screen.SetResolution`, whose Unity 6 scripting reference says *"in the
  Editor, it affects only the Game view's resolution"* — needs Play mode and a game view; rendering the
  canvases into a `RenderTexture` off-screen needs neither, which is also why the batch
  `-executeMethod` form works. That the two agree is measured, not assumed: the board comes out at the
  rectangle the browser published (see above).
- Proof the check can fail: `ProjectSurvive/Tools/2. Capture UI self-test (expected to FAIL)` runs the
  same command with one invented region dropped on the board and writes `ui-report-selftest.md`, which
  reported 4 failures (the pair named at both sizes). Its rule is also unit-tested 8 ways
  (`UiLayoutCheckTests`, in Fast).

## 7. Known flake: a bulk PlayMode run

Twenty of the **40** PlayMode tests failed in the bulk run measured on 2026-10-16 for Stage 10
(`artifacts/stage10/playmode-bulk-status-after-fix.json`): every failure is the same ambient log assert
and **not one is an assertion of ours** — twelve `IndexOutOfRangeException` (Input System) plus eight
`Cached unprocessed value` (R3). The classes Stage 10 added are green in that same bulk payload
(`ResultScenePlayModeTests` 3/3, `SceneFlowPlayModeTests` 5/5 incl. the 20-loop), and the camp and
flow classes are green when the filter is applied (`--filter Camp` → **25/25**, `--filter
SceneFlowPlayModeTests` → **5/5**); see [`artifacts/stage10/playmode-after-fix.md`](../../artifacts/stage10/playmode-after-fix.md).
Re-measured on 2026-10-17 after the `loseNow` fix, the same split holds: bulk **20/40**
(`artifacts/stage10/playmode-bulk-status-followup.json`) and every class green on its own — Camp 25/25,
`ResultScenePlayModeTests` 3/3, `SceneFlowPlayModeTests` 5/5, `BattlePlayModeTests` 4/4,
`ShellPlayModeTests` 4/4 ([`artifacts/stage10/playmode-per-class-followup.md`](../../artifacts/stage10/playmode-per-class-followup.md)).
One further oddity of the payload, recorded rather than explained away: it lists four of
`ShellPlayModeTests`' five `[UnityTest]`s, and the one it drops
(`Thai_spike_scene_measures_both_stacks_without_missing_glyphs`) is dropped from every payload since
Stage 10 without appearing as a failure, so a per-class run is the only thing that reports it — and
running that class on its own on 2026-10-17 still reported **4 results with a summary row of
`total 0`**, so the class' payload is internally inconsistent rather than the bulk assembly alone being
the problem. The five counts come from the result rows, never from the summary.

Stage 10d's own bulk run (2026-10-17, **45** tests, `artifacts/dotnet/unity-playmode-status.json`)
failed **25 of 45** with the same two ambient messages, and it is worth naming what a *cascade* looks
like, because the five `TitleScenePlayModeTests` failures in it read like real defects and are not: a
test that dies on the log assert skips its own teardown, so the next fixture inherits a half-finished
scene transition and reports `the boot flow did not stop at the title ... But was: "Boot"`,
`no RootLifetimeScope is loaded`, or a `NullReferenceException` from a `Find<T>()` that returned null.
Run on its own the same class is **5/5** (and `CampBagDragPlayModeTests` **12/12**,
`CampMergeChooserPlayModeTests` **4/4**, `CampWorldPlayModeTests` **8/8**, `BattlePlayModeTests` **4/4**,
`ResultScenePlayModeTests` **3/3**, `SceneFlowPlayModeTests` **5/5**, `ShellPlayModeTests` **4 reported,
4 passed**) — every class, 45 tests, none failing:
[`artifacts/stage10d/playmode-per-class.md`](../../artifacts/stage10d/playmode-per-class.md), which is
where the per-class evidence for Stage 10d lives.

Sixteen of the 28 PlayMode tests failed in the bulk run measured on 2026-10-16 for Stage 09f
(`unity: PlayMode 16/28`, 12 passed; 12 `IndexOutOfRangeException` + 4 `Cached unprocessed`),
fifteen of 27 on the same day for Stage 09e (`unity: PlayMode
15/27`, 12 passed) and twelve of 24 in every bulk run measured on 2026-10-15(`unity: PlayMode 12/24`), with **no failed assertion**: the failures are Unity's own ambient-message log asserts
(`[Exception] IndexOutOfRangeException` inside `InputSystem.InputManagerStateMonitors` via
`InputSystemUIInputModule.OnEnable`, and `[Error] Cached unprocessed value unexpectedly became
outdated`). The same tests pass individually and Stage 09 recorded 24/24 on the same code — this is the
unsolved item in [`llmwiki/stage-log.md`](../../llmwiki/stage-log.md) ("The bulk PlayMode suite fails on
ambient Input System log-asserts"). The bulk suite was **not** re-run for Stage 09g (which took the
count to 33: one more in `CampBagDragPlayModeTests` plus the four chooser cases); the two camp fixtures were run on their own through the open Editor and were green —
`CampBagDragPlayModeTests` **12/12** and the new `CampMergeChooserPlayModeTests` **4/4** — which is the
same per-class check this section asks for, and is not a claim about the suite as a suite.

The harness reports it as **FAIL**, because that is what it is: the suite, run as a suite, does not pass.
Do not read it as a pass, and do not read it as a regression in whatever was just changed either — check
the artifact (`artifacts/dotnet/unity-playmode-status.json`, which carries every test's status, duration
and message) and, for a specific class, run it on its own:

```powershell
unity command run_tests --mode playmode --async_tests --filter CampBagDragPlayModeTests
unity command test_status --result-only
```

The same note also records that `run_tests` can answer with the **previous** run's payload (identical
per-test durations). The harness guards against it: it polls `test_status` and only accepts a result that
either reported `running` first or differs from the payload captured before the submit, and it writes the
raw payload to `artifacts/dotnet/unity-<mode>-status.json` so the durations can be checked.
