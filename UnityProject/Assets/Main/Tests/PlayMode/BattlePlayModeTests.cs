using System;
using System.Collections;
using Leopotam.EcsLite;
using NUnit.Framework;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Simulation.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.SpineActors;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 08 acceptance in PlayMode: the Battle scene plays a floor, keeps playing it without an
    /// exception, and gives everything back when it is left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the lines of the acceptance list that only a running player loop can answer:
    /// "PlayMode: เข้า Battle แล้ว tick 10 วินาทีไม่มี exception" and
    /// "ออก Battle แล้ว ECS world และ subscription ถูก dispose". The EditMode side proves the
    /// same contract against hand-made frames; a green pair is evidence, one green suite is not.
    /// </para>
    /// <para>
    /// Scene indices come from the build settings rather than from a hardcoded number, so the
    /// tests follow whatever <c>ProjectSetup</c> configured.
    /// </para>
    /// </remarks>
    public sealed class BattlePlayModeTests
    {
        private const string BattleSceneName = "Battle";
        private const string TitleSceneName = "Title";

        /// <summary>How long to wait for the scene to start a floor before calling it stuck.</summary>
        private const float StartDeadlineSeconds = 30f;

        /// <summary>
        /// Wall-clock budget for ten seconds of simulation. Comfortably over the five seconds the
        /// run needs at 2×, so a slow machine fails on a real stall rather than on its own speed —
        /// and the assertion is on ticks, never on elapsed time.
        /// </summary>
        private const float TenSecondRunDeadlineSeconds = 90f;

        [UnityTest]
        public IEnumerator The_battle_scene_starts_a_floor_and_binds_the_hud()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSceneDirector director = Find<BattleSceneDirector>();
            BattleSimulationRunner runner = Find<BattleSimulationRunner>();

            Assert.That(director, Is.Not.Null, "the Battle scene has no BattleSceneDirector");
            Assert.That(runner, Is.Not.Null, "the Battle scene has no BattleSimulationRunner");
            Assert.That(director.StartError, Is.Empty, "the floor refused to start: " + director.StartError);
            Assert.That(runner.IsRunning, Is.True, "the runner is not stepping a floor");
            Assert.That(runner.Session, Is.Not.Null, "no session was created through the factory port");
            Assert.That(runner.View, Is.Not.Null);

            // The pool is warmed at scene start and has not grown while the first wave arrived.
            Assert.That(
                runner.View.Pool.CreatedTotal,
                Is.EqualTo(
                    BattleViewCaps.PlayerViews + BattleViewCaps.EnemyViews
                    + BattleViewCaps.ProjectileViews + BattleViewCaps.PickupViews),
                "the pool was not warmed to the caps");
            Assert.That(
                runner.View.Pool.CreatedAfterWarmup,
                Is.Zero,
                "a view was created after warm-up: the pool is too small for the floor");

            // The HUD is bound: a fixed set of subscriptions, the floor's own timeline, and a
            // caption that reached a label.
            BattleHudView hud = Find<BattleHudView>();
            Assert.That(hud, Is.Not.Null, "the Battle scene has no HUD");
            Assert.That(hud.State, Is.Not.Null, "the HUD never resolved its state");
            Assert.That(hud.SubscriptionCount, Is.GreaterThan(20), "the HUD subscribed to almost nothing");
            Assert.That(hud.SubscriptionCount, Is.LessThan(40), "the HUD holds a subscription per something");
            Assert.That(hud.TimelineMarkerCount, Is.EqualTo(1), "floor 1 has one spawn window in the tables");

            string floorName = hud.State.FloorName.Value;
            Assert.That(floorName, Is.Not.Empty, "the floor's name was not localized");
            Assert.That(
                LabelText("FloorLabel"),
                Is.EqualTo(floorName),
                "the presenter's floor name never reached the view's label");
        }

        [UnityTest]
        public IEnumerator Ten_seconds_of_ticks_raise_no_exception_and_bind_no_new_view()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            BattleHudView hud = Find<BattleHudView>();
            BattleHudState state = hud.State;
            int subscriptionsAtStart = hud.SubscriptionCount;
            bool speedSubscribed = state.SpeedLabel.HasObservers;

            // Play the way a player would: 2× (the scene's own speed button) and some movement, so
            // the floor actually has ghosts closing in rather than a still target.
            runner.View.Speed.Set(2f);
            ((BattleInputAdapter)runner.View.Input).SetOverride(0.6f, 0.9f);

            int ticksWanted = BattleConstants.TickRate * 10;
            float deadline = Time.realtimeSinceStartup + TenSecondRunDeadlineSeconds;
            int maxBoundViews = 0;
            long droppedBefore = runner.DroppedTicks;

            while (runner.StepsRun < ticksWanted && Time.realtimeSinceStartup < deadline)
            {
                maxBoundViews = Mathf.Max(maxBoundViews, runner.View.BoundViews);
                yield return null;
            }

            Assert.That(
                runner.StepsRun,
                Is.GreaterThanOrEqualTo(ticksWanted),
                "the floor did not reach ten seconds of ticks (runner is "
                    + (runner.IsRunning ? "running" : "stopped") + ")");
            Assert.That(maxBoundViews, Is.GreaterThan(0), "the view never bound a single entity");
            Assert.That(
                runner.View.Pool.CreatedAfterWarmup,
                Is.Zero,
                "a view was created after warm-up, so the pool does not cover the floor");

            // Stage 17b: the doctor is the Spine figure, built once at the floor's start, and its
            // Play is not called per frame. Ten seconds at 2× is roughly 1 200 rendered frames.
            PlayerFigurePresenter figure = runner.View.PlayerFigure;
            Assert.That(figure, Is.Not.Null, "the scene wired no player figure");
            Assert.That(figure.IsReady, Is.True, "the player figure is not drawing the doctor: " + figure.ErrorCode);
            Assert.That(
                figure.PlayCalls,
                Is.LessThan(40),
                "the figure played " + figure.PlayCalls + " animations in ten seconds: Play is running per frame");

            // "HUD ไม่ subscribe ตำแหน่งทุก entity": the subscription count is a constant, whatever
            // the floor did in those ten seconds.
            Assert.That(hud.SubscriptionCount, Is.EqualTo(subscriptionsAtStart), "the HUD subscribed per entity");
            Assert.That(
                state.SpeedLabel.HasObservers,
                Is.EqualTo(speedSubscribed),
                "the HUD's subscription set changed shape");

            TestContext.WriteLine(
                "ten seconds: steps=" + runner.StepsRun + " dropped=" + (runner.DroppedTicks - droppedBefore)
                + " maxBound=" + maxBoundViews + " viewsCreated=" + runner.View.Pool.CreatedTotal
                + " grownAfterWarmup=" + runner.View.Pool.CreatedAfterWarmup
                + " damageNumbers=" + runner.View.ActiveDamageNumbers
                + " hudSubscriptions=" + hud.SubscriptionCount
                + " spinePlays=" + figure.PlayCalls + " spineAnimation=" + figure.Animation
                + " spinePose=" + figure.Pose);

            // No LogAssert here on purpose. The runner publishes its progress report through
            // WebGlBridge, which logs a line in a player and in the editor, so "no unexpected log"
            // would fail on the report the browser tests read. What matters — that ten seconds of
            // ticks raised no error or exception — is what Unity's test framework already fails a
            // test on, and this method reaching its end is the evidence.
            Assert.That(runner.IsRunning, Is.True, "the floor stopped before its ten seconds were up");
        }

        // -------------------------------------------------------------------
        // Stage 08g-2: the HUD covers at most 15 % of the screen
        // -------------------------------------------------------------------

        /// <summary>
        /// The battle HUD's four panels cover no more than 15 % of the screen, measured on the scene the
        /// player gets rather than on a hand-made layout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The stage's sixth item states the number ("วัดพื้นที่ที่ UI บังจอ ต้อง ≤ 15 % ที่ 1920×1080
        /// (มีเทสต์)"). Before it the same screen was five panels totalling 37.86 %: the mini ย่าม
        /// 11.38, the run's numbers 8.62, the timeline 7.88, the status panel 5.75 and the floor block
        /// 4.22. Those panels never overlapped, so moving them could not have met the target — the HUD
        /// had to be rebuilt, which is what 08g-2 did.
        /// </para>
        /// <para>
        /// The measure is each panel's rectangle as a fraction of the canvas, which is honest only
        /// because the four rectangles are disjoint — asserted here as well. A screen of stacked
        /// translucent panels would have to be measured by covered pixels instead.
        /// </para>
        /// <para>
        /// The dev menu is not one of the four. Since 08g-2 it starts shut and opens on F1, so before
        /// the key is pressed it covers nothing.
        /// </para>
        /// </remarks>
        [UnityTest]
        public IEnumerator The_hud_covers_at_most_fifteen_percent_of_the_screen()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();
            Canvas.ForceUpdateCanvases();

            BattleHudView hud = Find<BattleHudView>();
            Assert.That(hud, Is.Not.Null, "the Battle scene has no HUD");

            // The HUD lives on the screen's canvas, so its own rectangle is the screen's own space.
            Rect screen = WorldRect((RectTransform)hud.transform);
            Assert.That(screen.width, Is.GreaterThan(0f), "the canvas has no size, so nothing can be measured");

            Rect[] panels =
            {
                PanelOf("FloorLabel"),
                PanelOf("TimeCaption"),
                PanelOf("HpCaption"),
                PanelOf("TimelineCaption"),
            };

            Assert.That(
                BattleHudArea.AnyOverlap(panels),
                Is.False,
                "two HUD panels overlap, so their rectangles cannot be summed as the covered area");

            float fraction = BattleHudArea.Fraction(panels, screen.size);

            DevMenuView devMenu = Find<DevMenuView>();
            Assert.That(devMenu, Is.Not.Null, "the Battle scene has no dev menu");
            Assert.That(
                devMenu.PanelVisible, Is.False,
                "the dev menu is on screen without F1, so it is part of the screen the player sees");

            TestContext.WriteLine(
                "hud area: canvas=" + screen.width + "x" + screen.height
                + " floor=" + panels[0].size + " resources=" + panels[1].size
                + " status=" + panels[2].size + " timeline=" + panels[3].size
                + " covered=" + (fraction * 100f).ToString("0.00") + " %");

            Assert.That(
                fraction, Is.LessThanOrEqualTo(BattleHudArea.Budget),
                "the HUD covers " + (fraction * 100f).ToString("0.00")
                + " % of the screen; the stage allows " + (BattleHudArea.Budget * 100f) + " %");
        }

        /// <summary>The panel a label is drawn in: the label's own parent, measured the way the capture does.</summary>
        private static Rect PanelOf(string labelName)
        {
            GameObject label = GameObject.Find(labelName);
            Assert.That(label, Is.Not.Null, "the HUD has no " + labelName);
            return WorldRect((RectTransform)label.transform.parent);
        }

        // -------------------------------------------------------------------
        // Stage 17b: the doctor is the Spine figure
        // -------------------------------------------------------------------

        /// <summary>
        /// The scene really wires the figure, and it really built: the reference in
        /// <see cref="BattleViewSettings.PlayerFigure"/>, the presenter the scene's scope registered,
        /// and the object the player sees are the same three things. This is the wiring check Stage
        /// 08c's mini-bag defect taught — a component that is correct in a fixture and unwired in the
        /// scene passes every EditMode assertion.
        /// </summary>
        [UnityTest]
        public IEnumerator The_battle_scene_builds_the_doctor_figure_once_and_stands_still_at_first()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            MonoBehaviour figureInScene = FigureProbe.Find();

            Assert.That(figureInScene, Is.Not.Null, "the Battle scene carries no player figure");
            Assert.That(
                runner.View.PlayerFigure,
                Is.Not.Null,
                "the scene's BattleViewSettings.PlayerFigure is not assigned to the view");

            PlayerFigurePresenter figure = runner.View.PlayerFigure;
            Assert.That(
                figure.View,
                Is.SameAs(figureInScene),
                "the presenter drives a different figure than the one in the scene");
            // Stage 17f: the presenter holds the component and calls it through the seam.
            Assert.That(
                figure.Figure,
                Is.SameAs(figureInScene.GetComponent<IPlayerFigure>()),
                "the presenter did not resolve the backend seam off the scene's figure");
            Assert.That(figure.ErrorCode, Is.Null, "the figure refused to build: " + figure.ErrorCode);
            Assert.That(figure.IsReady, Is.True, "the player figure never built");
            Assert.That(figure.Figure.BuildCount, Is.EqualTo(1), "the figure was built more than once");
            Assert.That(
                figure.Figure.DrawnHeightWorld,
                Is.EqualTo(FigureProbe.ProfileHeight(figureInScene)).Within(0.01f),
                "the figure is not drawn at its profile's height");

            // No view was created while the floor ran, and the figure is not one of them.
            Assert.That(runner.View.Pool.CreatedAfterWarmup, Is.Zero);

            yield return WaitForTicks(runner, runner.StepsRun + BattleConstants.TickRate);

            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Idle), "a standing doctor must stand");
            Assert.That(
                figure.Animation,
                Is.EqualTo(FigureProbe.IdleAnimation(figureInScene)),
                "the idle pose must be the profile's idle animation");
            Assert.That(
                figure.Figure.BuildCount,
                Is.EqualTo(1),
                "a second of floor built the figure again");

            // Stage 17e: the mirror follows the profile's own rule, using the facing the real frame
            // produced (not a hand-presented one).
            int face = figure.Facing;
            // flip = ArtFacesRight ? facing < 0 : facing > 0; the mirror is localScale.x = -1.
            bool flipped = FigureProbe.ArtFacesRight(figureInScene) ? face < 0 : face > 0;
            float expectedScale = flipped ? -1f : 1f;
            Assert.That(
                FigureProbe.MirrorScaleX(figureInScene), Is.EqualTo(expectedScale),
                "the arena's figure is mirrored against the profile's artFacesRight rule (facing " + face + ")");

            TestContext.WriteLine(
                "figure: backend=" + FigureProbe.Backend(figureInScene) + " ready=" + figure.IsReady
                + " plays=" + figure.PlayCalls + " facing=" + face
                + " scaleX=" + FigureProbe.MirrorScaleX(figureInScene)
                + " idle=" + FigureProbe.IdleAnimation(figureInScene)
                + " pose=" + figure.Pose + " animation='" + figure.Animation + "'"
                + " height=" + figure.Figure.DrawnHeightWorld.ToString("0.###")
                + " sorting=" + figure.SortingOrder + " poolGrowth=" + runner.View.Pool.CreatedAfterWarmup);
        }

        /// <summary>
        /// The doctor walks, turns and stops on the arena floor: the pose comes from the frame's own
        /// movement, not from a copy of the input.
        /// </summary>
        [UnityTest]
        public IEnumerator Walking_runs_the_doctor_turning_follows_and_stopping_returns_to_idle()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            PlayerFigurePresenter figure = runner.View.PlayerFigure;
            Assert.That(figure, Is.Not.Null, "the scene wired no player figure");
            Assert.That(figure.IsReady, Is.True, "the player figure never built");

            var input = (BattleInputAdapter)runner.View.Input;

            input.SetOverride(1f, 0f);
            yield return WaitForTicks(runner, runner.StepsRun + (BattleConstants.TickRate / 2));

            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Run), "a walking doctor must run");
            Assert.That(figure.Facing, Is.EqualTo(1), "walking right must face right");
            Assert.That(
                figure.Animation,
                Is.EqualTo(FigureProbe.RunAnimation(figure.View)),
                "the run pose must be the profile's run animation");
            Assert.That(figure.SpeedUnitsPerSecond, Is.GreaterThan(0f), "the frame reported no movement");

            input.SetOverride(-1f, 0f);
            yield return WaitForTicks(runner, runner.StepsRun + (BattleConstants.TickRate / 2));

            Assert.That(figure.Facing, Is.EqualTo(-1), "walking left must turn the doctor");
            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Run));

            input.SetOverride(0f, 0f);
            yield return WaitForTicks(runner, runner.StepsRun + (BattleConstants.TickRate / 2));

            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Idle), "a stopped doctor must stand");
            Assert.That(figure.SpeedUnitsPerSecond, Is.EqualTo(0f).Within(0.0001f));

            TestContext.WriteLine(
                "walked: pose=" + figure.Pose + " facing=" + figure.Facing
                + " plays=" + figure.PlayCalls + " sorting=" + figure.SortingOrder);
        }

        /// <summary>
        /// A hit the floor publishes is a flinch, and the flinch ends where it started — through the
        /// same listener the event pump calls, not through a test-only shortcut.
        /// </summary>
        [UnityTest]
        public IEnumerator A_player_damaged_event_makes_the_doctor_flinch_and_return()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            PlayerFigurePresenter figure = runner.View.PlayerFigure;
            Assert.That(figure, Is.Not.Null, "the scene wired no player figure");
            Assert.That(figure.IsReady, Is.True, "the player figure never built");

            int playsBefore = figure.PlayCalls;

            // The listener is the pump's contract: one event in, one reaction. Playing the event the
            // floor publishes for the player's own damage is what a real hit does (the VFX channel).
            figure.OnBattleEvent(new BattleEvent(1, BattleEventType.PlayerDamaged, value: 1));

            Assert.That(figure.IsHurt, Is.True, "PlayerDamaged did not start the hurt pose");
            Assert.That(
                figure.Animation,
                Is.EqualTo(FigureProbe.HurtAnimation(figure.View)),
                "the hurt pose must be the profile's hurt animation");

            float deadline = Time.realtimeSinceStartup + 8f;
            while (figure.IsHurt && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(figure.IsHurt, Is.False, "the hurt animation never ended");
            Assert.That(
                figure.PlayCalls,
                Is.GreaterThanOrEqualTo(playsBefore + 2),
                "the flinch and the return to the pose are two plays");
            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Idle), "the figure did not return to its pose");

            TestContext.WriteLine("hurt: plays=" + playsBefore + "->" + figure.PlayCalls + " pose=" + figure.Pose);
        }

        /// <summary>
        /// The pause control holds the figure still, and only the pause's own two calls do it: the
        /// animation state's clock, never <c>Time.timeScale</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator Pausing_the_runner_holds_the_figures_animation()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            PlayerFigurePresenter figure = runner.View.PlayerFigure;
            Assert.That(figure, Is.Not.Null, "the scene wired no player figure");
            Assert.That(figure.IsReady, Is.True, "the player figure never built");

            yield return WaitForTicks(runner, runner.StepsRun + (BattleConstants.TickRate / 2));

            float timeScaleBefore = Time.timeScale;
            runner.SetPaused(true);

            Assert.That(figure.IsPaused, Is.True, "the figure was not told about the pause");
            Assert.That(
                Time.timeScale,
                Is.EqualTo(timeScaleBefore),
                "pausing the floor must not touch Time.timeScale");

            float held = figure.Figure.AnimationTime;
            long stepsWhenPaused = runner.StepsRun;

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(runner.StepsRun, Is.EqualTo(stepsWhenPaused), "a paused floor kept stepping");
            Assert.That(
                figure.Figure.AnimationTime,
                Is.EqualTo(held),
                "a paused figure's animation moved");

            runner.SetPaused(false);
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(figure.IsPaused, Is.False);
            Assert.That(runner.StepsRun, Is.GreaterThan(stepsWhenPaused), "a resumed floor is not stepping");
            Assert.That(
                figure.Figure.AnimationTime,
                Is.GreaterThan(held),
                "a resumed figure must animate again");

            TestContext.WriteLine(
                "pause: held=" + held.ToString("0.000") + " resumed=" + figure.Figure.AnimationTime.ToString("0.000")
                + " timeScale=" + Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Leaving_the_battle_scene_disposes_the_world_and_the_subscriptions()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            BattleHudView hud = Find<BattleHudView>();
            BattleHudState state = hud.State;
            BattleSpeed speed = runner.View.Speed;

            // The session the scene is running, and the ECS world it owns. The test is allowed to
            // look behind the seam; what matters is that nothing else does (asserted in EditMode).
            var session = runner.Session as EcsBattleSimulation;
            Assert.That(session, Is.Not.Null, "the scene is not running the EcsLite battle");
            EcsWorld world = session.Context.World;

            Assert.That(world.IsAlive(), Is.True, "the world was destroyed while the floor was running");
            Assert.That(hud.SubscriptionCount, Is.GreaterThan(0), "there is nothing to dispose, so the test proves nothing");
            Assert.That(speed.Multiplier.HasObservers, Is.True, "the speed control has no subscriber");

            // Leave the scene the way the player does.
            yield return LoadScene(TitleSceneName);
            yield return null;

            Assert.That(world.IsAlive(), Is.False, "leaving the Battle scene left its ECS world alive");
            Assert.That(hud == null, Is.True, "leaving the Battle scene left the HUD object behind");

            // The scene's scope owns the HUD state, the speed control and the presenter, so leaving
            // disposes them — and with them every R3 subscription they held.
            Assert.That(state.SpeedLabel.IsDisposed, Is.True, "the HUD's reactive state outlived the scene");
            Assert.That(speed.Multiplier.IsDisposed, Is.True, "the speed control outlived the scene");
        }

        [UnityTest]
        public IEnumerator Re_entering_the_battle_scene_starts_a_fresh_floor()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner first = Find<BattleSimulationRunner>();
            var firstSession = first.Session as EcsBattleSimulation;
            Assert.That(firstSession, Is.Not.Null);
            EcsWorld firstWorld = firstSession.Context.World;

            // A second of floor, so "the second visit is fresh" is a statement about a floor that
            // really ran. One frame is not enough: 30 Hz means the first tick needs two of them.
            yield return WaitForTicks(first, BattleConstants.TickRate);
            long stepsOfFirstFloor = first.StepsRun;
            Assert.That(stepsOfFirstFloor, Is.GreaterThanOrEqualTo(BattleConstants.TickRate));

            yield return LoadScene(TitleSceneName);
            yield return null;
            Assert.That(firstWorld.IsAlive(), Is.False, "the first floor's world survived leaving the scene");

            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner second = Find<BattleSimulationRunner>();
            Assert.That(second, Is.Not.Null);
            Assert.That(Find<BattleSceneDirector>().StartError, Is.Empty, "the second visit refused to start");
            Assert.That(second.StepsRun, Is.LessThan(stepsOfFirstFloor), "the second visit is not a fresh floor");
            Assert.That(second.View.Pool.CreatedAfterWarmup, Is.Zero);

            var secondSession = second.Session as EcsBattleSimulation;
            Assert.That(secondSession, Is.Not.Null);
            Assert.That(secondSession.Context.World.IsAlive(), Is.True, "the second visit has no live world");
            Assert.That(
                ReferenceEquals(secondSession.Context.World, firstWorld),
                Is.False,
                "the second visit reused the destroyed world");
        }

        // -------------------------------------------------------------------
        // Stage 17d: the status panel's portrait
        // -------------------------------------------------------------------

        /// <summary>
        /// The status panel's figure is the backend the arena runs, built once when the floor starts and
        /// then held still for the whole floor.
        /// </summary>
        /// <remarks>
        /// The EditMode suite proves the geometry and the build against the real asset; this proves
        /// the scene's own wiring — that Bind builds the portrait the player sees, that the build is a
        /// single picture (<c>CreatedAfterBuild</c> stays 0), that the Spine halfbody is the skeleton it
        /// names (<c>2110101</c>, the lesson of the 08c wiring bug: assert the asset, not the
        /// component) or that the 2D Animation backend draws the pack's portrait sprite instead, and
        /// that a second of frames neither plays nor advances anything.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_status_panels_portrait_is_the_still_halfbody_built_once()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            BattleHudPortrait portrait = Find<BattleHudPortrait>();

            Assert.That(portrait, Is.Not.Null, "the Battle scene has no BattleHudPortrait");
            Assert.That(
                portrait.IsBuilt, Is.True,
                "the status panel's portrait never built: " + portrait.ErrorCode);
            Assert.That(portrait.BuildCount, Is.EqualTo(1), "the portrait was built more than once");
            Assert.That(
                portrait.CreatedAfterBuild, Is.Zero,
                "an actor was created after the portrait's first build");

            // Stage 17f: the panel follows the arena's backend, so the shipped scene may carry the
            // rigged figure and the sprite portrait instead of the Spine bust.
            if (portrait.Backend == PlayerFigureBackend.GenericCute)
            {
                Assert.That(
                    portrait.Actor, Is.Null,
                    "the sprite portrait must not build the Spine halfbody beside the rigged figure");
                Assert.That(portrait.SpritePortrait, Is.Not.Null, "no portrait sprite was assigned");

                var image = portrait.SpriteImage;
                Assert.That(image, Is.Not.Null, "the sprite portrait drew no Image");
                Assert.That(image.sprite, Is.SameAs(portrait.SpritePortrait));
                Assert.That(portrait.PlayCalls, Is.Zero, "a still sprite plays nothing");

                int builds = portrait.BuildCount;
                yield return WaitForTicks(runner, runner.StepsRun + BattleConstants.TickRate);

                Assert.That(
                    portrait.BuildCount, Is.EqualTo(builds),
                    "a second of floor rebuilt the portrait's sprite");
                Assert.That(
                    portrait.SpriteImage.sprite, Is.SameAs(portrait.SpritePortrait),
                    "the portrait changed sprite while the floor ran");

                TestContext.WriteLine(
                    "portrait: backend=GenericCute built=" + portrait.IsBuilt + " builds=" + portrait.BuildCount
                    + " sprite='" + portrait.SpritePortrait.name + "' plays=" + portrait.PlayCalls
                    + " box=" + ((RectTransform)portrait.transform).rect.size);
                yield break;
            }
            Assert.That(
                portrait.SkeletonDataName, Is.EqualTo("2110101_SkeletonData"),
                "the status panel is not drawing the halfbody bust");
            Assert.That(
                portrait.Animation, Is.EqualTo(portrait.Profile.StillAnimation),
                "the portrait is not holding the profile's still animation");
            Assert.That(portrait.PlayCalls, Is.EqualTo(1), "the portrait played more than its one still frame");
            Assert.That(
                portrait.Actor.Graphic.timeScale, Is.Zero,
                "the SkeletonGraphic keeps its own clock, so Time.deltaTime could animate the panel");

            float held = portrait.AnimationTime;
            int played = portrait.PlayCalls;
            yield return WaitForTicks(runner, runner.StepsRun + BattleConstants.TickRate);

            Assert.That(
                portrait.AnimationTime, Is.EqualTo(held).Within(0.0001f),
                "a second of floor advanced the portrait; the still frame must not move");
            Assert.That(
                portrait.PlayCalls, Is.EqualTo(played),
                "the portrait started another animation as the floor ran; PlayCalls must not grow with frames");
            Assert.That(
                portrait.Animation, Is.EqualTo(portrait.Profile.StillAnimation),
                "the portrait changed animation; it draws one still frame");
            Assert.That(
                portrait.CreatedAfterBuild, Is.Zero,
                "a second of floor built the portrait's actor again");

            TestContext.WriteLine(
                "portrait: built=" + portrait.IsBuilt + " builds=" + portrait.BuildCount
                + " createdAfterBuild=" + portrait.CreatedAfterBuild + " skeleton=" + portrait.SkeletonDataName
                + " animation='" + portrait.Animation + "' time=" + portrait.AnimationTime.ToString("0.00")
                + " plays=" + portrait.PlayCalls + " box=" + ((RectTransform)portrait.transform).rect.size);
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static IEnumerator LoadScene(string sceneName)
        {
            // A shell this fixture owns: see SceneFlowHarness. The App scope persists once a
            // fixture loads Boot, so a test that did not reset would inherit a live run.
            SceneFlowHarness.ResetShell();
            yield return null;

            yield return SceneManager.LoadSceneAsync(SceneIndex(sceneName), LoadSceneMode.Single);
        }

        /// <summary>
        /// Waits for the scene's director to have built a floor, and fails with the director's own
        /// reason when it never does.
        /// </summary>
        private static IEnumerator WaitForFloorToStart()
        {
            float deadline = Time.realtimeSinceStartup + StartDeadlineSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                BattleSimulationRunner runner = Find<BattleSimulationRunner>();
                if (runner is not null && runner.IsRunning)
                {
                    yield break;
                }

                yield return null;
            }

            BattleSceneDirector director = Find<BattleSceneDirector>();
            Assert.Fail(
                "the Battle scene never started a floor; director says: '"
                + (director is null ? "no director in the scene" : director.StartError) + "'");
        }

        /// <summary>Waits until the runner has stepped <paramref name="ticks"/> fixed ticks.</summary>
        private static IEnumerator WaitForTicks(BattleSimulationRunner runner, long ticks)
        {
            float deadline = Time.realtimeSinceStartup + StartDeadlineSeconds;

            while (runner.StepsRun < ticks && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(runner.StepsRun, Is.GreaterThanOrEqualTo(ticks), "the floor stopped stepping");
        }

        private static T Find<T>()
            where T : UnityEngine.Object =>
            UnityEngine.Object.FindFirstObjectByType<T>();

        private static readonly Vector3[] WorldCornersBuffer = new Vector3[4];

        /// <summary>A rect transform's rectangle in the canvas' own space (bottom-left origin).</summary>
        private static Rect WorldRect(RectTransform rect)
        {
            rect.GetWorldCorners(WorldCornersBuffer);
            return new Rect(
                WorldCornersBuffer[0].x,
                WorldCornersBuffer[0].y,
                WorldCornersBuffer[2].x - WorldCornersBuffer[0].x,
                WorldCornersBuffer[2].y - WorldCornersBuffer[0].y);
        }

        private static string LabelText(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            return found is null ? null : found.GetComponent<TMP_Text>().text;
        }

        private static int SceneIndex(string sceneName)
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            for (int index = 0; index < sceneCount; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return index;
                }
            }

            Assert.Fail("scene '" + sceneName + "' is not in the build settings");
            return -1;
        }
    }
}
