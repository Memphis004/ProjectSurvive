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
* Every string is a `result.*` row in `data/localization/localization.csv` (187 rows now) resolved by
  `ResultPresenter`; the view mirrors and forwards, and computes nothing.
* **Restart is not a command on the run.** It records the intent, is committed by the loader, and
  `RunScopeOwner.DisposeRun()` is what makes the camp start a brand-new run (floor 1, starting purse,
  empty bag). Back to Title performs `RunSession.ReturnToTitle()` and lets the transition dispose the
  run.

The scene is 1200×1000 centred column: 96 px headline, 32 px lines 60 px apart, two 400×76 buttons —
all above the 22 px Thai floor, measured in `artifacts/stage10/result-*.png`.

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
* **The summary screen cannot be reached in a browser cheaply.** Floor 1 is *won* by a standing player,
  so the built player goes Battle → Camp; `?scene=Result` opens the scene but it bounces to the camp
  because there is no finished run to summarise. Anything that only a real player can catch in the
  Result scene (a stripped DI constructor, an unbaked font) is therefore still unverified, and the
  budget for closing it is a browser run that ends a run — five floors, or a surrender the browser
  cannot ask for.
