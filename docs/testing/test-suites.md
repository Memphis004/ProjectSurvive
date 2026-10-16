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
| `dotnet test (xUnit)` | Fast | 466 tests: the rule engine, the architecture guards, the contract suites, the Sim |
| `sim: determinism` | Fast | the same seed prints the same `determinismDigest` twice, a different seed does not |
| `sim: stream separation` | Fast | shop / battle / visual digests differ, `ticksProduced=300` |
| `sim: bot smoke run` | Fast | the kiting bot plays 2 seeds and writes balance rows |
| `data: generated tables validate` | Fast | `tools/validate-data.ps1` over the real tables |
| `data: generated code reproducible` | Fast | `tools/verify-generated.ps1` — `data/` + `proto/` reproduce the tracked output |
| `data: broken fixtures rejected` | Fast | `tools/check-fixtures.ps1` — 20 broken data sets rejected for the right reason |
| `unity: EditMode` | Fast | 115 tests in `ProjectSurvive.Game.Tests.EditMode` |
| `unity: PlayMode` | **Full** | 40 tests in `ProjectSurvive.Game.Tests.PlayMode` (scenes, pooling, pointer drags, walking, the upgrade-choice window, the three scopes and the 20-round-trip loop) |
| `web: WebGL build (Development)` | **Full** | `tools/build-web.ps1 -Configuration Development -Force` |
| `web: Playwright (smoke + battle + camp + result)` | **Full** | 9 browser tests against that build, in Chrome (13 more are the opt-in screenshot generator, skipped unless `SCREENS=1`) |

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
| `web: Playwright (smoke + battle + camp + result)` | 121.4 (2026-10-16: pending, 9 tests) | 9 tests, 1 worker, Chrome channel |

`unity: EditMode` measured 9.2–13.6 s across the five runs above and `dotnet test` 2.2–2.7 s; the
numbers quoted per run are the runs of record, not averages.

## 3. Every test, and the suite it belongs to

Nothing was dropped when the suites were split: this table lists every test that existed before the
split and where it runs now. "Fast" means the working loop, "Full" the closing one; a test cannot be in
neither.

### dotnet — `lib/Tests~/ProjectSurvive.Core.sln` (466 tests) → **Fast**

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
| `Battle/*.cs` | the battle contract suite over the fake and the real ECS session, the frame buffer, the event ring. Includes `Dev_lose_now_reports_the_death_it_stands_for` (both factories, 2026-10-17): the cheat spends the player's HP before it finishes, so a forced loss is `PlayerDied` with `FinalHp` 0 rather than a defeat at full health |
| `Inventory/*.cs` | Board/Backpack/BackpackEditor/Merge/Recycle/Rotation/Synergy/Loadout + the randomized bag tests |
| `Run/*.cs` | RunSession economy and flow, shop stock, the drop preview, replay |

### Unity EditMode — `ProjectSurvive.Game.Tests.EditMode` (115 tests) → **Fast**

