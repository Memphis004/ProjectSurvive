# The full loop: three scopes, five screens, one run (Stage 10)

Stage 10 is the first time the game plays end to end: **Boot → Title → Camp → Battle (floor 1) →
Camp → … → floor 5 → Result**, and back out to the Title. Nothing new is simulated — Stage 07's
battle and Stage 09's camp are unchanged — what this stage adds is the thing that was missing
between them: **where a run lives**, and what happens when one ends.

| Piece | File | What it owns |
| --- | --- | --- |
| App scope | `Assets/Main/Scripts/Presentation/RootLifetimeScope.cs` | the shell: config, localization, clock, `SceneLoader`, `RunScopeOwner`. Persists across scenes (`SceneBoot.PersistAcrossScenes`) |
| Run scope | `Assets/Main/Scripts/Presentation/Flow/RunLifetimeScope.cs` (+ `RunScopeOwner`) | exactly one `RunSession` — wallet, bag, shop, floor cursor, flow phase |
| Scene scope | `BattleSceneScope` / `CampSceneScope` / `ResultSceneScope` | that scene's presenters, UI state, and (Battle) the `IBattleSessionFactory` |

The rule that makes the loop work is **a child scope never re-registers the shell, and a scene never
creates a run**. `RootLifetimeScope.Configure` returns early when `Parent is not null`, so a scene
scope's `Resolve<RunSession>()` comes from the App scope's run scope — the same object the camp
spent from and the battle drained. Before Stage 10a the camp built its own run and the battle built a
throwaway one, which is exactly the bug ("ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene") this stage removes.

## 1. Leaving a scene is one place

`Game.Presentation.Flow.SceneLoader` is the only way a scene changes: one transition at a time,
behind a fade, with a typed refusal (`transition.in_progress`, `transition.scene_missing`,
`transition.load_failed`, `transition.disposed`) instead of a half-applied move. Two behaviours in it
are load-bearing:

* **A scene that is not in the build cannot be loaded** — checked *before* the fade, so a refusal
  leaves the player looking at the screen they were on. (`ShellPlayModeTests` exercises the typed
  refusal with an id that has no scene behind it.)
* **Arriving at `SceneId.Title` disposes the Run scope.** "กลับ Title แล้ว dispose Run scope" is a
  property of the transition, not something each screen remembers — which is what makes
  "Title ไม่มี Run scope เก่าค้าง" a one-line assertion.

A *director* asks for a departure after it has committed the domain change, never before: the battle
settles the floor and only then asks; the summary screen records the player's intent and only leaves
once the loader accepted it.

## 2. The floor loop

`BattleSceneDirector` plays exactly one floor and settles it:

1. the floor comes from `RunSession.PendingSetup` — the camp already validated the departure, so the
   arena never rebuilds a run or re-resolves a loadout;
2. the floor ends (`Won` / `Lost` / `Aborted`) and `ApplyBattleResult` writes HP, bia, consumables,
   the kill total and the 15 % clear-heal into the run;
3. the settlement decides the destination: `Result` when the run is over (a floor it did not clear,
   or its last one), `Camp` otherwise.

`BattleSetup.CurrentHp` in and `BattleResult.FinalHp` out is the whole HP contract; the heal belongs
to the run, not the battle.

**The departure is asked for repeatedly, and that is deliberate.** A floor can end while a transition
is still in flight (a surrender in the first frames, which is what the PlayMode loop does), and the
floor is already settled — nothing else will ever ask again — so `TryLeave` retries every frame and
logs the first refusal only. A single attempt would strand the player in a finished arena.

## 3. The Result screen

`Assets/Main/Scenes/Result.unity` (built by `ProjectSurvive/Stage 10/6. Create Result scene`) shows a
`RunSummary`: headline (ชัยชนะ / พ่ายแพ้), floor reached, ghosts defeated, bia left, HP left, and two
ways out.

