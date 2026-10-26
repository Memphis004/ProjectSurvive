namespace ProjectSurvive.Game.Presentation.Figure
{
    /// <summary>
    /// Which implementation draws the player figure. A scene carries the choice as a serialized value
    /// (the Editor tool writes it, the scene builder reads it), so switching backends is a rebuild, not
    /// a code change. There is no runtime switching: one backend is built once, at Begin.
    /// </summary>
    public enum PlayerFigureBackend
    {
        /// <summary>The Spine skeleton the game shipped with (the default).</summary>
        Spine = 0,

        /// <summary>A Unity 2D Animation rig (SpriteSkin bones + Animator).</summary>
        GenericCute = 1,
    }
}