| Class | Tests | What it covers |
| --- | --- | --- |
| `ShellSmokeTests` | 12 | the 7-probe smoke suite, config hash, R3/UniTask/serialization in the Editor |
| `BattlePresentationTests` | 15 | the battle HUD/state/presenter seam without a scene |
| `CampPresenterTests` | 22 | every camp command through the presenter, the preview/command agreement, the 0-byte drag, the drawn-link kinds, and (Stage 09g) the upgrade-choice window: dropping a matching pair opens it with nothing changed, the cards are the offer's own rows in order with their data lines, the left panel is the tooltip's own five lines, confirming raises the tier in the same cell and reaches the loadout, cancelling changes nothing and reopening offers the same cards, a pair with no choices still folds on the drop, and the board is locked while the window is up |
| `CampTooltipPlacementTests` | 4 | the tooltip stays on screen, flips at the edges and never covers the pointer |
| `RunFlowEditModeTests` | 4 | the run flow against the shipped `Resources` tables |
| `WebSizeProfilesTests` | 8 | the size profiles and their recorded configuration |
| `BackpackRulesTests` | 10 | the bag rules as the Editor sees them |
| `UiLayoutCheckTests` | 19 | the overlap rule the UI capture command applies, the overlay exemption, the opacity floor and (Stage 09g) the modal rules: a window may cover the panel it is asking about, two cards sharing pixels are still reported, a card inside its window passes and one outside it does not; includes the deliberate-overlap case |
| `DevCheatTests` | 8 | **Stage 10c** — `DevCheats.Allowed` is false until the root decides it and false in a release player, `Reset` puts it back; every dev command through the real ECS session: a floor with cheats off refuses each one with `RejectedNotAllowed` and changes nothing (HP, bia, entity count, phase), `DevGodMode` only takes 0/1, `DevAddBia` only takes a positive amount, and a refused command leaves the menu's own toggle telling the truth |
| `ResultScreenTests` | 7 | **Stage 10b** — the summary a cleared run and a lost one produce (headline, floor reached, ghosts, bia, HP), a run that has not ended, the strings coming through the localization table, one way out at a time, `ReturnToTitle` being the run's own transition, and every `result.*` row existing in both languages |
| `RunScopeLifetimeTests` | 3 | **Stage 10a** — the run is created once and replaced only by its owner's disposal, a run after a disposal is a new one and not the old object, and a scene scope inherits the App scope's run instead of building its own |
| `ResultScreenLayoutTests` | 3 | **Stage 10b** — the Result scene read from disk and judged with `UiLayoutCheck`: the seven regions are pairwise disjoint and inside the summary column, every label is ≥ 22 px in a ≥ 32 px line box, and the reading order (headline → lines → restart → title) holds. Measured rectangles in [`artifacts/stage10/result-layout.md`](../../artifacts/stage10/result-layout.md) |

### Unity PlayMode — `ProjectSurvive.Game.Tests.PlayMode` (40 tests) → **Full**

| Class | Tests | What it covers |
| --- | --- | --- |
| `BattlePlayModeTests` | 4 | the battle scene starts, ticks, re-enters and disposes |
| `CampBagDragPlayModeTests` | 12 | the จัดย่าม screen driven by real pointer events, plus the tooltip's draw order, the two-name rule and the line/badge kinds |
| `CampMergeChooserPlayModeTests` | 4 | **Stage 09g** — the upgrade-choice window through real pointer/keyboard events: drag onto the twin opens it (measured geometry: the dim covers the canvas, the cards do not overlap), a tap on the second card upgrades in the same cell with that card's variant id, `Escape` and the back control close it with the run's hash byte-identical and the same cards offered again, the board behind it stays locked and no tooltip is drawn, and `2` picks the second card |
| `CampWorldPlayModeTests` | 8 | WASD walking, the walk clamp, `E` at a shelf, clicking without walking |
| `SceneFlowPlayModeTests` | 5 | **Stage 10a** — the whole loop through the real scenes: the purse, bag and HP that went into the arena come back out of it (same `RunSession` object, same seed), a settled floor writes `BattleResult.FinalHp` back into the run and heals only what the run's own settlement heals, leaving the arena disposes its `IBattleSession`, arriving at the title leaves no run scope and no scene scope behind, the loader refuses a second transition and a scene that is not built, and **20 camp/battle round trips** leave exactly one scene scope and one run scope each time and grow the managed heap by ≤ 3 MiB (measured: 0.70 MiB; [`artifacts/stage10/loop-memory.md`](../../artifacts/stage10/loop-memory.md)) |
| `ResultScenePlayModeTests` | 3 | **Stage 10b** — a lost floor lands on the summary screen and **Restart** starts a new run at floor 1 with the starting purse and an empty bag, a run can be won floor by floor and ends on the victory summary, and leaving the summary for the title leaves no run behind |
| `ShellPlayModeTests` | 5 | scene loading, the title, the Thai spike's two stacks, UniTask cancellation in play mode. **Note:** the assembly's bulk payload reports four of these five — `Thai_spike_scene_measures_both_stacks_without_missing_glyphs` was absent from the 40-result payload of every Stage 10 run while the file still declares it; run the class on its own to see it (see §7) |

### Playwright — `tests/web` (9 gate tests) → **Full**

