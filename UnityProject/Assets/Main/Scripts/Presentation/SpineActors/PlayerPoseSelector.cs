namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// Turns a speed and a horizontal direction into the pose and the facing to draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pure function with no UnityEngine and no Spine type: the rule "walking starts above this
    /// speed and stops below half of it" is a presentation rule that deserves a test without an
    /// Editor, and the animation system is not allowed to decide it.
    /// </para>
    /// <para>
    /// The hysteresis exists because a figure that stands at exactly one speed threshold flickers
    /// between two animations on the frame the number crosses it. Entering run needs the full
    /// threshold; leaving it needs half, so a speed hovering around the line stays in one pose.
    /// </para>
    /// </remarks>
    public static class PlayerPoseSelector
    {
        /// <summary>The fraction of the run threshold a running figure must fall below to stop running.</summary>
        public const float HysteresisRatio = 0.5f;

        /// <summary>
        /// The pose for this frame. <paramref name="dead"/> wins over everything: a dead figure is
        /// dead at any speed.
        /// </summary>
        public static PlayerPose Select(float speed, PlayerPose current, bool dead, float runThreshold)
        {
            if (dead)
            {
                return PlayerPose.Dead;
            }

            float magnitude = speed < 0f ? -speed : speed;

            if (runThreshold <= 0f)
            {
                // A non-positive threshold means "any movement is a run"; that is a configuration
                // error the profile refuses, and this keeps the function total anyway.
                return magnitude > 0f ? PlayerPose.Run : PlayerPose.Idle;
            }

            if (current == PlayerPose.Run)
            {
                float exit = runThreshold * HysteresisRatio;
                return magnitude > exit ? PlayerPose.Run : PlayerPose.Idle;
            }

            // Idle, and Dead-but-alive (a resurrected figure starts from standing).
            return magnitude >= runThreshold ? PlayerPose.Run : PlayerPose.Idle;
        }

        /// <summary>
        /// The horizontal direction to draw: <c>+1</c> right, <c>-1</c> left. A direction inside
        /// the dead zone keeps <paramref name="current"/> — a figure being pushed sideways, or a
        /// stick with a little drift, must not spin on the spot.
        /// </summary>
        public static int Facing(float dx, int current, float deadZone)
        {
            if (deadZone < 0f)
            {
                deadZone = -deadZone;
            }

            if (dx > deadZone)
            {
                return 1;
            }

            if (dx < -deadZone)
            {
                return -1;
            }

            if (current > 0)
            {
                return 1;
            }

            if (current < 0)
            {
                return -1;
            }

            return 1;
        }
    }
}
