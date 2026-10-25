using Spine.Unity;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// Loads a <see cref="SkeletonDataAsset"/> from a resource path and says, in a code, why it
    /// could not.
    /// </summary>
    /// <remarks>
    /// The three codes are <c>noun.problem</c> like every other refusal in the project, so a caller
    /// can print a localized row for the actual fault instead of one message for all of them. The
    /// skeleton is parsed here (not at first draw) because "the asset exists" and "the asset
    /// produces a skeleton" are different failures: a broken atlas passes the first and fails the
    /// second, and only the second one draws nothing.
    /// </remarks>
    public static class SpineSkeletonLoader
    {
        /// <summary>The path was null, empty or whitespace.</summary>
        public const string PathEmptyCode = "spine.skeleton_path_empty";

        /// <summary>Nothing in Resources answers that path.</summary>
        public const string MissingCode = "spine.skeleton_missing";

        /// <summary>The asset is there but cannot produce a SkeletonData (skeleton file, atlas or material missing).</summary>
        public const string InvalidCode = "spine.skeleton_invalid";

        /// <summary>
        /// Loads <paramref name="resourcePath"/> and reports the skeleton's own verdict.
        /// Returns false with one of the three codes; <paramref name="asset"/> is null then.
        /// </summary>
        public static bool TryLoad(string resourcePath, out SkeletonDataAsset asset, out string errorCode)
        {
            asset = null;
            errorCode = null;

            if (string.IsNullOrEmpty(resourcePath) || resourcePath.Trim().Length == 0)
            {
                errorCode = PathEmptyCode;
                return false;
            }

            asset = Resources.Load<SkeletonDataAsset>(resourcePath.Trim());
            if (asset == null)
            {
                errorCode = MissingCode;
                return false;
            }

            // quiet: the caller reports the code, and a failed probe should not also throw a
            // red line into the console of every frame that asks.
            if (asset.GetSkeletonData(true) == null)
            {
                asset = null;
                errorCode = InvalidCode;
                return false;
            }

            return true;
        }
    }
}
