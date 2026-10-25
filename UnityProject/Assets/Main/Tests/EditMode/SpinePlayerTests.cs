using System;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.SpineActors;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 17a acceptance: the pose rule, the loader's codes, the profile against the real
    /// skeleton, and the view's two promises — one Play per pose change and nothing allocated —
    /// plus the hurt animation returning where it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here runs against the shipped asset
    /// (<c>Resources/body_1110901/body_1110901_SkeletonData</c>), never a stub: the point of the
    /// stage is that a real export with unfamiliar animation and skin names can be wired up without
    /// a guess, and a fake skeleton would prove nothing about that.
    /// </para>
    /// <para>
    /// The allocation measurement is <see cref="GC.GetAllocatedBytesForCurrentThread"/> — exact and
    /// per-thread, and it needs no Profiler window. The loop is warmed first so the first call's own
    /// JIT work is not counted as Present's cost.
    /// </para>
    /// </remarks>
    public sealed class SpinePlayerTests
    {
        // -------------------------------------------------------------------
        // The pose rule (pure, no Unity types)
        // -------------------------------------------------------------------

        [Test]
        public void Dead_wins_over_a_moving_speed()
        {
            Assert.That(
                PlayerPoseSelector.Select(0f, PlayerPose.Idle, true, 0.1f),
                Is.EqualTo(PlayerPose.Dead));
            Assert.That(
                PlayerPoseSelector.Select(99f, PlayerPose.Run, true, 0.1f),
                Is.EqualTo(PlayerPose.Dead),
                "a dead figure must not be drawn running no matter the speed");
        }

        [Test]
        public void Run_starts_at_the_threshold_and_stays_until_half_of_it()
        {
            const float threshold = 0.4f;

            Assert.That(
                PlayerPoseSelector.Select(threshold, PlayerPose.Idle, false, threshold),
                Is.EqualTo(PlayerPose.Run),
                "the threshold itself is a run");
            Assert.That(
                PlayerPoseSelector.Select(threshold - 0.01f, PlayerPose.Idle, false, threshold),
                Is.EqualTo(PlayerPose.Idle));

            // The hysteresis: the same speed that is too slow to start a run does not stop one.
            float between = threshold * 0.75f;
            Assert.That(
                PlayerPoseSelector.Select(between, PlayerPose.Idle, false, threshold),
                Is.EqualTo(PlayerPose.Idle));
            Assert.That(
                PlayerPoseSelector.Select(between, PlayerPose.Run, false, threshold),
                Is.EqualTo(PlayerPose.Run),
                "a speed between the exit and the enter threshold must keep the current pose");

            Assert.That(
                PlayerPoseSelector.Select(threshold * PlayerPoseSelector.HysteresisRatio - 0.01f, PlayerPose.Run, false, threshold),
                Is.EqualTo(PlayerPose.Idle),
                "below half the threshold the run ends");

            Assert.That(
                PlayerPoseSelector.Select(-threshold, PlayerPose.Idle, false, threshold),
                Is.EqualTo(PlayerPose.Run),
                "backwards movement is still movement");
        }

        [Test]
        public void Facing_keeps_its_direction_inside_the_dead_zone()
        {
            const float deadZone = 0.1f;

            Assert.That(PlayerPoseSelector.Facing(0f, 1, deadZone), Is.EqualTo(1));
            Assert.That(PlayerPoseSelector.Facing(0f, -1, deadZone), Is.EqualTo(-1));
            Assert.That(PlayerPoseSelector.Facing(0.09f, -1, deadZone), Is.EqualTo(-1), "a drift right inside the dead zone must not turn the figure");
            Assert.That(PlayerPoseSelector.Facing(-0.09f, 1, deadZone), Is.EqualTo(1), "a drift left inside the dead zone must not turn the figure");
            Assert.That(PlayerPoseSelector.Facing(0.2f, -1, deadZone), Is.EqualTo(1));
            Assert.That(PlayerPoseSelector.Facing(-0.2f, 1, deadZone), Is.EqualTo(-1));
            Assert.That(PlayerPoseSelector.Facing(-0.2f, 0, deadZone), Is.EqualTo(-1));
        }

        // -------------------------------------------------------------------
        // The loader
        // -------------------------------------------------------------------

        [Test]
        public void An_empty_path_is_its_own_refusal()
        {
            SkeletonDataAsset asset;
            string code;

            Assert.That(SpineSkeletonLoader.TryLoad(null, out asset, out code), Is.False);
            Assert.That(code, Is.EqualTo(SpineSkeletonLoader.PathEmptyCode));
            Assert.That(asset, Is.Null);

            Assert.That(SpineSkeletonLoader.TryLoad("   ", out asset, out code), Is.False);
            Assert.That(code, Is.EqualTo(SpineSkeletonLoader.PathEmptyCode));
        }

        [Test]
        public void A_path_with_nothing_behind_it_is_missing_not_invalid()
        {
            SkeletonDataAsset asset;
            string code;

            Assert.That(SpineSkeletonLoader.TryLoad("body_1110901/no_such_skeleton", out asset, out code), Is.False);
            Assert.That(code, Is.EqualTo(SpineSkeletonLoader.MissingCode));
            Assert.That(asset, Is.Null);
        }

        [Test]
        public void The_player_skeleton_loads_and_produces_a_skeleton()
        {
            SkeletonDataAsset asset;
            string code;

            Assert.That(
                SpineSkeletonLoader.TryLoad(SpineAssetPaths.PlayerSkeletonData, out asset, out code),
                Is.True,
                code + " for '" + SpineAssetPaths.PlayerSkeletonData + "'");
            Assert.That(code, Is.Null);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.GetSkeletonData(true), Is.Not.Null);
            Assert.That(
                asset.GetSkeletonData(true).Animations.Count,
                Is.GreaterThan(0),
                "the skeleton must contain at least one animation");
        }

        // -------------------------------------------------------------------
        // The profile the tool created
        // -------------------------------------------------------------------

        [Test]
        public void The_created_profile_validates_against_the_real_skeleton()
        {
            SpinePlayerProfile profile = LoadProfile();
            SkeletonData data = LoadSkeletonData();

            string code;
            string detail;
            Assert.That(
                profile.TryValidate(data, out code, out detail),
                Is.True,
                code + ": " + detail);

            Assert.That(profile.IdleAnimation, Is.Not.Empty);
            Assert.That(profile.RunAnimation, Is.Not.Empty);
            Assert.That(
                data.FindAnimation(profile.IdleAnimation),
                Is.Not.Null,
                "the profile's idle animation is not in the skeleton");
            Assert.That(
                data.FindAnimation(profile.RunAnimation),
                Is.Not.Null,
                "the profile's run animation is not in the skeleton");

            // Every skin part the tool picked exists, and the parts cover more than one slot holder.
            Assert.That(profile.SkinParts, Is.Not.Empty, "the skeleton has body/head/face part skins; the profile should pick them");
            foreach (SpineActorBase.SkinPart part in profile.SkinParts)
            {
                Assert.That(data.FindSkin(part.skin), Is.Not.Null, "skin part '" + part.part + "' -> '" + part.skin + "'");
            }
        }

        [Test]
        public void The_profile_refuses_an_animation_the_skeleton_does_not_have()
        {
            SkeletonData data = LoadSkeletonData();
            SpinePlayerProfile profile = NewProfile("no_such_idle", "run");

            try
            {
                string code;
                string detail;
                Assert.That(profile.TryValidate(data, out code, out detail), Is.False);
                Assert.That(code, Is.EqualTo(SpinePlayerProfile.AnimationMissingCode));
                StringAssert.Contains("no_such_idle", detail);
                StringAssert.Contains(
                    data.Animations.Items[0].Name,
                    detail,
                    "the detail must list the animations the skeleton does offer");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void The_profile_refuses_a_skin_the_skeleton_does_not_have()
        {
            SkeletonData data = LoadSkeletonData();
            SpinePlayerProfile profile = NewProfile("idle1", "run");
            profile.SetSkinParts(new[]
            {
                new SpineActorBase.SkinPart { part = "body", skin = "body_does_not_exist" },
            });

            try
            {
                string code;
                string detail;
                Assert.That(profile.TryValidate(data, out code, out detail), Is.False);
                Assert.That(code, Is.EqualTo(SpinePlayerProfile.SkinMissingCode));
                StringAssert.Contains("body_does_not_exist", detail);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        // -------------------------------------------------------------------
        // The view
        // -------------------------------------------------------------------

        [Test]
        public void Begin_builds_one_figure_at_the_profiles_height_and_is_idempotent()
        {
            SpinePlayerProfile profile = LoadProfile();
            GameObject host = new GameObject("SpinePlayerViewTest");

            try
            {
                SpinePlayerView view = host.AddComponent<SpinePlayerView>();
                view.Configure(profile);

                Assert.That(view.Begin(), Is.True, "Begin failed: " + view.ErrorCode);
                Assert.That(view.ErrorCode, Is.Null);
                Assert.That(view.Actor, Is.Not.Null);
                Assert.That(view.Actor.Animation, Is.Not.Null);
                Assert.That(view.BuildCount, Is.EqualTo(1));
                Assert.That(view.PlayCalls, Is.EqualTo(1), "Begin plays the idle pose once");
                Assert.That(view.Pose, Is.EqualTo(PlayerPose.Idle));

                Assert.That(
                    view.DrawnHeightWorld,
                    Is.EqualTo(profile.TargetHeightWorld).Within(0.001f),
                    "the figure is not drawn at the profile's height");

                Assert.That(profile.WorldMaterial, Is.Not.Null, "the profile names no world material");
                Assert.That(
                    view.Actor.Animation.GetComponent<MeshRenderer>().sharedMaterial,
                    Is.SameAs(profile.WorldMaterial),
                    "the mesh is not drawn with the profile's world material");

                // The scale belongs to the actor's root; the skeleton transform keeps the flip.
                Assert.That(view.Actor.transform.localScale.x, Is.GreaterThan(0f));
                Assert.That(
                    Mathf.Abs(view.Actor.SkeletonTransform.localScale.y),
                    Is.EqualTo(1f).Within(0.0001f),
                    "the flip scale lives on the skeleton transform and must stay unit");

                Assert.That(view.Begin(), Is.True, "a second Begin must report the same success");
                Assert.That(view.BuildCount, Is.EqualTo(1), "Begin must be idempotent");
                Assert.That(view.PlayCalls, Is.EqualTo(1), "a second Begin must not restart the animation");

                TestContext.WriteLine(
                    "begin: build=" + view.BuildCount + " plays=" + view.PlayCalls +
                    " height=" + view.DrawnHeightWorld.ToString("0.###") +
                    " idle=" + profile.IdleAnimation + " run=" + profile.RunAnimation +
                    " hurt=" + profile.HurtAnimation + " death=" + profile.DeathAnimation);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Presenting_the_same_pose_ten_thousand_times_plays_once_and_allocates_nothing()
        {
            SpinePlayerProfile profile = LoadProfile();
            GameObject host = new GameObject("SpinePlayerViewAllocTest");

            try
            {
                SpinePlayerView view = host.AddComponent<SpinePlayerView>();
                view.Configure(profile);
                Assert.That(view.Begin(), Is.True, "Begin failed: " + view.ErrorCode);

                // Warm-up: the first call after Begin is the one that would pay for anything lazy.
                for (int i = 0; i < 16; i++)
                {
                    view.Present(PlayerPose.Idle, 1);
                }

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++)
                {
                    view.Present(PlayerPose.Idle, 1);
                }

                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(view.PlayCalls, Is.EqualTo(1), "presenting the same pose restarted the animation");
                Assert.That(allocated, Is.EqualTo(0), "Present allocated " + allocated + " bytes over 10 000 calls");

                // A real change is what plays, and the facing follows the direction.
                view.Present(PlayerPose.Run, -1);
                Assert.That(view.PlayCalls, Is.EqualTo(2));
                Assert.That(view.Pose, Is.EqualTo(PlayerPose.Run));
                Assert.That(view.FaceDirection, Is.EqualTo(-1));
                Assert.That(
                    view.Actor.FlipX,
                    Is.EqualTo(profile.ArtFacesRight),
                    "a figure walking left is flipped when its art faces right, and only then");

                view.Present(PlayerPose.Run, -1);
                Assert.That(view.PlayCalls, Is.EqualTo(2), "the same facing must not flip or replay");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void The_hurt_animation_returns_to_the_pose_it_interrupted()
        {
            SpinePlayerProfile profile = LoadProfile();
            Assert.That(
                profile.HurtAnimation,
                Is.Not.Empty,
                "the shipped skeleton has a 'hit' animation; the profile should name it");

            GameObject host = new GameObject("SpinePlayerViewHurtTest");

            try
            {
                SpinePlayerView view = host.AddComponent<SpinePlayerView>();
                view.Configure(profile);
                Assert.That(view.Begin(), Is.True, "Begin failed: " + view.ErrorCode);

                view.Present(PlayerPose.Idle, 1);
                int playsBefore = view.PlayCalls;

                view.PlayHurt();
                Assert.That(view.IsHurt, Is.True, "the hurt reaction did not start");
                Assert.That(view.PlayCalls, Is.EqualTo(playsBefore + 1));

                int guard = 0;
                while (view.IsHurt && guard < 1200)
                {
                    view.Tick(1f / 30f);
                    guard++;
                }

                Assert.That(view.IsHurt, Is.False, "the hurt animation never ended within 40 s of ticks");
                Assert.That(
                    view.PlayCalls,
                    Is.EqualTo(playsBefore + 2),
                    "the pose must be played again once the one-shot ends");
                Assert.That(view.Pose, Is.EqualTo(PlayerPose.Idle), "the figure did not return to its pose");

                TestContext.WriteLine("hurt: ticks=" + guard + " plays=" + view.PlayCalls);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static SpinePlayerProfile LoadProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(SpineProfileTool.ProfileAssetPath);
            Assert.That(
                profile,
                Is.Not.Null,
                SpineProfileTool.ProfileAssetPath + " is missing: run ProjectSurvive/Spine/2. Create player profile");
            return profile;
        }

        private static SkeletonData LoadSkeletonData()
        {
            SkeletonDataAsset asset;
            string code;
            Assert.That(
                SpineSkeletonLoader.TryLoad(SpineAssetPaths.PlayerSkeletonData, out asset, out code),
                Is.True,
                code);
            return asset.GetSkeletonData(true);
        }

        /// <summary>An in-memory profile for the refusal tests (never saved).</summary>
        private static SpinePlayerProfile NewProfile(string idle, string run)
        {
            var profile = ScriptableObject.CreateInstance<SpinePlayerProfile>();
            profile.SkeletonResourcePath = SpineAssetPaths.PlayerSkeletonData;
            profile.IdleAnimation = idle;
            profile.RunAnimation = run;
            profile.HurtAnimation = string.Empty;
            profile.DeathAnimation = string.Empty;
            profile.SetSkinParts(null);
            return profile;
        }
    }
}
