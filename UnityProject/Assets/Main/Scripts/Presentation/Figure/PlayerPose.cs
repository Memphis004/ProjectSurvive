namespace ProjectSurvive.Game.Presentation.Figure
{
    /// <summary>The three things the player figure can be drawn doing.</summary>
    public enum PlayerPose
    {
        /// <summary>Standing still.</summary>
        Idle = 0,

        /// <summary>Moving above the profile's run threshold.</summary>
        Run = 1,

        /// <summary>Dead: one-shot, and no speed changes it.</summary>
        Dead = 2,
    }
}
