using UnityEngine;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// What <see cref="Camp.CampPlayerView"/> and <c>PlayerFigurePresenter</c> ask of the thing that
    /// draws the doctor in the world — the seam a second backend plugs into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists</b> (the one abstraction Stage 17f was told to add): the user asked for a
    /// second backend so the player figure can be drawn either by Spine or by a Unity 2D Animation
    /// rig, chosen from serialized settings without editing code. Two implementations behind one call
    /// contract is exactly the case rule 12 allows; nothing else in the project is abstracted here,
    /// and no third backend is being prepared for.
    /// </para>
    /// <para>
    /// <b>Only what the callers use is on it.</b> The members below are read from the two call sites,
    /// not copied from Spine: <c>CampPlayerView</c> presents a pose and a facing every frame and asks
    /// for the run threshold; <c>PlayerFigurePresenter</c> begins once, places the figure, presents,
    /// ticks, pauses, changes speed, plays the hurt reaction and reads the diagnostics the battle
    /// report prints. A backend keeps everything else private — a Spine profile, a Spine actor, a
    /// skeleton animation state or an Animator controller never appear in this file.
    /// </para>
    /// <para>
    /// <b>Unity cannot serialize an interface</b>, so a scene field stays a <see cref="MonoBehaviour"/>
    /// (the backend's own component) and the caller resolves this interface off it with
    /// <c>GetComponent&lt;IPlayerFigure&gt;()</c>. Two implementations on one object would be
    /// ambiguous, which is a wiring mistake worth failing loudly on, not worth designing around.
    /// </para>
    /// <para>
    /// <b>The contract, in one paragraph:</b> an implementation owns no clock — <see cref="Tick"/> is
    /// the only thing that advances the animation, and pause/speed never go through
    /// <c>Time.timeScale</c>; <see cref="Present"/> is called every frame and does nothing when the
    /// pose and the facing are the ones already drawn; <see cref="Begin"/> builds once and is
    /// idempotent; a failed <see cref="Begin"/> leaves <see cref="ErrorCode"/> set and can be retried;
    /// <see cref="PlayHurt"/> plays one one-shot and returns to the presented pose; the figure is
    /// scaled once to the profile's height, and the facing is a single mirror applied in one place.
    /// </para>
    /// </remarks>
    public interface IPlayerFigure
    {
        /// <summary>True once <see cref="Begin"/> produced a drawable figure.</summary>
        bool IsBegun { get; }

        /// <summary>The <c>noun.problem</c> code that made <see cref="Begin"/> fail, or null.</summary>
        string ErrorCode { get; }

        /// <summary>The pose currently drawn.</summary>
        PlayerPose Pose { get; }

        /// <summary>+1 right, -1 left.</summary>
        int FaceDirection { get; }

        /// <summary>True while a one-shot hurt animation is playing.</summary>
        bool IsHurt { get; }

        /// <summary>How many animations this figure has started (Begin's idle is the first).</summary>
        int PlayCalls { get; }

        /// <summary>How many figures this view has built (one per successful <see cref="Begin"/>).</summary>
        int BuildCount { get; }

        /// <summary>The animation playing now, or null when nothing does.</summary>
        string CurrentAnimation { get; }

        /// <summary>Seconds the current animation has been playing; frozen while paused.</summary>
        float AnimationTime { get; }

        /// <summary>The height the figure was drawn at, in world units (0 before <see cref="Begin"/>).</summary>
        float DrawnHeightWorld { get; }

        /// <summary>The playback speed the last <see cref="SetSpeed"/> or <see cref="SetPaused"/> left: 0 while paused.</summary>
        float CurrentTimeScale { get; }

        /// <summary>The draw order the figure draws at.</summary>
        int SortingOrder { get; }

        /// <summary>
        /// The speed, in world units a second, above which the caller draws
        /// <see cref="PlayerPose.Run"/>. It lives on the profile, so the pose rule reads it here
        /// rather than from a backend's own asset type.
        /// </summary>
        float RunSpeedThreshold { get; }

        /// <summary>Builds the figure once. Idempotent; false comes with <see cref="ErrorCode"/>.</summary>
        bool Begin();

        /// <summary>Draws this pose and facing; a no-op when both are already drawn.</summary>
        void Present(PlayerPose pose, int facing);

        /// <summary>Plays the hurt reaction once, then returns to the presented pose.</summary>
        void PlayHurt();

        /// <summary>Freezes or resumes the animation (never <c>Time.timeScale</c>).</summary>
        void SetPaused(bool paused);

        /// <summary>Playback speed of the animation; 0 or less freezes it.</summary>
        void SetSpeed(float speed);

        /// <summary>Draw order of the whole figure.</summary>
        void SetSortingOrder(int order);

        /// <summary>Advances the animation by <paramref name="deltaTime"/>; the only clock the figure has.</summary>
        void Tick(float deltaTime);
    }
}
