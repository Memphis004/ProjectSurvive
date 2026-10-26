using NUnit.Framework;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 17b: the doctor's figure is driven by the frame, the event stream and the scene's
    /// controls — and by nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The figure is the <b>real</b> one (<c>SpinePlayerView</c> built from the shipped profile and
    /// skeleton), because the thing under test is the wiring between the battle's contract and the
    /// skeleton's animation state: a stub view would prove the arithmetic and nothing about the
    /// bones. Every frame here is a real <see cref="BattleFrameBuffer"/> publication, so the speed
    /// the pose is chosen from is the formula the running scene uses.
    /// </para>
    /// <para>
    /// What each case pins: the pose and facing come from <c>(cur − prev) × tickRate</c> and the
    /// frame's own <c>dx</c>; the draw order is <c>BattleWorldMap.SortingOrder</c> and is written only
    /// when it changes; the hurt reaction arrives through a real <see cref="BattleEventPump"/> channel
    /// and returns; a lost floor is the only thing that draws the death pose; a paused view's
    /// animation does not move; and the sprite bridge stops drawing the player's pooled view once the
    /// figure is the player.
    /// </para>
    /// </remarks>
    public sealed class BattlePlayerFigureTests
    {
        private const float TickSeconds = 1f / BattleConstants.TickRate;

        /// <summary>A tick's worth of movement at <paramref name="unitsPerSecond"/>.</summary>
        private static float Step(float unitsPerSecond) => unitsPerSecond * TickSeconds;

        // -------------------------------------------------------------------
        // Pose and facing, from the frame's two ends
        // -------------------------------------------------------------------

        [Test]
        public void The_pose_and_the_facing_come_from_the_frames_own_movement()
        {
            using var fixture = new FigureFixture();

            // A tick the player did not move on: standing.
            fixture.Publish(tick: 1, x: 0f, y: 0f);
            fixture.Accept();
            Assert.That(fixture.Figure.Pose, Is.EqualTo(PlayerPose.Idle), "a still player must stand");
            Assert.That(fixture.Figure.SpeedUnitsPerSecond, Is.Zero);

            // Walking right at 4 u/s: the entity moved 4 × (1/30) units in one tick.
            fixture.Publish(tick: 2, x: Step(4f), y: 0f);
            fixture.Accept();
            Assert.That(
                fixture.Figure.SpeedUnitsPerSecond,
                Is.EqualTo(4f).Within(0.01f),
                "(cur − prev) × tickRate is the speed, and it is the frame's own number");
            Assert.That(fixture.Figure.Pose, Is.EqualTo(PlayerPose.Run), "a walking player must run");
            Assert.That(fixture.Figure.Facing, Is.EqualTo(1), "walking right faces right");

            // Walking left at the same speed turns the figure and keeps the run.
            float left = fixture.LastX - Step(4f);
            fixture.Publish(tick: 3, x: left, y: 0f);
            fixture.Accept();
            Assert.That(fixture.Figure.Facing, Is.EqualTo(-1), "walking left faces left");
            Assert.That(fixture.Figure.Pose, Is.EqualTo(PlayerPose.Run));

            // Stopped: the hysteresis needs the speed to fall below half the threshold, and a still
            // player does.
            fixture.Publish(tick: 4, x: left, y: 0f);
            fixture.Accept();
            Assert.That(fixture.Figure.Pose, Is.EqualTo(PlayerPose.Idle), "a stopped player must stand");
            Assert.That(
                fixture.Figure.Facing,
                Is.EqualTo(-1),
                "standing still keeps the last direction: the doctor must not spin on the spot");
        }

        [Test]
        public void One_pose_change_plays_one_animation_and_the_frames_do_not_play_more()
        {
            using var fixture = new FigureFixture();

            fixture.Publish(tick: 1, x: 0f, y: 0f);
            fixture.Accept();
            int playsAtStart = fixture.Figure.PlayCalls;

            // Ten ticks standing still, each drawn: the same pose may not restart the animation.
            for (int tick = 2; tick <= 11; tick++)
            {
                fixture.Publish(tick, x: 0f, y: 0f);
                fixture.Accept();
                fixture.Figure.Render(0.5f, TickSeconds);
            }

            Assert.That(
                fixture.Figure.PlayCalls,
                Is.EqualTo(playsAtStart),
                "drawing the same pose ten times played the animation again");

            // One change of pose is exactly one play.
            fixture.Publish(tick: 12, x: Step(4f), y: 0f);
            fixture.Accept();
            Assert.That(fixture.Figure.PlayCalls, Is.EqualTo(playsAtStart + 1));
            Assert.That(fixture.Figure.Animation, Is.EqualTo(fixture.Profile.RunAnimation));
        }

        // -------------------------------------------------------------------
        // Draw order
        // -------------------------------------------------------------------

        [Test]
        public void The_draw_order_is_the_world_maps_and_is_written_only_when_it_changes()
        {
            using var fixture = new FigureFixture();

            fixture.Publish(tick: 1, x: 0f, y: 0f);
            fixture.Accept();
            fixture.Figure.Render(1f, TickSeconds);

            Assert.That(
                fixture.Figure.SortingOrder,
                Is.EqualTo(BattleWorldMap.SortingOrder(RenderEntityKind.Player, 0f)),
                "the figure's order is BattleWorldMap's rule, not a number of its own");
            Assert.That(fixture.Figure.SortingWrites, Is.EqualTo(1), "the first draw writes the order once");

            for (int frame = 0; frame < 10; frame++)
            {
                fixture.Figure.Render(0.5f, TickSeconds);
            }

            Assert.That(fixture.Figure.SortingWrites, Is.EqualTo(1), "an unchanged row must not write again");

            // Two rows further away (simulation −Y is toward the camera), the order changes once.
            fixture.Publish(tick: 2, x: 0f, y: -10f);
            fixture.Accept();
            fixture.Figure.Render(1f, TickSeconds);

            Assert.That(
                fixture.Figure.SortingOrder,
                Is.EqualTo(BattleWorldMap.SortingOrder(RenderEntityKind.Player, -10f)));
            Assert.That(fixture.Figure.SortingWrites, Is.EqualTo(2), "crossing a row writes the order once");
        }

        [Test]
        public void The_sprite_bridge_stops_drawing_the_player_once_the_figure_stands_there()
        {
            using var art = new BattlePlaceholderArt();
            var root = new GameObject("figure-bridge-root");
            var pool = new EntityViewPool(root.transform, art);
            pool.Warmup();

            var bridge = new BattleRenderBridge(pool, art);
            var snapshot = new BattleFrameSnapshot();
            var buffer = new BattleFrameBuffer();

            try
            {
                PublishFrame(buffer, tick: 1, kind: RenderEntityKind.Player, x: 0f, y: 0f, present: true);
                snapshot.CopyFrom(buffer);
                bridge.ApplySnapshot(snapshot);
                Assert.That(bridge.BoundCount, Is.EqualTo(1), "the pooled sprite draws the player by default");

                // The figure is the player now: the same tick binds nothing, and the bound view goes
                // back to the pool on the release pass.
                bridge.PlayerDrawnElsewhere = true;
                bridge.ApplySnapshot(snapshot);

                Assert.That(bridge.BoundCount, Is.Zero, "the player's pooled view must not be drawn beside the figure");
                Assert.That(pool.InUseCount, Is.Zero);
                Assert.That(pool.CreatedAfterWarmup, Is.Zero, "skipping the player must not create a view");

                // A ghost is still drawn: the skip is the player's kind, not the bridge's job.
                PublishFrame(buffer, tick: 2, kind: RenderEntityKind.Enemy, x: 0f, y: 0f, present: true);
                snapshot.CopyFrom(buffer);
                bridge.ApplySnapshot(snapshot);
                Assert.That(bridge.BoundCount, Is.EqualTo(1), "a ghost must still be drawn");
            }
            finally
            {
                bridge.Dispose();
                pool.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        // -------------------------------------------------------------------
        // Events: hurt through the real pump, and never a rule
        // -------------------------------------------------------------------

        [Test]
        public void A_player_damaged_event_plays_the_hit_animation_and_returns_to_the_pose()
        {
            using var fixture = new FigureFixture();
            Assert.That(fixture.Profile.HurtAnimation, Is.Not.Empty, "the shipped profile names a hit animation");

            fixture.Publish(tick: 1, x: 0f, y: 0f);
            fixture.Accept();
            int playsBefore = fixture.Figure.PlayCalls;

            var ring = new BattleEventRing();
            using var pump = new BattleEventPump();
            pump.AddListener(BattleEventChannel.Vfx, fixture.Figure);

            ring.Write(new BattleEvent(0, BattleEventType.PlayerDamaged, value: 3));
            pump.Drain(ring);

            Assert.That(fixture.Figure.IsHurt, Is.True, "PlayerDamaged on the VFX channel must play the hurt pose");
            Assert.That(fixture.Figure.Animation, Is.EqualTo(fixture.Profile.HurtAnimation));
            Assert.That(fixture.Figure.PlayCalls, Is.EqualTo(playsBefore + 1));

            int guard = 0;
            while (fixture.Figure.IsHurt && guard < 1200)
            {
                fixture.Figure.Render(0f, TickSeconds);
                guard++;
            }

            Assert.That(fixture.Figure.IsHurt, Is.False, "the hurt animation never ended within 40 s of frames");
            Assert.That(
                fixture.Figure.Pose,
                Is.EqualTo(PlayerPose.Idle),
                "the figure must return to the pose the hurt interrupted");
            Assert.That(
                fixture.Figure.PlayCalls,
                Is.EqualTo(playsBefore + 2),
                "the return to the pose is one more play, not one per frame");
        }

        [Test]
        public void An_event_that_is_not_the_players_damage_changes_nothing()
        {
            using var fixture = new FigureFixture();

            fixture.Publish(tick: 1, x: 0f, y: 0f);
            fixture.Accept();
            int plays = fixture.Figure.PlayCalls;

            fixture.Figure.OnBattleEvent(new BattleEvent(0, BattleEventType.DamageDealt, value: 5));
            fixture.Figure.OnBattleEvent(new BattleEvent(1, BattleEventType.EnemyKilled));
            fixture.Figure.OnBattleEvent(new BattleEvent(2, BattleEventType.BossPhaseChanged));

            Assert.That(fixture.Figure.IsHurt, Is.False);
            Assert.That(fixture.Figure.PlayCalls, Is.EqualTo(plays), "a ghost's damage is not the doctor's reaction");
        }

        // -------------------------------------------------------------------
        // The floor's phase: only a lost floor draws the death pose
        // -------------------------------------------------------------------

        [Test]
        public void A_lost_floor_draws_the_death_pose_and_a_cleared_one_does_not()
        {
            using var fixture = new FigureFixture();

            fixture.Publish(tick: 1, x: 0f, y: 0f);
            fixture.Accept();

            fixture.Figure.End(Result(BattlePhase.Won));
            Assert.That(fixture.Figure.Pose, Is.EqualTo(PlayerPose.Idle), "a cleared floor does not kill the doctor");
            Assert.That(fixture.Figure.IsDead, Is.False);

            fixture.Figure.End(Result(BattlePhase.Lost));
            Assert.That(fixture.Figure.IsDead, Is.True);
            Assert.That(fixture.Figure.Pose, Is.EqualTo(PlayerPose.Dead), "the Lost phase is the death pose");
            Assert.That(fixture.Figure.Animation, Is.EqualTo(fixture.Profile.DeathAnimation));

            // A hit that lands on the death frame may not replace the fall.
            int plays = fixture.Figure.PlayCalls;
            fixture.Figure.PlayHurt();
            Assert.That(fixture.Figure.PlayCalls, Is.EqualTo(plays), "a dead figure does not flinch");
        }

        // -------------------------------------------------------------------
        // Pause and speed: values that change, not per-frame writes
        // -------------------------------------------------------------------

        [Test]
        public void Pausing_holds_the_animation_and_resuming_lets_it_run_again()
        {
            using var fixture = new FigureFixture();

            fixture.Publish(tick: 1, x: Step(4f), y: 0f);
            fixture.Accept();

            fixture.Figure.Render(1f, TickSeconds);
            Assert.That(fixture.Figure.IsPaused, Is.False);

            fixture.Figure.SetPaused(true);
            Assert.That(fixture.Figure.IsPaused, Is.True);

            float held = fixture.View.AnimationTime;
            for (int frame = 0; frame < 10; frame++)
            {
                fixture.Figure.Render(0f, TickSeconds);
            }

            Assert.That(
                fixture.View.AnimationTime,
                Is.EqualTo(held),
                "a paused figure's animation moved, so Time.timeScale or a tick leaked through");

            fixture.Figure.SetPaused(false);
            for (int frame = 0; frame < 5; frame++)
            {
                fixture.Figure.Render(0f, TickSeconds);
            }

            Assert.That(
                fixture.View.AnimationTime,
                Is.GreaterThan(held),
                "a resumed figure must animate again");
        }

        [Test]
        public void The_battle_speed_is_written_once_per_change_and_scales_the_animation()
        {
            using var fixture = new FigureFixture();

            fixture.Publish(tick: 1, x: Step(4f), y: 0f);
            fixture.Accept();

            fixture.Speed.Set(2f);
            int writes = fixture.Figure.SpeedWrites;
            Assert.That(fixture.View.CurrentTimeScale, Is.EqualTo(2f).Within(0.0001f));

            // Setting the same speed again is not a change and must not write.
            fixture.Speed.Set(2f);
            Assert.That(fixture.Figure.SpeedWrites, Is.EqualTo(writes), "an unchanged speed was written again");

            // At 2× the runner hands the view a doubled frame; the figure may not be scaled twice.
            float before = fixture.View.AnimationTime;
            fixture.Figure.Render(0f, TickSeconds * 2f);
            float advanced = fixture.View.AnimationTime - before;

            Assert.That(
                advanced,
                Is.EqualTo(TickSeconds * 2f).Within(0.001f),
                "2× should advance the animation by 2 ticks of time, not 4");
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static BattleResult Result(BattlePhase outcome) =>
            new BattleResult
            {
                Outcome = outcome,
                EndReason = BattleEndReason.PlayerDied,
                SurvivedTicks = 1,
                FinalHp = 0,
                CurrencyEarned = 0,
                Kills = 0,
                RemainingConsumables = new int[0],
            };

        private static void PublishFrame(
            BattleFrameBuffer buffer, long tick, RenderEntityKind kind, float x, float y, bool present)
        {
            buffer.BeginFrame(tick);
            buffer.SetHud(new HudState { Hp = 10, MaxHp = 10, FloorDurationTicks = 1800 });
            if (present)
            {
                buffer.Add(new RenderEntity(new EntityRef(1, (int)tick), kind, x, y));
            }

            buffer.Publish();
        }

        /// <summary>
        /// One real figure on a host object, the presenter that drives it, and the frame buffer the
        /// frames are published from.
        /// </summary>
        private sealed class FigureFixture : System.IDisposable
        {
            private readonly GameObject _host;
            private readonly BattleFrameBuffer _buffer = new BattleFrameBuffer();
            private readonly BattleFrameSnapshot _snapshot = new BattleFrameSnapshot();

            internal FigureFixture()
            {
                Profile = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(SpineProfileTool.BattleProfileAssetPath);
                Assert.That(Profile, Is.Not.Null, SpineProfileTool.BattleProfileAssetPath + " is missing");

                _host = new GameObject("BattlePlayerFigureTest");
                View = _host.AddComponent<SpinePlayerView>();
                View.Configure(Profile);

                Speed = new BattleSpeed();
                Figure = new PlayerFigurePresenter(Speed);
                Figure.Attach(View, BattleCameraRig.PitchDegrees);

                Assert.That(Figure.Begin(), Is.True, "the figure refused to build: " + Figure.ErrorCode);
            }

            internal SpinePlayerProfile Profile { get; }

            internal SpinePlayerView View { get; }

            internal PlayerFigurePresenter Figure { get; }

            internal BattleSpeed Speed { get; }

            /// <summary>Where the last published player stood, so a test can walk one tick from it.</summary>
            internal float LastX { get; private set; }

            /// <summary>Publishes one frame with the player at (x, y).</summary>
            internal void Publish(long tick, float x, float y)
            {
                _buffer.BeginFrame(tick);
                _buffer.SetHud(new HudState { Hp = 10, MaxHp = 10, FloorDurationTicks = 1800 });
                _buffer.Add(new RenderEntity(new EntityRef(1, 0), RenderEntityKind.Player, x, y));
                _buffer.Publish();

                LastX = x;
            }

            /// <summary>Copies the last publication and hands it to the presenter, as a tick does.</summary>
            internal void Accept()
            {
                _snapshot.CopyFrom(_buffer);
                Figure.AcceptSnapshot(_snapshot);
            }

            public void Dispose()
            {
                Figure.Dispose();
                Speed.Dispose();
                Object.DestroyImmediate(_host);
            }
        }
    }
}