* The numbers come from `RunSession.Summary()` — a plain read over run state, with `GhostsDefeated`
  summed per settled floor because no floor outlives its battle.
* Every string is a `result.*` row in  `data/localization/localization.csv` (192 rows now) resolved by
  `ResultPresenter`; the view mirrors and forwards, and computes nothing.
* **Restart is not a command on the run.** It records the intent, is committed by the loader, and
  `RunScopeOwner.DisposeRun()` is what makes the camp start a brand-new run (floor 1, starting purse,
  empty bag). Back to Title performs `RunSession.ReturnToTitle()` and lets the transition dispose the
  run.

The scene is 1200×1000 centred column: 96 px headline, 32 px lines 60 px apart, two 400×76 buttons —
all above the 22 px Thai floor, measured in `artifacts/stage10/result-*.png` and again, as rectangles at
both GDD sizes, in `artifacts/ui/result-{1920x1080,1280x720}.png` + `artifacts/ui/ui-report.md`
(`ProjectSurvive/Tools/1. Capture UI`, which now renders Camp, Battle and Result).

The column is a panel, at the camp's own alpha: an `Image` of `0.07/0.09/0.12` at **0.92** — the value
ศาลาพัก's `Board` is authored with — with `raycastTarget` off so it cannot take a click meant for the
two buttons. It is named in `UiLayoutCheck.OpaqueRegions` beside the camp's panels, so the capture tool
reports a summary you can read the scene through, and
`ResultScreenLayoutTests.The_summary_column_carries_the_camp_panels_own_background` compares the two
scenes' alphas rather than trusting the copy of the number.

**What it publishes.** `ResultReport.Publish` (called by `ResultSceneDirector` right after
`Presenter.Begin()`) hands the page one document, `__projectSurviveResult`: the run's own outcome and
numbers, the seven strings the screen is showing, and the screen-pixel centres of its two controls
(`restart`, `title`) from `ResultView.TryGetButtonScreenPoint`. It is the mirror of `CampReport`'s job
for the one screen whose content is *text*, and it is what makes the last leg of the loop assertable in
a built player.

## 4. Dev cheats (development builds only)

"อมตะ, เพิ่มเบี้ย, ข้ามชั้น, spawn ผี", named `DEV MENU (test only)` on its own canvas
(`sortingOrder` 200, authored **inactive**).

Two independent gates, both readable values rather than `#if`s:

1. **`DevCheats.Allowed`** — decided once by the root scope
   (`DevCheats.Configure(Debug.isDebugBuild, Application.isEditor)`), false before that. In a release
   player both inputs are false, so the menu never shows.
2. **`BattleSetup.DevCheatsAllowed`** — copied from gate 1 when the run departs, so a floor started
   while cheats were off refuses every dev command with `CommandResult.RejectedNotAllowed` even if
   the shell changes its mind later.

