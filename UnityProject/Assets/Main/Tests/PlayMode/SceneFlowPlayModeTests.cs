using System.Collections;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Audio;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 10a in PlayMode: one run across scene loads, three scopes with three lifetimes, and a
    /// battle session that never outlives the scene that made it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here goes through the real scenes: the camp's own <c>CampPresenter</c> performs
    /// the departure, the real <c>SceneLoader</c> performs the handover, the real EcsLite session
    /// plays the floor, and the run is read from the scope the game would read it from. A test that
    /// hand-built a run and a setup would pass on a build where the camp and the battle disagreed
    /// about which run they were playing, which is the bug this stage exists to remove.
    /// </para>
    /// <para>
    /// Every test boots through <c>Boot</c> first (<see cref="SceneFlowHarness.BootShell"/>), which
    /// is what makes the App scope persist and therefore what makes a run outlive a scene. Loading
    /// the camp directly would give the camp its own root scope and a run of its own — the
    /// standalone behaviour, which the battle fixture covers.
    /// </para>
    /// <para>
    /// The 20-loop case is the acceptance line "Camp ↔ Battle วน 20 รอบแล้วไม่มี presenter หรือ ECS
    /// world ซ้ำ และ memory ไม่โตต่อเนื่อง". It runs on <b>one</b> App scope — resetting between
    /// iterations would hide exactly the accumulation it is looking for — and asserts both what a
    /// leaked scope looks like (a second <c>CampSceneScope</c>, a surviving <c>RunLifetimeScope</c>,
    /// a live session after the arena is gone) and the memory the leak would cost.
    /// </para>
    /// </remarks>
    public sealed class SceneFlowPlayModeTests
    {
        /// <summary>How long a floor may take to start before the test calls it stuck.</summary>
        private const float FloorStartBudgetSeconds = 30f;

        /// <summary>
        /// How much the managed heap may grow across the 20-loop, measured between iteration 2 and
        /// iteration 20 (a warm-up pair, so the first iteration's caches are not counted). A leak of
        /// one run scope plus one ECS world per iteration costs far more than this; the number and
        /// the measurement are in the stage report.
        /// </summary>
        private const long ManagedGrowthBudgetBytes = 3L * 1024L * 1024L;

        /// <summary>Unity's own allocation gets the same treatment: a leaked GameObject or a
        /// Material built per scene shows up here long before it shows up in the managed heap.</summary>
        private const long AllocatedGrowthBudgetBytes = 24L * 1024L * 1024L;

        /// <summary>
        /// The whole point of the stage, in one gesture: spend at the camp, walk into the arena, and
        /// find the same run — the same purse, the same bag, the same HP — on the other side.
        /// </summary>
        [UnityTest]
        public IEnumerator The_run_carries_its_purse_bag_and_hp_into_the_battle_scene()
        {
            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            RunSession run = camp.Run;
            Assert.That(run, Is.Not.Null, "the camp has no run");
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp));

            // Spend something, so "nothing was lost" is a statement about numbers that changed.
            camp.Presenter.Rent(0);
            Assert.That(camp.Presenter.Counters.Rented, Is.EqualTo(1), "the camp's first shelf was not rented");

            int bia = run.Wallet.Bia;
            int chestUsed = run.ChestUsed;
            int hp = run.Hp;
            uint seed = run.Seed;
            float boardWeight = run.BagWeight;

            SceneLoader loader = SceneFlowHarness.Loader();
            camp.Presenter.Depart();
            Assert.That(camp.Presenter.Departed, Is.True, "the camp refused to depart: " + camp.Presenter.StatusCode);

            yield return null;
            Assert.That(loader.IsTransitioning, Is.True, "the departure did not start a scene change");

            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.BattleSceneName);
            yield return SceneFlowHarness.WaitForIdle(loader);

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            Assert.That(battle, Is.Not.Null, "the Battle scene has no BattleSceneDirector");
            yield return WaitForFloorToStart(battle);

            // The same object, not an equal one: a second run would look identical for one frame.
            Assert.That(battle.Run, Is.SameAs(run), "the battle scene built a run of its own");
            Assert.That(battle.Standalone, Is.False, "the battle scene stood in for a camp that did depart");
            Assert.That(battle.Runner.IsRunning, Is.True, "the floor is not running: " + battle.StartError);

            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Battle), "the run is not in its battle phase");
            Assert.That(run.Wallet.Bia, Is.EqualTo(bia), "the purse changed on the way into the arena");
            Assert.That(run.ChestUsed, Is.EqualTo(chestUsed), "the chest changed on the way into the arena");
            Assert.That(run.Hp, Is.EqualTo(hp), "HP changed on the way into the arena");
            Assert.That(run.Seed, Is.EqualTo(seed), "the run seed changed");
            Assert.That(run.BagWeight, Is.EqualTo(boardWeight).Within(0.001f), "the bag changed on the way");

            // And the floor really is that run's floor: the HP the battle contract was handed is the
            // run's HP, and the purse on the HUD is the camp's purse.
            BattleSetup setup = run.PendingSetup;
            Assert.That(setup, Is.Not.Null, "the battle scene has no floor setup on the run");
            Assert.That(setup.CurrentHp, Is.EqualTo(hp), "BattleSetup.CurrentHp is not the run's HP");
            Assert.That(setup.FloorIndex, Is.EqualTo(1), "the first floor of the run is not floor 1");
            // The HUD's purse is the camp's purse plus whatever the floor itself has paid out so
            // far: the presenter composes `startingBia + earned-in-floor`, so a battle that built a
            // run of its own would show only the second term.
            int earnedInFloor = battle.Runner.View.Snapshot.Hud.Currency;
            int hudBia = int.Parse(
                SceneFlowHarness.BattleHud().BiaValue.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(
                hudBia, Is.EqualTo(bia + earnedInFloor),
                "the HUD's purse is not the camp's purse (camp " + bia + ", floor " + earnedInFloor +
                ", HUD " + hudBia + ")");
        }

        /// <summary>
        /// A finished floor is settled into the run, and a lost one ends it — the HP the run keeps
        /// is the battle's <c>FinalHp</c>, and no clear-heal is applied to a loss.
        /// </summary>
        [UnityTest]
        public IEnumerator A_settled_floor_writes_its_result_back_into_the_run()
        {
            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            camp.Presenter.Depart();
            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.BattleSceneName);
            yield return SceneFlowHarness.WaitForIdle(SceneFlowHarness.Loader());

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            yield return WaitForFloorToStart(battle);

            RunSession run = battle.Run;
            int hpBefore = run.Hp;
            int biaBefore = run.Wallet.Bia;

            // Surrender is a real command of the contract (not a dev cheat), so this is a loss the
            // game itself can produce on any build.
            Assert.That(
                battle.Runner.Submit(BattleCommand.Surrender()), Is.EqualTo(CommandResult.Ok),
                "the floor refused a surrender");

            yield return SceneFlowHarness.WaitUntil(() => battle.Settled, "the floor ended but was never settled");

            Assert.That(battle.LastSettlement, Is.Not.Null, "the settlement was not kept");
            FloorSettlement settlement = battle.LastSettlement.Value;
            Assert.That(settlement.EndReason, Is.EqualTo(BattleEndReason.Surrendered));
            Assert.That(settlement.RunCompleted, Is.True, "a lost floor did not end the run");
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Result), "the run is not over");

            // HP across the seam: BattleSetup.CurrentHp went in, BattleResult.FinalHp came out, and
            // a loss heals nothing (the 15% clear heal belongs to the run's settlement).
            BattleResult result = battle.Runner.Result;
            Assert.That(run.Hp, Is.EqualTo(result.FinalHp), "the run's HP is not the battle's FinalHp");
            Assert.That(settlement.HealedHp, Is.EqualTo(0), "a lost floor healed the player");
            Assert.That(settlement.RewardBia, Is.EqualTo(0), "a lost floor paid a reward");
            Assert.That(run.Wallet.Bia, Is.EqualTo(biaBefore), "the purse changed over a lost floor");
            Assert.That(hpBefore, Is.GreaterThan(0));
        }

        /// <summary>
        /// Leaving the arena disposes the battle session: "dispose IBattleSession ทุกครั้งที่ออกจาก
        /// SceneBattle". The runner is held across the unload, so the assertion is about the object
        /// the scene actually built rather than about a scene that happens to be gone.
        /// </summary>
        [UnityTest]
        public IEnumerator Leaving_the_battle_scene_disposes_its_session_and_its_run_scope()
        {
            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            camp.Presenter.Depart();
            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.BattleSceneName);

            SceneLoader loader = SceneFlowHarness.Loader();
            yield return SceneFlowHarness.WaitForIdle(loader);

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            yield return WaitForFloorToStart(battle);

            BattleSimulationRunner runner = battle.Runner;
            Assert.That(runner.Session, Is.Not.Null, "no session was created");
            Assert.That(SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1), "there is not exactly one run scope");

            // Out of the arena through the loader, which is also what ends the run at the title.
            Assert.That(loader.TryGo(SceneId.Title), Is.True, "the title was refused: " + loader.LastError);
            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.TitleSceneName);
            yield return SceneFlowHarness.WaitForIdle(loader);

            Assert.That(runner.Session, Is.Null, "the session outlived the scene that created it");
            Assert.That(runner.View, Is.Null, "the view outlived the scene that created it");
            Assert.That(runner.IsRunning, Is.False, "the runner is still stepping a floor");

            // "กลับ Title แล้ว dispose Run scope": the run is gone, and nothing about it is left.
            Assert.That(SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(0), "the run scope outlived the title");
            Assert.That(SceneFlowHarness.Count<BattleSceneScope>(), Is.EqualTo(0), "the arena's scope outlived its scene");
            Assert.That(SceneFlowHarness.Count<CampSceneScope>(), Is.EqualTo(0), "the camp's scope outlived its scene");
            Assert.That(
                SceneFlowHarness.Owner().HasRun, Is.False,
                "the App scope still holds a run at the title");
        }

        /// <summary>
        /// The game has exactly one <see cref="AudioListener"/>, on the audio device's own object,
        /// and it is still there after a camp → battle → title walk.
        /// </summary>
        /// <remarks>
        /// The listener is authored into the Boot scene on the app root that is carried into
        /// <c>DontDestroyOnLoad</c>. That is a fix, not a detail: the game used to let
        /// <c>AudioDirector</c> add its own listener from <c>Awake</c>, the component never appeared,
        /// and the resulting "There are no audio listeners in the scene" warning — once per frame per
        /// playing source — was most of what the 20-visit heap case was measuring. This is the direct
        /// assertion; the 20-loop keeps the byte budget that the flood used to blow through.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_game_has_one_audio_listener_that_survives_every_scene_change()
        {
            yield return SceneFlowHarness.EnterCamp();
            AssertOneListener("camp");

            // The camp's own departure, so the walk is the one the player takes.
            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            camp.Presenter.Depart();
            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.BattleSceneName);
            yield return SceneFlowHarness.WaitForIdle(SceneFlowHarness.Loader());

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            yield return WaitForFloorToStart(battle);
            AssertOneListener("battle");

            SceneLoader loader = SceneFlowHarness.Loader();
            Assert.That(loader.TryGo(SceneId.Title), Is.True, "the title was refused: " + loader.LastError);
            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.TitleSceneName);
            yield return SceneFlowHarness.WaitForIdle(loader);

            AssertOneListener("title");
        }

        /// <summary>
        /// Fails unless there is exactly one enabled listener and it belongs to the audio device —
        /// the object the shell persists, rather than one a scene brought with it.
        /// </summary>
        private static void AssertOneListener(string where)
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(
                listeners.Length, Is.EqualTo(1),
                "the " + where + " screen has " + listeners.Length + " audio listeners");
            Assert.That(listeners[0].enabled, Is.True, "the listener on the " + where + " screen is disabled");
            Assert.That(AudioDirector.Instance, Is.Not.Null, "there is no audio device on the " + where + " screen");
            Assert.That(
                listeners[0].gameObject, Is.EqualTo(AudioDirector.Instance.gameObject),
                "the listener on the " + where + " screen is not on the game's audio device");
        }

        /// <summary>
        /// Twenty camp → battle → title round trips on one App scope: no second scene scope, no
        /// second run scope, no session left alive, and no unbounded memory growth.
        /// </summary>
        [UnityTest]
        public IEnumerator Twenty_camp_and_battle_visits_leave_nothing_behind_and_do_not_grow_the_heap()
        {
            // Through Boot, so this is the real shell: one App scope for the whole loop.
            yield return SceneFlowHarness.BootShell();

            Assert.That(
                SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(0),
                "the title screen already had a run scope");

            SceneLoader loader = SceneFlowHarness.Loader();

            long managedAtWarmUp = 0;
            long allocatedAtWarmUp = 0;
            int sessionsCreated = 0;

            for (int iteration = 0; iteration < 20; iteration++)
            {
                Assert.That(
                    loader.TryGo(SceneId.Camp), Is.True,
                    "iteration " + iteration + ": the camp was refused: " + loader.LastError);
                yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.CampSceneName);
                yield return SceneFlowHarness.WaitForIdle(loader);

                Assert.That(
                    SceneFlowHarness.Count<CampSceneScope>(), Is.EqualTo(1),
                    "iteration " + iteration + ": the camp has " + SceneFlowHarness.Count<CampSceneScope>() + " scopes");
                Assert.That(
                    SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                    "iteration " + iteration + ": there are " + SceneFlowHarness.Count<RunLifetimeScope>() + " run scopes");

                var camp = SceneFlowHarness.Find<CampSceneDirector>();
                Assert.That(camp.TryStart(), Is.True, "iteration " + iteration + ": " + camp.StartError);
                Assert.That(
                    camp.Run.FlowState, Is.EqualTo(RunFlowState.Camp),
                    "iteration " + iteration + ": the run did not start at the camp");

                camp.Presenter.Depart();
                Assert.That(
                    camp.Presenter.Departed, Is.True,
                    "iteration " + iteration + ": the camp refused to depart: " + camp.Presenter.StatusCode);

                yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.BattleSceneName);
                yield return SceneFlowHarness.WaitForIdle(loader);

                Assert.That(
                    SceneFlowHarness.Count<BattleSceneScope>(), Is.EqualTo(1),
                    "iteration " + iteration + ": the arena has " + SceneFlowHarness.Count<BattleSceneScope>() + " scopes");
                Assert.That(
                    SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                    "iteration " + iteration + ": entering the arena built a second run");

                var battle = SceneFlowHarness.Find<BattleSceneDirector>();
                yield return WaitForFloorToStart(battle);

                BattleSimulationRunner runner = battle.Runner;
                Assert.That(runner.Session, Is.Not.Null, "iteration " + iteration + ": no session was created");
                sessionsCreated++;

                // End the floor with a command the contract always accepts, then leave for the title
                // — which is where the run scope is disposed and the next iteration starts fresh.
                Assert.That(
                    runner.Submit(BattleCommand.Surrender()), Is.EqualTo(CommandResult.Ok),
                    "iteration " + iteration + ": the floor refused a surrender");
                yield return SceneFlowHarness.WaitUntil(
                    () => battle.Settled, "iteration " + iteration + ": the floor was never settled");
                Assert.That(
                    battle.Runner.Result.EndReason, Is.EqualTo(BattleEndReason.Surrendered),
                    "iteration " + iteration + ": the surrender did not end the floor");

                // A surrendered floor ends the run, so the arena leaves for the summary screen by
                // itself (Stage 10b). The loop rides that transition rather than racing it: a
                // second TryGo on that frame would be refused, correctly, as a transition already
                // in flight.
                yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.ResultSceneName);
                Assert.That(
                    SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                    "iteration " + iteration + ": the summary screen has no run to summarise");

                Assert.That(
                    loader.TryGo(SceneId.Title), Is.True,
                    "iteration " + iteration + ": the title was refused: " + loader.LastError);
                yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.TitleSceneName);
                yield return SceneFlowHarness.WaitForIdle(loader);

                Assert.That(
                    runner.Session, Is.Null,
                    "iteration " + iteration + ": a session outlived its Battle scene");
                Assert.That(
                    SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(0),
                    "iteration " + iteration + ": a run scope outlived the title");
                Assert.That(
                    SceneFlowHarness.Count<CampSceneScope>(), Is.EqualTo(0),
                    "iteration " + iteration + ": a camp scope outlived its scene");
                Assert.That(
                    SceneFlowHarness.Count<BattleSceneScope>(), Is.EqualTo(0),
                    "iteration " + iteration + ": an arena scope outlived its scene");

                long managedNow = SceneFlowHarness.ManagedBytes();
                long allocatedNow = SceneFlowHarness.AllocatedBytes();
                TestContext.WriteLine(
                    "loop " + iteration + ": managed " + managedNow + " B, allocated " + allocatedNow + " B");

                if (iteration == 1)
                {
                    managedAtWarmUp = managedNow;
                    allocatedAtWarmUp = allocatedNow;
                }
            }

            Assert.That(sessionsCreated, Is.EqualTo(20), "twenty visits did not play twenty floors");

            long managedGrowth = SceneFlowHarness.ManagedBytes() - managedAtWarmUp;
            long allocatedGrowth = SceneFlowHarness.AllocatedBytes() - allocatedAtWarmUp;

            TestContext.WriteLine(
                "20 loop: managed growth " + managedGrowth + " B, allocated growth " + allocatedGrowth + " B");

            Assert.That(
                managedGrowth, Is.LessThan(ManagedGrowthBudgetBytes),
                "the managed heap grew by " + managedGrowth + " B over 18 camp/battle round trips");
            Assert.That(
                allocatedGrowth, Is.LessThan(AllocatedGrowthBudgetBytes),
                "Unity's allocated memory grew by " + allocatedGrowth + " B over 18 camp/battle round trips");
        }

        /// <summary>
        /// The scene loader takes one transition at a time: a second request while one is in flight
        /// is refused, and a scene that is not in the build is refused before anything is hidden.
        /// </summary>
        [UnityTest]
        public IEnumerator The_scene_loader_refuses_a_second_transition_and_a_scene_that_is_not_built()
        {
            yield return SceneFlowHarness.EnterCamp();

            SceneLoader loader = SceneFlowHarness.Loader();
            Assert.That(loader.IsTransitioning, Is.False, "the loader was busy before it was asked");

            // Every scene the game has is in the build settings now that Stage 10b created the
            // summary scene, so the "cannot be loaded" path is exercised through the enum's own
            // shape: an id with no scene behind it. The refusal is typed, logged, and the screen
            // stays put — the log is expected rather than tolerated, because a silent refusal would
            // be worse.
            LogAssert.Expect(
                LogType.Error, "SceneLoader cannot load '99': it is not in the build settings.");

            Assert.That(
                loader.TryGo((SceneId)99), Is.False, "the loader accepted a scene that is not built");
            Assert.That(loader.LastError, Is.EqualTo(TransitionError.SceneMissing + ":99"));

            // And the summary scene really is loadable, which is what the battle's own departure
            // relies on.
            Assert.That(
                Application.CanStreamedLevelBeLoaded(SceneFlowHarness.ResultSceneName), Is.True,
                "the Result scene is not in the build settings");
            Assert.That(loader.IsTransitioning, Is.False, "a refused transition left the loader busy");
            Assert.That(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                Is.EqualTo(SceneFlowHarness.CampSceneName),
                "a refused transition changed the scene");

            Assert.That(loader.TryGo(SceneId.Title), Is.True, "the title was refused: " + loader.LastError);
            Assert.That(loader.IsTransitioning, Is.True, "the accepted transition is not in flight");

            // One at a time: the second ask is refused, and it is the first transition that lands.
            Assert.That(loader.TryGo(SceneId.Battle), Is.False, "a second transition was accepted");
            Assert.That(loader.LastError, Is.EqualTo(TransitionError.InProgress));

            yield return SceneFlowHarness.WaitForScene(SceneFlowHarness.TitleSceneName);
            yield return SceneFlowHarness.WaitForIdle(loader);

            Assert.That(loader.IsTransitioning, Is.False, "the loader is still busy after the handover");
            Assert.That(loader.Target, Is.EqualTo(SceneId.Title));
        }

        private static IEnumerator WaitForFloorToStart(BattleSceneDirector battle)
        {
            float deadline = Time.realtimeSinceStartup + FloorStartBudgetSeconds;
            while ((battle.Runner is null || !battle.Runner.IsRunning) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(battle.Runner, Is.Not.Null, "the battle scene has no runner");
            Assert.That(
                battle.Runner.IsRunning, Is.True,
                "the floor never started: " + battle.StartError);
        }
    }
}