| Spec | Tests | What it covers |
| --- | --- | --- |
| `smoke.spec.ts` | 2 | the player boots, passes its own 7-probe self-check, hands over to Title; the Thai spike's glyph coverage |
| `battle.spec.ts` | 2 | floor 1 played to a win or a loss in a real browser; a mid-floor report. Since Stage 10 the first test also rides the handover that follows: a **won** floor does not end the run, so the arena carries it back to the camp and the camp's own report has to be the same run — `__projectSurviveScene` becomes `Camp`, `floor` is 2 (a new run would be 1), `hp` is the arena's `finalHp`, `bia` is the run's purse and `canDepart` is true |
| `camp.spec.ts` | 4 | the จัดย่าม flow end-to-end, the sala's controls, WASD + `E`, the layout at 1280×720 and 1920×1080. Its merge step presses `1` to pick the first card of the Stage 09g window after the drop (the camp report publishes no card rectangle, so the key path is the honest one) — that edit was unverified when it landed in 09g and **is verified now**: the Stage 10 Full run with the Editor closed ran this spec **4/4** against the Development player (`artifacts/stage10/full-suite-editor-closed.log`) |
| `result.spec.ts` | 1 | **the last leg of Stage 10's loop in a built player** (2026-10-16): `?scene=Battle`, the dev menu's แพ้ทันที / lose now pressed through the centres `__projectSurviveDevMenu` publishes, then the handover to the summary screen, where `__projectSurviveResult` has to agree with the arena's own last report (`kills`, `bia`, `hp` = `finalHp`, `maxHp`), carry non-empty Thai lines with the run's numbers in them, and name both controls with a point inside the canvas. The player then presses **Restart** — the control the scene drew — and the camp has to come up with a run that is new: `flow` Camp, `floor` 1, `canDepart`, `hp` = the dead run's `maxHp`. Needs a Development build (that is what a dev menu means); a Release build publishes no menu document at all. Measured in a real player (2026-10-17, Full, Editor closed): the step reports **9 passed in Chrome, 140.0 s**, this spec 1/1 of them — after the `loseNow` fix below, and after its own second assertion was corrected (it compared the summary's *purse* with the arena's earned-currency counter; the two are different numbers and the spec now says so) |
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
through the menu item (**1 098 ms** on 2026-10-16 and **1 265 ms** on 2026-10-17 for the ten images it writes now, the Result section included, both PASS with 0 failures),
and 9 s wall clock for the batch `-executeMethod` form (the Unity launch dominates) — both were run,
and they produced **byte-identical images** and the same rectangles. A third entry point,
`ProjectSurvive/Tools/3. Capture the merge chooser in Play Mode (Camp)`, shoots the Stage 09g
upgrade-choice window, which does not exist in the authored scene at all — see the last bullet below.

- Coverage: **10 images** — `artifacts/ui/{camp,battle,result}-{1920x1080,1280x720}.png` plus the
  closed camp (`camp-closed-*`) and the tooltip state (`camp-tooltip-*`) — and `ui-report.md` /
  `ui-report.json` with every rectangle in pixels **and the alpha of every region**, so the opacity
  floor is a measured column rather than a claim (Board and ChestPanel 0.92, Instructions 0.85, Stats 1,
  Tooltip 0.95). The summary screen is measured the same way: its 1200×1000 column, its five lines and
  its two 400×76 buttons, eight regions at each size, 0 overlaps — and its rectangles are the ones
  `ResultScreenLayoutTests` judges in the column's own units, so the two checks agree rather than
  duplicate a rule that could drift. What the screen *says* is not in these images: every label in
  `Result.unity` is empty until the runtime fills it from the localization tables. That the measured
  rectangles describe the **live** screen was checked rather than assumed (2026-10-17): the white-text
  bands of a play-mode capture at 1280×720 fall inside the label boxes this report gives (headline
  592–651 in 573–667, floor 535–557 in 531–560, kills 496–516 in 491–520, bia 456–476 in 451–480, HP
  415–436 in 411–440, the two button labels 288–302 / 221–235 in the 269–320 and 203–253 button boxes).
  A `Surface` here carries no background unless the scene author gave it one: the summary's column is a
  bare `RectTransform`, which the capture shows as one flat colour behind the text.
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
(`Thai_spike_scene_measures_both_stacks_without_missing_glyphs`) is dropped from every Stage 10 payload
without appearing as a failure, so a per-class run is the only thing that reports it.

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
