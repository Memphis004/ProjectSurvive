using System;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.SpineActors;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        /// <summary>
        /// Each scene stands with its own idle, and the art is mirrored for the other direction only
        /// (Stage 17e).
        /// </summary>
        /// <remarks>
        /// The camp stands with <c>stand</c> and the arena with <c>idle5</c>; everything else about the
        /// two profiles is the same figure. <c>artFacesRight</c> is false because the art itself faces
        /// the viewer's left in all three poses — read off close-up captures of the running game
        /// (<c>artifacts/stage17e/zoom-A-run-right.png</c>, <c>zoom-B-run-left.png</c>,
        /// <c>zoom-C-stand-right.png</c>), not assumed from the export's name.
        /// </remarks>
        [Test]
        public void The_two_profiles_name_their_own_idle_and_the_art_faces_left()
        {
            SkeletonData data = LoadSkeletonData();

            SpinePlayerProfile camp = LoadProfile(SpineProfileTool.CampProfileAssetPath);
            SpinePlayerProfile battle = LoadProfile(SpineProfileTool.BattleProfileAssetPath);

            Assert.That(camp.IdleAnimation, Is.EqualTo("stand"), "the camp stands with `stand`");
            Assert.That(battle.IdleAnimation, Is.EqualTo("idle5"), "the arena stands with `idle5`");

            foreach (SpinePlayerProfile profile in new[] { camp, battle })
            {
                string code;
                string detail;
                Assert.That(profile.TryValidate(data, out code, out detail), Is.True, code + ": " + detail);
                Assert.That(profile.ArtFacesRight, Is.False, "the art faces the viewer's left in stand, idle5 and run");

                // The four weapon slots no skin fills are declared, so the build log stays honest.
                foreach (string slot in new[] { "w_dao", "w_zhang", "w_qiang", "w_jian" })
                {
                    Assert.That(
                        profile.ExpectedEmptySlots, Does.Contain(slot),
                        "the profile does not declare the weapon slot '" + slot + "' as empty on purpose");
                }
            }

            // The one field that differs is the idle; the rest of the look is identical.
            Assert.That(camp.TargetHeightWorld, Is.EqualTo(battle.TargetHeightWorld));
            Assert.That(camp.WorldMaterial, Is.SameAs(battle.WorldMaterial));
        }

        /// <summary>
        /// A figure walking right is the mirrored one, and one walking left is not — the sign the
        /// mirror is written to, read from the SkeletonTransform the flip lives on.
        /// </summary>
        [Test]
        public void Walking_right_mirrors_the_art_and_walking_left_leaves_it_alone()
        {
            SpinePlayerProfile profile = LoadProfile(SpineProfileTool.BattleProfileAssetPath);
            GameObject host = new GameObject("SpinePlayerViewFacingTest");

            try
            {
                SpinePlayerView view = host.AddComponent<SpinePlayerView>();
                view.Configure(profile);
                Assert.That(view.Begin(), Is.True, "Begin failed: " + view.ErrorCode);

                view.Present(PlayerPose.Run, 1);
                Assert.That(view.Actor.FlipX, Is.True, "walking right must mirror an art that faces left");
                Assert.That(
                    view.Actor.SkeletonTransform.localScale.x, Is.EqualTo(-1f),
                    "the mirror is a negative x scale on the skeleton transform");

                view.Present(PlayerPose.Run, -1);
                Assert.That(view.Actor.FlipX, Is.False, "walking left must not mirror an art that already faces left");
                Assert.That(
                    view.Actor.SkeletonTransform.localScale.x, Is.EqualTo(1f),
                    "the unmirrored figure keeps a positive x scale");

                // Standing still keeps the last direction, not a default one.
                view.Present(PlayerPose.Idle, -1);
                Assert.That(view.Actor.SkeletonTransform.localScale.x, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// The profile draws with the outlined material, and that material is not a UI shader and
        /// carries the page texture plus a visible ink width (Stage 17b follow-up).
        /// </summary>
        /// <remarks>
        /// The failure this catches is the one a rendered capture cannot: a profile re-pointed at the
        /// plain world material still draws the doctor, and every animation test still passes — the
        /// figure has simply lost the edge that makes it readable on the arena's bright haze. Spine's
        /// own <c>Spine/Outline/Skeleton</c> is refused by name here too: it is two passes in one
        /// SubShader, and URP renders one, so it would draw the ink with no body at all.
        /// </remarks>
        [Test]
        public void The_profile_draws_with_the_outlined_world_material()
        {
            SpinePlayerProfile profile = LoadProfile();
            Material material = profile.WorldMaterial;

            Assert.That(material, Is.Not.Null, "the profile has no world material");
            Assert.That(
                material.shader.name,
                Is.EqualTo("ProjectSurvive/SpinePlayerOutline"),
                "the profile's material is not the outlined one (shader '" + material.shader.name + "')");
            Assert.That(
                material.shader.name,
                Is.Not.EqualTo("Spine/Outline/Skeleton"),
                "URP draws one pass per material, so spine's two-pass outline shader loses the body");

            Assert.That(material.GetTexture("_MainTex"), Is.Not.Null, "the outline material has no atlas page");
            Assert.That(material.GetFloat("_OutlineWidth"), Is.GreaterThan(0f), "an outline of zero width draws nothing");
            Assert.That(
                material.GetFloat("_OutlineWidth"),
                Is.LessThanOrEqualTo(2f),
                "an ink wider than two screen pixels closes the gaps inside a 45-pixel figure");
            Assert.That(material.GetFloat("_Cutoff"), Is.InRange(0.05f, 0.95f), "the edge cutoff is outside the alpha range");

            Color ink = material.GetColor("_OutlineColor");
            Assert.That(ink.a, Is.EqualTo(1f).Within(0.001f), "a translucent outline does not separate the figure");
            Assert.That(
                0.2126f * ink.r + 0.7152f * ink.g + 0.0722f * ink.b,
                Is.LessThan(0.2f),
                "the outline is not dark enough to read as ink against a lit floor");
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
        // The camp scene (Stage 17c)
        // -------------------------------------------------------------------

        /// <summary>
        /// ศาลาพัก draws the doctor with the same figure and the same look as the arena: the scene
        /// carries the <b>profile asset</b>, not a skeleton path, and the world builds the view from it
        /// when the camp starts (ADR 0037 §2).
        /// </summary>
        /// <remarks>
        /// This is the check a scene that was never rebuilt after this stage fails: the component
        /// compiles and the pose rule is green, but the serialized profile is empty, so the sala would
        /// quietly keep drawing the placeholder silhouette. The figure itself needs play mode (it is
        /// built at the camp's start), which is what `CampWorldPlayModeTests` is for.
        /// </remarks>
        [Test]
        public void The_camp_scene_names_the_profile_its_figure_is_built_from()
        {
            const string campScenePath = "Assets/Main/Scenes/Camp.unity";

            Scene scene = SceneManager.GetSceneByPath(campScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(campScenePath, OpenSceneMode.Additive);
            }

            try
            {
                CampWorldView world = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    world = world != null ? world : roots[i].GetComponentInChildren<CampWorldView>(true);
                }

                Assert.That(world, Is.Not.Null, "the Camp scene has no CampWorldView");
                Assert.That(
                    world.PlayerFigureProfile, Is.Not.Null,
                    "" + campScenePath + " names no SpinePlayerProfile, so the sala would draw the " +
                    "placeholder silhouette: rebuild it with ProjectSurvive/Stage 09/5. Create Camp scene");

                Assert.That(
                    world.PlayerFigureProfile, Is.SameAs(LoadProfile(SpineProfileTool.CampProfileAssetPath)),
                    "the camp must name its own profile: it stands with `stand`, the arena with `idle5`");
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// The arena carries its own profile — the one whose idle is <c>idle5</c> — so a scene rebuilt
        /// after this stage cannot quietly keep standing with <c>stand</c> through a stale reference.
        /// </summary>
        [Test]
        public void The_battle_scene_names_the_arena_profile_for_its_figure()
        {
            const string battleScenePath = "Assets/Main/Scenes/Battle.unity";

            Scene scene = SceneManager.GetSceneByPath(battleScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                SpinePlayerView figure = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length && figure == null; i++)
                {
                    figure = roots[i].GetComponentInChildren<SpinePlayerView>(true);
                }

                Assert.That(figure, Is.Not.Null, battleScenePath + " has no player figure view");
                Assert.That(
                    figure.Profile, Is.SameAs(LoadProfile(SpineProfileTool.BattleProfileAssetPath)),
                    "the arena must name its own profile (idle5)");
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static SpinePlayerProfile LoadProfile()
        {
            return LoadProfile(SpineProfileTool.BattleProfileAssetPath);
        }

        private static SpinePlayerProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(path);
            Assert.That(
                profile,
                Is.Not.Null,
                path + " is missing: run ProjectSurvive/Spine/2. Create player profiles");
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
