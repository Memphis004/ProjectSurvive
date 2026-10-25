namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// Every spine asset this project loads by path, in the form <c>Resources.Load</c> wants it
    /// (relative to a <c>Resources</c> folder, no extension).
    /// </summary>
    /// <remarks>
    /// A path is code, not art: it is here so a test and the loader can agree on the one string
    /// instead of two copies drifting apart. Everything under <c>Resources/</c> is shipped with
    /// the player, so these paths are also the list of what the download pays for.
    /// </remarks>
    public static class SpineAssetPaths
    {
        /// <summary>The Mor's skeleton (<c>Assets/Resources/body_1110901/body_1110901_SkeletonData.asset</c>).</summary>
        public const string PlayerSkeletonData = "body_1110901/body_1110901_SkeletonData";
    }
}
