# Stage 08c — the battle HUD rearranged (Presentation only; the mini ย่าม)

- **Stage:** 08c (a follow-up to Stage 08, landed after 08b — it is 08b's first consumer).
- **Date:** 2026-10-17
- **Decision record:** none. The brief fixes the layout region by region, so there was no ambiguous
  design choice left to record (AGENTS rule 12); the two rules that are *not* the brief's — where a
  panel's rect comes from and what a plate's tone means — are documented on the code that owns them
  (`BattleMiniBagLayout`, `BattleMiniBagView`).
- **Scope, from the brief:** *ทำ Stage 08c เท่านั้น: จัด HUD ของ Battle ใหม่ตามแบบเกมอ้างอิง
  (Presentation ใช้ข้อมูลจาก 08b) … ห้ามแก้ Core และ Simulation เพิ่มได้เฉพาะแถว localization*.
  No file under `lib/Runtime/Domain/`, `lib/Runtime/Simulation/` or `lib/Runtime/Application/` was
  edited: the only `lib/` change in this stage is `lib/Tests~/Core.Tests/ConfigLoadTests.cs`, whose
  pinned row count follows the four new localization rows (§1.6).

---

## 1. What was delivered

The battle screen was one column of labels on the left and a wall of text on the right. It is now six
anchored panels, each named for the reader of the picture: the mini ย่าม, the floor, the run's numbers,
the figure with its two bars, the timeline, and the dev column.

1. **The mini ย่าม, top left, read-only.** It draws **only the bounding box of the cells the bag
   actually covers** — not the whole 9 × 7 board — as a grid whose cell size is computed to fit the
   panel, and the panel is at most a quarter of the canvas wide by construction (`anchorMax.x = 0.25`:
   432 × 546 px at 1920 × 1080, 288 × 364 at 1280 × 720). The bag is the background, each piece is a
   box coloured by its `item.category` with its tier number on it. Geometry comes from the
   `BagSnapshot` and the `CombatLoadoutSnapshot` the run already hands the battle; the view recomputes
   no rule — it does not decide where a footprint fits, it draws where the board already put it.
2. **The plate.** A piece that can cast wears a dark plate covering `RemainingTicks / PeriodTicks` of
   its box, read from `IBattleFrame.Cooldowns` (08b) of the snapshot the runner copied in the same
   frame. Ready-but-the-pool-cannot-pay is a faint blue tone (`LoadoutCooldownFlags.ManaShort`), and a
   ready entry wears **no** plate at all — "no plate" and "plate at 0 %" are the same picture, so the
   tone is what separates them.
3. **Built once.** Every cell, piece box, plate and label is created at floor start and given a
   background sprite generated in code (AGENTS rule 13 — no binary art, nothing copied from the
   reference game). During play the view writes only values that changed, and it counts those writes
   (`PlateUpdates`) so a test can tell "the floor moved the plates" from "the view redrew everything".
   No `Instantiate` per frame and no string concatenation per frame.
4. **The rest of the screen.** The floor's name and number moved from the top left to the top centre;
   time left, bia earned, enemies slain, the wave cursor, the boss band and the 1×/2× control stay
   together at the top right; the figure, the HP bar (red) and the MP bar (blue) with current/max
   numbers moved into a bottom-left **status panel**; the dev menu became a **single column** at the
   bottom right, and the slim timeline stays along the bottom middle between the status panel and the
   dev column.
5. **The equipped-spell list is gone** (*เอาข้อความรายการ คาถาที่ติดตั้ง ออก*) — the mini ย่าม says which
   pieces are equipped and which of them can cast. The synergy link lines are deliberately **not**
   drawn this round, per the brief.
6. **Strings and the pin.** Four rows were added to `data/localization/localization.csv`
   (`hud.kills`, `hud.bag`, `hud.status`, `hud.floor.format`) and every new caption resolves through
   the same localization service as the rest of the screen; every label is ≥ 22 px at 1080p. The
   config load test's pinned localization count moved **188 → 192** with them — the pin exists so a
   table that silently loses rows fails there, and this is the third time it has been corrected for
   the reason it was written for.
7. **The overlap tool now judges the battle screen.** `UiLayoutCheck` gained the battle HUD's six
   surfaces (`MiniBagPanel`, `FloorPanel`, `ResourcePanel`, `StatusPanel`, `TimelinePanel`, `DevPanel`)
   and the controls inside them (the bars, the track, the speed control, the mini bag's grid, the dev
   rows), so the capture command asks the same "may these two share pixels" question of the battle
   screen that it asks of the จัดย่าม panel.
8. **The defect this stage's own test found, and it was a real one.** `BuildBattleHud` created the
   `BattleMiniBagView` component and set its own serialized fields, but nothing ever assigned it to
   `BattleViewSettings.MiniBag` — so in the real scene `runner.View.MiniBag` was **null**, the bag was
   never built, and the panel drew its caption over an empty background forever. It compiled, the
   EditMode suite was green, and the capture shots looked right (that command finds the component by
   type and builds it itself). Only a PlayMode assertion that goes through the scene's own wiring can
   catch this class of defect, and both new PlayMode assertions do: they failed with *"the scene wired
   no mini bag"* on the first bulk run. The fix is one `out` parameter on `BuildBattleHud` and one
   `SetObjectReference(runner, "_viewSettings.MiniBag", miniBag)`, followed by regenerating
   `Battle.unity` through `ProjectSurvive/Stage 08/3. Create Battle scene` — the scene now carries
   `MiniBag: {fileID: 86127954}` and the class is 5/5.

## 2. Files that changed

| File | What |
| --- | --- |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/Hud/BattleMiniBagLayout.cs` | **new** — the read-only bag's geometry: bounding box, cell size, panel fit, which box each piece occupies. Pure arithmetic, no `UnityEngine` transform math beyond rects the view hands it |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/Hud/BattleMiniBagView.cs` | **new** — builds the cells/pieces/plates once, writes only changed values, counts the writes (`CreatedAfterBuild`, `PlateUpdates`) |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/Hud/BattleHudArt.cs` | **new** — the generated sprites and the category palette |
| `UnityProject/Assets/Main/Scripts/Editor/BattleCaptureState.cs` | **new** — the state the capture tool hands the HUD for the three battle shots (a real run's bag, a mid-fight frame, and the one authored `ManaShort` flag) |
| `UnityProject/Assets/Main/Scripts/Editor/ProjectSetup.cs` | the six panels, the mini bag's wiring (`out BattleMiniBagView` + `_viewSettings.MiniBag`), the dev menu as one column, the timeline's new bounds |
| `UnityProject/Assets/Main/Scripts/Editor/UiLayoutCheck.cs` | the battle HUD's surface and control region names |
| `UnityProject/Assets/Main/Scripts/Editor/UiCaptureTool.cs` | the battle shots: a running floor, the three states, the battle regions |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/BattleViewScene.cs` | builds and plates the mini bag from the frame snapshot; swaps it on a panel resize |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/BattleViewSettings.cs` | `MiniBag` |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/Hud/BattleHudPresenter.cs`, `BattleHudState.cs`, `BattleHudView.cs` | the new labels (kills, bag, status, the floor number's format) and the removed spell list |
| `UnityProject/Assets/Main/Scripts/Presentation/Battle/BattleFrameSnapshot.cs`, `BattleSimulationRunner.cs`, `BattleRunnerContext.cs`, `BattleViewCaps.cs`, `Battle/BattleSceneDirector.cs` | the snapshot the view reads: cooldowns copied with the frame, the caps counters extended |
| `UnityProject/Assets/Main/Scenes/Battle.unity` | regenerated by the Stage 08 menu item — the six panels, the wiring, the dev column |
| `data/localization/localization.csv` | 4 `hud.*` rows |
| `lib/Runtime/Generated/Luban/ConfigBuildMetadata.g.cs` | regenerated by `tools/gen.ps1` (the hash moved with the data) |
| `lib/Tests~/Core.Tests/ConfigLoadTests.cs` | the pinned localization row count 188 → 192 |
| `UnityProject/Assets/Main/Tests/EditMode/BattleMiniBagTests.cs` | **new** — 14 tests: the plate's arithmetic over hand-made frames, the layout's bounding box and cell fit, the tones, the update-on-change rule |
| `UnityProject/Assets/Main/Tests/EditMode/BattlePresentationTests.cs` | the new labels through the presenter |
| `UnityProject/Assets/Main/Tests/PlayMode/BattlePlayModeTests.cs` | two additions: the mini bag is built and creates nothing after that; the plates are the frame's cooldowns on the loadout's pieces and they move with the floor |
| `docs/testing/test-suites.md`, `llmwiki/systems/battle-presentation.md`, `llmwiki/stage-log.md`, `llmwiki/index.md`, `llmwiki/systems/loop-and-scopes.md` | the counts and the new system note |

## 3. Commands that were run

```powershell
unity-cli recompile                                        # after every C# edit; 0 errors each time
unity-cli menu "ProjectSurvive/Stage 08/3. Create Battle scene"   # regenerate Battle.unity (Editor API)
grep -n "MiniBag" UnityProject/Assets/Main/Scenes/Battle.unity    # MiniBag: {fileID: 86127954}
unity-cli menu "ProjectSurvive/Tools/1. Capture UI (Title + Camp + Battle + Result, 1920x1080 + 1280x720)"
unity command run_tests --mode playmode --filter BattlePlayModeTests --filter_type testName --async_tests
unity command run_tests --mode playmode --async_tests       # the bulk assembly run, for the flake record
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
```

## 4. Results, and where the evidence is

| Check | Result | Evidence |
| --- | --- | --- |
| Editor compile | **0 errors, 0 new warnings** | `unity-cli recompile` → `completed`, `compilationFailed: false`; the console's error count is 0 after the scene regeneration |
| `dotnet test` (Fast) | **481 passed** | `artifacts/stage08c/fast-after-minibag-fix.log` (first run after the data change: 480 passed, 1 failed — the pin; then 481) |
| `unity: EditMode` | **144/144** | same log; per class: `BattleMiniBagTests` 14, `BattlePresentationTests` 15 |
| `unity: PlayMode`, the battle class | **5/5** (9.8 s) | `artifacts/stage08c/playmode-battle-class.json` — the two new tests included |
| `unity: PlayMode`, the whole assembly | 20/46, **every failure the documented ambient-log flake** | `artifacts/stage08c/playmode-bulk-after-minibag-fix.json`: 12 `IndexOutOfRangeException` (Input System), 8 `Cached unprocessed value` (R3), and the cascade those two cause when a test dies before its teardown (3 `NullReferenceException`, 1 `no RootLifetimeScope is loaded`, 1 `the boot flow did not stop at the title`). **Not one is an assertion of ours**, and the battle class is 5/5 inside that same bulk payload |
| `tools/test.ps1 -Suite Fast` | **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0**, 88.1 s | `artifacts/stage08c/fast-after-minibag-fix.log` |
| UI capture, both sizes | **PASS, 0 failures**, 1753 ms, 16 images | `artifacts/ui/ui-report.md` / `.json`; 36 measured battle regions per state per size, **0 overlaps** at 1920 × 1080 and at 1280 × 720 |
| The battle shots are real | the mid-fight shot is **tick 180 with 2 casters at 2 distinct cooldown levels**, and the bag in the picture is the run's own bag | `artifacts/ui/battle-{begin,midfight,manashort}-{1920x1080,1280x720}.png` and the report's notes |
| The mini bag's measured panel | **432 × 546** at 1920 × 1080 and **288 × 364** at 1280 × 720 (22.5 % of the canvas width at both) | the capture report's region list |
| The scene really wires it | `runner.View.MiniBag` is not null and `IsBuilt` is true **in the scene** (not in a hand-built harness) | `The_mini_bags_plates_are_the_frames_cooldowns_on_the_loadouts_pieces`, `Ten_seconds_of_ticks_raise_no_exception_and_bind_no_new_view` |
| The pool still holds | `viewsGrownDuringPlay = 0` over ten seconds of ticks, and the mini bag's own `CreatedAfterBuild = 0` | the same test, plus the published report (`viewsCreated: 1793`, `viewsBound: 7`, `viewsGrownDuringPlay: 0` at tick 300) |

## 5. Not tested, and why

- **No WebGL build and no browser run.** The brief says so explicitly (*ไม่ต้อง build Web ระหว่างทำงาน*),
  and the Full suite's web steps need the Editor closed, which the working loop does not. They report
  **NOT RUN** with that reason; the battle screen's Web player behaviour is unverified by this stage.
- **No frame-cost measurement of the mini bag.** The brief's promise is structural — elements built
  once, updated only when a value changes, no per-frame `Instantiate`, no per-frame string
  concatenation — and that is what the tests assert (`CreatedAfterBuild == 0` over a running floor,
  `PlateUpdates` only rising when the frame moves). What was **not** measured is the milliseconds the
  mini bag costs per frame on the target browser; measuring it needs a built player (ADR 0003), which
  this stage did not produce. No number is quoted for it.
- **`ManaShort` on a real floor 1.** Neither session reaches the flag on floor 1 (the dev session's MP
  pool never falls below a single cast's cost), so the capture's third shot is the one **authored**
  state — a real `LoadoutCooldown` carrying the flag, handed to the real view — and the picture proves
  the tone is visible at both sizes. That the flag reaches the plate is proved by `BattleMiniBagTests`
  and by the PlayMode test's per-entry tone assertion, but that assertion sees `None`/`Counting` on
  floor 1; **a floor that is genuinely MP-starved was not played**.
- **No synergy link lines** and **no editing of the bag during a battle** — both out of scope by the
  brief, not oversights.

## 6. Blockers, risks, and two things worth naming

- **A scene reference is not compiled.** The defect in §1.8 is invisible to the compiler, to the
  EditMode suite and to the capture command (which builds the bag itself). The only guard is a test
  that runs the real scene, which is why the two new assertions sit in PlayMode and why one of them
  asserts the *scene's* wiring before it asserts any plate. This is a risk for any future view that is
  authored into a scene: the wiring is data, and data needs a test that reads the data.
- **The bulk PlayMode run stays red, and that is the documented flake.** 20/46 this run, 19/46 before
  the fix, 20/40 and 25/45 in Stage 10's runs: the shape is the Input System's ambient
  `IndexOutOfRangeException` log assert and R3's `Cached unprocessed value`, with the cascade they
  cause. It is recorded in `docs/testing/test-suites.md` §7 and in this stage's artifact rather than
  explained away, and the per-class run is the measurement that is quoted.
- **The plate is one number per caster, and the view never predicts.** If a future stage wants a
  "about to cast" pulse or a per-target readout, it must come through `IBattleFrame` (08b decided
  that); a view-side clock is exactly the second battle the contract exists to prevent.

## 7. Acceptance, item by item (from the brief)

| # | Acceptance | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Mini ย่าม top left, read-only, **the bag's bounding box only** (not the whole 9 × 7 board), cells fitted inside ≤ ¼ of the canvas width, bag as background, pieces as category-coloured boxes with their tier number | **PASS** | `BattleMiniBagLayout` + `BattleMiniBagView`; `BattleMiniBagTests` (14) over the box and the fit; the capture report's `MiniBagPanel` 432 × 546 / 288 × 364 rect and the picture itself |
| 2 | Geometry from the same run/loadout the battle is given — no rule recomputed in the View | **PASS** | `BattleViewScene.Build` reads `BagSnapshot` + `CombatLoadoutSnapshot`; the layout decides no footprint and no legality |
| 3 | Casters wear a dark plate that shrinks with the remaining time, MP-short wears a faint blue tone, ready wears none | **PASS** | `BattleMiniBagTests` (the arithmetic and the three tones); the PlayMode test asserts the plate is `RemainingTicks / PeriodTicks` **and** the tone expected from every entry of a live frame; the capture's three shots |
| 4 | Elements built once (pool), no per-frame `Instantiate`, no per-frame string concat, updated only when a value changes | **PASS** | `CreatedAfterBuild == 0` over a running floor (PlayMode) and the build-once tests (EditMode); `PlateUpdates` counts the writes |
| 5 | No synergy link lines this round | **PASS** | not drawn, by the brief |
| 6 | Floor name and number to the top centre; time, bia, kills, wave and the speed control stay at the top right | **PASS** | the six panels in `ProjectSetup` (Editor API, AGENTS rule 11) and the capture's region list at both sizes |
| 7 | Status panel bottom left: placeholder portrait, HP (red) and MP (blue) bars with current/max — the bars moved there from the top left | **PASS** | `UiLayoutCheck.StatusPanel` / `HpBar` / `ManaBar`; the capture measures them inside the panel |
| 8 | Dev menu one column, on its own canvas, not over the timeline and not over the HP panel | **PASS** | `DevPanel` on the 200-sorting-order canvas; the capture's dev-menu regions and 0 overlaps |
| 9 | The equipped-spell list text is removed and the timeline does not collide with anything | **PASS** | the list is gone from `BuildBattleHud`; the capture reports 0 overlaps over 36 battle regions at both sizes |
| 10 | Thai through localization, ≥ 22 px at 1080p; R3 carries UI-level state only; the per-item cooldown read from the frame snapshot copied in the same frame, no per-entity subscription | **PASS** | the 4 `hud.*` rows and the presenter's lookups; the PlayMode test reads `runner.View.Snapshot` in the same block as the plates; `SubscriptionCount` is constant over ten seconds of ticks |
| 11 | Evidence images in `artifacts/ui/` at both sizes: floor start, mid-fight with several cooldown levels, and MP-short; the battle regions added to the existing overlap tool | **PASS** | `artifacts/ui/ui-report.md` (16 images, **PASS**, 0 failures); the mid-fight shot is tick 180 with 2 casters at 2 cooldown levels; the MP-short shot is authored (§5) and says so in the report |
| T1 | PlayMode: the frame's cooldown values make the dark plate change proportionally | **PASS** | `The_mini_bags_plates_are_the_frames_cooldowns_on_the_loadouts_pieces` — every entry's plate equals `RemainingTicks / PeriodTicks` on a live floor, and the tracked plate moved after 12 more ticks |
| T2 | PlayMode: the mini ย่าม matches the loadout | **PASS** | same test: `CasterCount == snapshot.CooldownCount`, every caster carries a plate, `PieceCount ≥ CasterCount` |
| T3 | PlayMode: 10 seconds of battle with no exception and `viewsGrownDuringPlay = 0` as before | **PASS** | `Ten_seconds_of_ticks_raise_no_exception_and_bind_no_new_view`, now also asserting the mini bag was built and created nothing during the floor |
| A1 | No overlaps at either size | **PASS** | the capture's 0 failures over 36 battle regions per state per size |
| A2 | The tests pass | **PASS** | `dotnet` 481/481, EditMode 144/144, PlayMode battle class 5/5, `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0** |
| A3 | A short PASS/FAIL/NOT RUN report | **PASS** | this file |

**NOT RUN (named, not assumed):** the WebGL build and the Playwright suite — the brief does not ask for
a Web build during work, and both are Full-suite steps that need the Editor closed. The bulk PlayMode
assembly run is **reported as 20/46 with the flake named in §4**, not as a pass.

**Stage verdict: PASS** on every acceptance item, with the three limitations in §5 recorded as
limitations: no browser run, no frame-cost measurement, and no genuinely MP-starved floor.
