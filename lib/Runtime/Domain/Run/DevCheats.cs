namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The one switch behind every dev cheat, and the rule that keeps it out of a release build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 10's DevMenu ("อมตะ, เพิ่มเบี้ย, ข้ามชั้น, spawn ผี") is a development tool. The GDD
    /// requires that a release build has it <b>off</b> and that a test confirms it, so the decision
    /// is a value in Domain — plain booleans, no <c>UnityEngine</c> — rather than a
    /// <c>#if</c> scattered through the screens that would also have to be trusted in a build
    /// nobody can inspect.
    /// </para>
    /// <para>
    /// Three links, each independently checkable:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <see cref="Allowed"/> — what the composition root decided at startup. It starts at
    /// <see cref="AllowedInRelease"/> (false) and only <see cref="Configure"/> can raise it, so the
    /// value a caller sees before any composition is the release value.
    /// </description></item>
    /// <item><description>
    /// <see cref="Allows"/> — the pure decision, so "a release build is off" is a test of one
    /// expression rather than a claim about a build nobody ran.
    /// </description></item>
    /// <item><description>
    /// <c>BattleSetup.DevCheatsAllowed</c> — the per-floor copy the session reads. A run copies
    /// <see cref="Allowed"/> into it when it departs, so a floor started while cheats are off
    /// cannot be cheated through even if they are turned on afterwards.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class DevCheats
    {
        /// <summary>
        /// What a release build allows. Named rather than written as a literal at each call site so
        /// the test that asserts it reads the same symbol the shell uses.
        /// </summary>
        public const bool AllowedInRelease = false;

        /// <summary>
        /// The decision itself: cheats belong to a development build and to the Editor.
        /// </summary>
        /// <param name="isDevelopmentBuild"><c>Debug.isDebugBuild</c> — true in a Development build and in the Editor.</param>
        /// <param name="isEditor"><c>Application.isEditor</c>.</param>
        /// <returns>True when dev cheats may be used.</returns>
        public static bool Allows(bool isDevelopmentBuild, bool isEditor) => isDevelopmentBuild || isEditor;

        /// <summary>
        /// What this run of the game allows. False until <see cref="Configure"/> says otherwise,
        /// which is the release answer.
        /// </summary>
        public static bool Allowed { get; private set; } = AllowedInRelease;

        /// <summary>
        /// Records the decision. Called once by the composition root (the App scope), which is the
        /// only place that knows whether this build is a development build.
        /// </summary>
        public static void Configure(bool isDevelopmentBuild, bool isEditor) =>
            Allowed = Allows(isDevelopmentBuild, isEditor);

        /// <summary>
        /// Puts the switch back to <see cref="AllowedInRelease"/>.
        /// </summary>
        /// <remarks>
        /// For a test or a fixture that must not inherit the decision of the shell it happens to run
        /// after: the flag is process-wide, so a play session in the Editor leaves it on.
        /// </remarks>
        public static void Reset() => Allowed = AllowedInRelease;
    }
}
