using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.GenericCute;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Reads whatever figure the scenes carry, so a PlayMode case can assert the shipped scene
    /// without naming one backend.
    /// </summary>
    /// <remarks>
    /// <c>ProjectSurvive/Player Figure</c> switches both scenes between Spine and the 2D Animation
    /// rig (Stage 17f), and the switch is a scene edit rather than a runtime choice, so a suite that
    /// hard-coded <see cref="SpinePlayerView"/> went red the moment the scenes were switched. The
    /// common half of the contract is <see cref="IPlayerFigure"/>; only the profile's own asset type
    /// differs, which is what these three readers hide.
    /// </remarks>
    internal static class FigureProbe
    {
        /// <summary>The figure component in the loaded scene, whichever backend it is.</summary>
        internal static MonoBehaviour Find()
        {
            MonoBehaviour spine = Object.FindFirstObjectByType<SpinePlayerView>(FindObjectsInactive.Include);
            if (spine != null)
            {
                return spine;
            }

            return Object.FindFirstObjectByType<GenericCutePlayerView>(FindObjectsInactive.Include);
        }

        /// <summary>The backend's name for the report line.</summary>
        internal static string Backend(MonoBehaviour figure)
        {
            if (figure is SpinePlayerView)
            {
                return "Spine";
            }

            if (figure is GenericCutePlayerView)
            {
                return "GenericCute";
            }

            return figure == null ? "none" : figure.GetType().Name;
        }

        /// <summary>The animation the profile draws while standing.</summary>
        internal static string IdleAnimation(MonoBehaviour figure)
        {
            var spine = figure as SpinePlayerView;
            if (spine != null)
            {
                return spine.Profile == null ? null : spine.Profile.IdleAnimation;
            }

            var cute = figure as GenericCutePlayerView;
            return cute == null || cute.Profile == null ? null : cute.Profile.IdleState;
        }

        /// <summary>The animation the profile draws while walking.</summary>
        internal static string RunAnimation(MonoBehaviour figure)
        {
            var spine = figure as SpinePlayerView;
            if (spine != null)
            {
                return spine.Profile == null ? null : spine.Profile.RunAnimation;
            }

            var cute = figure as GenericCutePlayerView;
            return cute == null || cute.Profile == null ? null : cute.Profile.RunState;
        }

        /// <summary>The animation the profile draws for one hit.</summary>
        internal static string HurtAnimation(MonoBehaviour figure)
        {
            var spine = figure as SpinePlayerView;
            if (spine != null)
            {
                return spine.Profile == null ? null : spine.Profile.HurtAnimation;
            }

            var cute = figure as GenericCutePlayerView;
            return cute == null || cute.Profile == null ? null : cute.Profile.HurtState;
        }

        /// <summary>The height, in world units, the profile says the figure is drawn at.</summary>
        internal static float ProfileHeight(MonoBehaviour figure)
        {
            var spine = figure as SpinePlayerView;
            if (spine != null)
            {
                return spine.Profile == null ? 0f : spine.Profile.TargetHeightWorld;
            }

            var cute = figure as GenericCutePlayerView;
            return cute == null || cute.Profile == null ? 0f : cute.Profile.TargetHeightWorld;
        }

        /// <summary>True when the art is drawn facing the viewer's right.</summary>
        internal static bool ArtFacesRight(MonoBehaviour figure)
        {
            var spine = figure as SpinePlayerView;
            if (spine != null)
            {
                return spine.Profile != null && spine.Profile.ArtFacesRight;
            }

            var cute = figure as GenericCutePlayerView;
            return cute != null && cute.Profile != null && cute.Profile.ArtFacesRight;
        }

        /// <summary>
        /// The mirror the current facing wrote, <c>-1</c> or <c>1</c>.
        /// </summary>
        /// <remarks>
        /// Spine mirrors the skeleton's own transform; the 2D Animation view keeps a scaled root and
        /// mirrors a child of it, which the EditMode suite pins — this reads the same child.
        /// </remarks>
        internal static float MirrorScaleX(MonoBehaviour figure)
        {
            var spine = figure as SpinePlayerView;
            if (spine != null && spine.Actor != null)
            {
                return spine.Actor.SkeletonTransform.localScale.x;
            }

            Transform scaleRoot = figure.transform.GetChild(0);
            return scaleRoot.GetChild(0).localScale.x;
        }
    }
}
