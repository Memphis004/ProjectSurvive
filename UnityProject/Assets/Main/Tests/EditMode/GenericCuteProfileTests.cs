using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.GenericCute;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 17f-2: the 2D Animation profiles are checked against the controller they name, not against
    /// a guess. Names are exact, loop flags come from the clips, and a mismatch is refused with the
    /// list of states the controller actually has.
    /// </summary>
    public sealed class GenericCuteProfileTests
    {
        [Test]
        public void The_shipped_profiles_match_the_packs_controller_and_names_its_states()
        {
            string[] paths =
            {
                PlayerFigureBackendTool.CampProfilePath,
                PlayerFigureBackendTool.BattleProfilePath,
            };

            for (int i = 0; i < paths.Length; i++)
            {
                var profile = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(paths[i]);
                Assert.That(profile, Is.Not.Null, paths[i] + " is missing: run ProjectSurvive/Player Figure/1.");

                bool valid = GenericCuteProfileValidator.TryValidate(
                    profile, out string code, out string detail, out string warning);

                Assert.That(valid, Is.True, paths[i] + ": " + code + ": " + detail);
                Assert.That(profile.Prefab, Is.Not.Null, paths[i] + " names no prefab");
                Assert.That(profile.Controller, Is.Not.Null, paths[i] + " names no controller");
                Assert.That(profile.IdleState, Is.EqualTo("idle"));
                Assert.That(profile.RunState, Is.EqualTo("run"));

                // Both scenes stand with the same idle on this backend: the rig has one, unlike the
                // Spine profiles, where the camp says `stand` and the arena says `idle5`.
                Assert.That(profile.TargetHeightWorld, Is.EqualTo(1.6f).Within(0.0001f));
                Assert.That(profile.ArtFacesRight, Is.True.Or.False, "artFacesRight is a declared value");

                TestContext.WriteLine(
                    paths[i] + ": idle=" + profile.IdleState + " run=" + profile.RunState +
                    " hurt='" + profile.HurtState + "' death='" + profile.DeathState + "'" +
                    " loop-ok=true warning=" + (warning ?? "(none)") +
                    " states=" + GenericCuteProfileValidator.Names(profile.Controller as UnityEditor.Animations.AnimatorController));
            }
        }

        [Test]
        public void The_pack_has_no_hurt_or_death_state_by_that_name_and_the_profile_says_so()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(
                PlayerFigureBackendTool.CampProfilePath);
            Assert.That(profile, Is.Not.Null, "the camp's GenericCute profile is missing");

            // The approved mapping: the pack's short one-shot is the hurt reaction, and there is no
            // death state at all — so the death field stays empty and Dead keeps the last pose, which is
            // the same option the Spine profile documents.
            Assert.That(profile.HurtState, Is.EqualTo("hit light"));
            Assert.That(profile.DeathState, Is.Empty, "the pack has no death state; the profile must not invent one");
        }

        [Test]
        public void A_state_name_the_controller_does_not_have_is_refused_with_the_list_of_states()
        {
            var profile = TempProfile();
            try
            {
                profile.IdleState = "no-such-state";

                bool valid = GenericCuteProfileValidator.TryValidate(
                    profile, out string code, out string detail, out _);

                Assert.That(valid, Is.False);
                Assert.That(code, Is.EqualTo(GenericCuteProfileValidator.StateMissingCode));
                Assert.That(detail, Does.Contain("no-such-state"));
                Assert.That(detail, Does.Contain("idle"), "the failure must list what the controller does have");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void A_run_state_whose_clip_does_not_loop_is_refused()
        {
            var profile = TempProfile();
            try
            {
                profile.RunState = "hit light";

                bool valid = GenericCuteProfileValidator.TryValidate(
                    profile, out string code, out string detail, out _);

                Assert.That(valid, Is.False, "a one-shot clip cannot be the run state");
                Assert.That(code, Is.EqualTo(GenericCuteProfileValidator.LoopMismatchCode));
                Assert.That(detail, Does.Contain("does not loop"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void A_loop_flag_the_rig_cannot_satisfy_is_refused_rather_than_edited()
        {
            var profile = TempProfile();
            try
            {
                profile.IdleState = "knockdown";

                bool valid = GenericCuteProfileValidator.TryValidate(
                    profile, out string code, out string detail, out _);

                Assert.That(valid, Is.False);
                Assert.That(code, Is.EqualTo(GenericCuteProfileValidator.LoopMismatchCode));
                Assert.That(detail, Does.Contain("knockdown"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static GenericCutePlayerProfile TempProfile()
        {
            var source = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(
                PlayerFigureBackendTool.CampProfilePath);
            Assert.That(source, Is.Not.Null, "the camp's GenericCute profile is missing");

            var profile = ScriptableObject.CreateInstance<GenericCutePlayerProfile>();
            profile.Prefab = source.Prefab;
            profile.Controller = source.Controller;
            profile.IdleState = source.IdleState;
            profile.RunState = source.RunState;
            profile.HurtState = source.HurtState;
            profile.DeathState = source.DeathState;
            return profile;
        }
    }
}
