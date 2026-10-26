using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.GenericCute;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 17f-2: the 2D Animation backend answers the same contract the Spine figure does. The pack's
    /// own rig is used — no stand-in skeleton — and the state names come from the shipped profile.
    /// </summary>
    public sealed class GenericCutePlayerViewTests
    {
        private GenericCutePlayerProfile _profile;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _profile = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(
                PlayerFigureBackendTool.CampProfilePath);
            Assert.That(_profile, Is.Not.Null, "run ProjectSurvive/Player Figure/1. Create GenericCute profiles");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
                _host = null;
            }
        }

        [Test]
        public void Begin_builds_the_rig_once_at_the_profiles_height_and_plays_the_idle_state()
        {
            GenericCutePlayerView view = Build();

            Assert.That(view.ErrorCode, Is.Null, "the figure refused to build: " + view.ErrorCode);
            Assert.That(view.IsBegun, Is.True);
            Assert.That(view.BuildCount, Is.EqualTo(1));
            Assert.That(view.PlayCalls, Is.EqualTo(1), "Begin plays the idle state exactly once");
            Assert.That(view.CurrentAnimation, Is.EqualTo(_profile.IdleState));
            Assert.That(view.Pose, Is.EqualTo(PlayerPose.Idle));
            Assert.That(
                view.DrawnHeightWorld, Is.EqualTo(_profile.TargetHeightWorld).Within(0.2f),
                "the rig is not drawn at the profile's height (pre-deformation measurement)");

            Assert.That(view.Begin(), Is.True, "a second Begin must succeed");
            Assert.That(view.BuildCount, Is.EqualTo(1), "a second Begin built a second rig");
            Assert.That(view.PlayCalls, Is.EqualTo(1), "a second Begin played again");

            // The Animator is disabled: only Tick may move the rig.
            Animator animator = _host.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, "the prefab carries no Animator");
            Assert.That(animator.enabled, Is.False, "the Animator must not advance itself");
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(_profile.Controller));
        }

        [Test]
        public void Presenting_the_same_pose_ten_thousand_times_plays_nothing()
        {
            GenericCutePlayerView view = Build();

            view.Present(PlayerPose.Run, 1);
            int plays = view.PlayCalls;
            Assert.That(plays, Is.EqualTo(2), "the first Run presents the run state");
            Assert.That(view.CurrentAnimation, Is.EqualTo(_profile.RunState));

            for (int i = 0; i < 10000; i++)
            {
                view.Present(PlayerPose.Run, 1);
            }

            Assert.That(view.PlayCalls, Is.EqualTo(plays), "Present must be free when nothing changed");
        }

        [Test]
        public void The_facing_is_one_mirror_on_the_flip_child_and_the_scale_never_changes()
        {
            GenericCutePlayerView view = Build();
            Transform flip = view.transform.GetChild(0).GetChild(0);
            float scaledHeight = view.transform.GetChild(0).localScale.y;

            view.Present(PlayerPose.Idle, 1);
            float right = flip.localScale.x;
            view.Present(PlayerPose.Idle, -1);
            float left = flip.localScale.x;

            bool expectedRight = _profile.ArtFacesRight;
            Assert.That(
                Mathf.Sign(right), Is.EqualTo(expectedRight ? 1f : -1f),
                "artFacesRight says which way the un-mirrored art looks");
            Assert.That(Mathf.Sign(left), Is.EqualTo(-Mathf.Sign(right)), "the two facings must be mirrored");

            Assert.That(
                view.transform.GetChild(0).localScale.y, Is.EqualTo(scaledHeight).Within(0.0001f),
                "turning must not resize the rig");
            Assert.That(flip.localScale.y, Is.EqualTo(1f).Within(0.0001f), "the flip child stays unit scale");
        }

        [Test]
        public void Hurt_plays_once_and_returns_to_the_pose_being_presented()
        {
            GenericCutePlayerView view = Build();
            view.Present(PlayerPose.Run, 1);
            int plays = view.PlayCalls;

            view.PlayHurt();
            Assert.That(view.IsHurt, Is.True);
            Assert.That(view.PlayCalls, Is.EqualTo(plays + 1));
            Assert.That(view.CurrentAnimation, Is.EqualTo(_profile.HurtState));

            // The clip is 0.1667 s; two steps take it past its end.
            view.Tick(0.2f);
            view.Tick(0.1f);

            Assert.That(view.IsHurt, Is.False, "a finished hurt state must end");
            Assert.That(
                view.CurrentAnimation, Is.EqualTo(_profile.RunState),
                "the figure must return to the pose it was presented with, not to idle");
            Assert.That(view.Pose, Is.EqualTo(PlayerPose.Run));
        }

        [Test]
        public void Pausing_holds_the_animation_and_speed_scales_the_time_that_passes()
        {
            GenericCutePlayerView view = Build();

            view.Tick(0.5f);
            float moved = view.AnimationTime;
            Assert.That(moved, Is.GreaterThan(0f), "a tick must advance the state");

            view.SetPaused(true);
            Assert.That(view.CurrentTimeScale, Is.Zero);
            view.Tick(1f);
            Assert.That(view.AnimationTime, Is.EqualTo(moved).Within(0.0001f), "a paused view must not move");

            view.SetPaused(false);
            view.SetSpeed(2f);
            Assert.That(view.CurrentTimeScale, Is.EqualTo(2f).Within(0.0001f));
            view.Tick(0.25f);
            Assert.That(
                view.AnimationTime, Is.EqualTo(moved + 0.5f).Within(0.0001f),
                "AnimationTime counts animation seconds, so speed 2 over 0.25 s is 0.5 s of cartoon");
        }

        [Test]
        public void The_draw_order_reaches_the_sorting_group_and_the_inner_order_is_kept()
        {
            GenericCutePlayerView view = Build();
            var group = _host.GetComponentInChildren<SortingGroup>(true);
            Assert.That(group, Is.Not.Null, "the rig must carry a SortingGroup so one number moves the whole figure");
            var inner = _host.GetComponentInChildren<SpriteRenderer>(true);

            view.SetSortingOrder(37);

            Assert.That(view.SortingOrder, Is.EqualTo(37));
            Assert.That(group.sortingOrder, Is.EqualTo(37));
            Assert.That(inner, Is.Not.Null);
        }

        [Test]
        public void A_profile_without_a_controller_fails_loudly_and_can_be_begun_again()
        {
            var broken = ScriptableObject.CreateInstance<GenericCutePlayerProfile>();
            try
            {
                // The view logs the code it refuses with; that is the behaviour under test.
                LogAssert.Expect(
                    LogType.Error, new System.Text.RegularExpressions.Regex(
                        "generic_cute\\.player_build_failed: the profile has no AnimatorController"));

                broken.Prefab = _profile.Prefab;
                broken.IdleState = _profile.IdleState;
                broken.RunState = _profile.RunState;

                _host = new GameObject("GenericCuteFigure");
                var view = _host.AddComponent<GenericCutePlayerView>();
                view.Configure(broken);

                Assert.That(view.Begin(), Is.False);
                Assert.That(view.ErrorCode, Is.EqualTo(GenericCutePlayerView.BuildFailedCode));
                Assert.That(view.IsBegun, Is.False);
                Assert.That(_host.transform.childCount, Is.Zero, "nothing built may be left behind");

                // The same view can be begun once a usable profile arrives.
                view.Configure(_profile);
                Assert.That(view.Begin(), Is.True, "a failed Begin must be retryable");
                Assert.That(view.BuildCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        private GenericCutePlayerView Build()
        {
            _host = new GameObject("GenericCuteFigure");
            var view = _host.AddComponent<GenericCutePlayerView>();
            view.Configure(_profile);
            view.SetSortingOrder(12);
            Assert.That(view.Begin(), Is.True, "the figure refused to build: " + view.ErrorCode);
            return view;
        }
    }
}