The menu's whole vocabulary is `BattleCommand` (`DevGodMode`, `DevAddBia`, `DevSpawnGhost`,
`DevKillAll`, `DevWinNow`, `DevLoseNow`) through `DevMenuView` → `BattleSimulationRunner.Submit` →
`IBattleSession.Submit`, i.e. the same port a surrender uses. Its strings are deliberately **not**
localization rows: they are developer diagnostics in a menu that does not exist in a release build
(the same reasoning that excludes the title screen's diagnostics line).

**What it publishes.** `DevMenuView.Update` calls `DevMenuReport.Publish` when the panel is on screen
and again when the canvas changes size, so the page carries `__projectSurviveDevMenu`: the six control
centres, and `visible`. Coordinates only — whether a command may be pressed is still decided by the two
gates above, and a release build writes no document at all because there is no panel to measure. That
document is how `tests/web/result.spec.ts` ends a floor on purpose to reach the summary screen;
`DevMenuReportTests` pins the hidden-panel half of it.

## 5. Facts that cost time to learn

* **A namespace called `ProjectSurvive.Game.Result` breaks every file under `ProjectSurvive.Game.*`.**
  `Result` is also the shared typed outcome, and a sibling namespace shadows it — the whole tree
  failed with `'Result' is a namespace but is used like a type`. The scene assembly is
  `ProjectSurvive.Game.ResultScene` with namespace `ProjectSurvive.Game.ResultScene`; the MVP types
  live in `ProjectSurvive.Game.Presentation` (next to `TitleView`/`TitlePresenter`), not in a
  `.Result` sub-namespace. Same trap would hit any scene named after a Core type.
* **`DevCheats.Allowed` is process-wide**, so a PlayMode fixture that inherits the run of the test
  before it inherits that flag too; `DevCheats.Reset()` is the counterpart, and the root scope's
  `Configure` call is what sets the real answer in the Editor (true) and in a release player (false).
* **Re-running a stage's scene menu item rewrites the whole `.unity` file.** The battle scene is
  generated, so adding the dev menu regenerated `Battle.unity` — the object *set* is identical
  (verified by comparing `m_Name` inventories) but Unity renumbers file ids, so the diff is large.
  Prefer an incremental tool when the scene is old, or expect the churn.
* **A scene menu item that touches the URL allow-list opens Boot.** `RefreshUrlSceneAllowList` opens
  and saves `Boot.unity`, so the Editor is left in the Boot scene after any scene-creation item.
* **The cheapest leak in the loop was a stray `EventSystem`.** `SceneLoader`'s fade overlay lives in
  `DontDestroyOnLoad` and used to build a fallback `EventSystem` on itself the first time it faded —
  which is Boot, the one scene that authors none — and never take it down. Every scene afterwards then
  carried two, Unity logged `There are 2 event systems in the scene` **every frame**, and the 20-round
  trip test measured it as a linear **314 KB per iteration**: 1 930 retained log entries in a 90 s
  session, 5.40 MiB over 18 trips against a 3 MiB budget. It now lives exactly as long as the
  transition that needed it (`GoAsync`'s `finally`), the test measures **0.73 MiB** and the warning
  storm is 5 entries instead of 1 930. The general lesson: an overlay that survives `LoadScene` is a
  *scene-scoped* object living in an *app-scoped* one, and the per-frame warning it causes is what the
  loop's memory budget ends up measuring.
* **A document written the frame a canvas resizes is a frame early.** `Screen.width` reports the new
  size in the frame the element changed; the `CanvasScaler` lays the widgets out of it during that
  same frame's render. So a report that publishes once per size change publishes the **new size with
  the old geometry** and then stops — exactly what `tests/web/title.spec.ts` read from the player on
  2026-10-17: `canvasWidth: 1280` with `buttons[0].x` still 505.964, the 960×600 position, 134 px from
  the 640 the centre had moved to. The fix is a second trigger: the title director republishes while
  the *point it published* and the point the view reports disagree, not only while the size does
  (`CampScreenRects.TryCentre` allocates nothing, which is what makes reading it per frame acceptable —
  the camp reads the same helper at the rate its picture moves). The camp's own geometry report has the
  same first-frame staleness; it is corrected by its 4 Hz republish instead, which is why its browser
  test never saw it.
* **A centred screen must be centred by its anchors, not by arithmetic on a 1920 canvas.** The title's
  three regions are `anchorMin = anchorMax = (0.5, 0.5)`, because a `CanvasScaler` in `ScaleWithScreenSize`
  mode changes the canvas' **virtual width** with the screen's aspect: at the template's own 960×600
  the reference rect is 1821.6 × 1138.5 units, so a control placed at `x = 760` from the canvas' top-left
  corner (the arithmetic that centres it in 1920) lands ~26 px right of the centre, while the two
  centre-anchored labels above it stay centred. The browser check caught it (`buttons[0].x = 505.964`
  where 480 was expected); `ProjectSetup.CreateUiButton` now takes an optional `anchor`, defaulting to
  the corner every panel and column in this project lays its controls out from.
* **The summary screen could not be reached in a browser cheaply — and now is reached on purpose.**
  Floor 1 is *won* by a standing player, so the built player goes Battle → Camp; `?scene=Result` opens
  the scene but bounces to the camp because there is no finished run to summarise. The way out is the
  **dev menu**, which the Development build the browser suite runs shows and its floor accepts
  (`DevCheats.Allowed` is `Debug.isDebugBuild || Application.isEditor`): `tests/web/result.spec.ts` plays
  a standalone `?scene=Battle` floor, presses แพ้ทันที / lose now through the centres
  `__projectSurviveDevMenu` publishes, asserts `__projectSurviveResult` (the defeat headline, the four
  lines carrying the arena's own `kills` / `bia` / `finalHp`, and both control points) and then presses
  the screen's own **Restart** to land in a camp whose run is new (full HP, floor 1, `canDepart`).
  A Release build publishes no dev menu at all, so that spec is a Development-build check by
  construction — and the release half of the rule is `DevMenuReportTests`' subject: the authored scene
  shows nothing and names no control. The Editor-side Route to the same screen is
  `ProjectSurvive/Tools/1. Capture UI` (the Result section) and `ResultReportTests`, both of which run
  in Fast.

## 6. The title screen and its start control (Stage 10d)

Boot hands over to the title and **stops** — that was already true — and until 10d nothing on the
title could leave it, so the loop's first screen was a display. It now has the one control the
screen's job implies.

| Piece | File | What it does |
| --- | --- | --- |
| Logic | `Assets/Main/Scripts/Presentation/TitlePresenter.cs` | writes the heading, the เริ่มเล่น caption (`TitleKeys`) and the build line; `RequestStart()` starts the run and records the intent |
| View | `.../Presentation/TitleView.cs` | mirrors three reactive strings, forwards one press, selects the control so `Enter` submits it, and reads `Space` (measured: the Input System version this project pins does not carry Space in the module's default submit bindings) |
| Director | `.../Presentation/Flow/TitleSceneDirector.cs` | brings the screen up, publishes `__projectSurviveTitle`, and asks the `SceneLoader` for the camp once the presenter has a request |
| Document | `.../Presentation/TitleReport.cs` | the page's copy: scene marker, the strings, the control's name and its centre in canvas pixels |
| Scene | `Assets/Main/Scenes/Title.unity` | `ProjectSurvive/Stage 10d/7. Create Title scene (start control)` — three centre-anchored regions (1800×140 heading, 1800×60 build line, 400×76 control) |

Three rules carry it, and each is [ADR 0022](../../docs/adr/0022-the-title-starts-a-run.md):

* **The title starts the run, not the camp.** `RequestStart` calls the same `RunSession.StartRun` the
  camp calls when it is opened without a run; the camp's own guard stays for the handovers that do not
  come through here (a direct `?scene=Camp`). One place starts a run, one answer to what a new run is.
* **The run is asked for lazily, never held.** The presenter is an **App-scope singleton** and arriving
  at the title *disposes* the run scope, so it calls `RunScopeOwner.RequireRun()` at the moment of the
  press. That is what makes "กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่" true by construction.
* **The intent is recorded, the exit is the loader's.** A refusal (`transition.in_progress`, a scene
  that is not built) is typed, logged once, and leaves the screen as it was; the request is retried
  every frame until the fade takes over. Same shape as the summary screen's two ways out.

What it publishes is coordinates, never a permission (the same reasoning as `DevMenuReport`): the
browser spec clicks the point and asserts the game's own answer — the scene marker becoming `Camp` and
the camp's report carrying a floor-1 run with a purse it can depart from.

Source: [`10d-report`](../../docs/stages/10d-report.md) (the title screen, its start control, and the
browser check that found the two geometry defects).
