using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>
    /// The loop as a state machine: which scene is showing and which scene may follow it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Boot → Title → Hub ⇄ Encounter → Result → Title.</b> The transition, not the screen,
    /// decides what a scene may do: arriving at <see cref="SceneId.Title"/> ends the run, so
    /// "dispose the run scope" is a property of the transition instead of something each screen
    /// remembers to do (the bug Stage 10 named). Presentation performs the actual load through
    /// its own loader; this type only answers "is this move legal, and is the run in a state
    /// that allows it".
    /// </para>
    /// <para>
    /// Every refusal is typed (AGENTS rule 7) and leaves both the flow and the run untouched.
    /// </para>
    /// </remarks>
    public sealed class SceneFlow
    {
        private readonly RunSession _run;

        /// <summary>Creates the flow around a run.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="run"/> is null.</exception>
        public SceneFlow(RunSession run)
        {
            _run = run ?? throw new System.ArgumentNullException(nameof(run));
            CurrentScene = SceneId.Title;
        }

        /// <summary>The run this flow drives.</summary>
        public RunSession Run => _run;

        /// <summary>The scene on screen. Starts at <see cref="SceneId.Title"/> — the run exists only after the title starts one.</summary>
        public SceneId CurrentScene { get; private set; }

        /// <summary>
        /// Declares which scene is actually on screen.
        /// </summary>
        /// <remarks>
        /// A scene that loaded on its own (the Editor's scene list, a PlayMode fixture, a
        /// <c>?scene=</c> handover) did not come through <see cref="TryGo"/>, so the flow has no way to
        /// know it happened. Each scene's director calls this once when it starts, which keeps
        /// "where the loop is" a fact rather than an assumption.
        /// </remarks>
        public void SyncTo(SceneId scene) => CurrentScene = scene;

        /// <summary>True when <see cref="TryGo"/> would accept <paramref name="target"/> now.</summary>
        public bool CanGo(SceneId target) => Check(target).IsSuccess;

        /// <summary>
        /// Moves to <paramref name="target"/> if the loop allows it from <see cref="CurrentScene"/>.
        /// </summary>
        /// <returns>
        /// <see cref="RunErrors.TransitionNotAllowed"/> for an edge the loop does not have, or
        /// <see cref="RunErrors.NotStarted"/> when a run scene is asked for with no run.
        /// </returns>
        public Result TryGo(SceneId target)
        {
            Result allowed = Check(target);
            if (allowed.IsFailure)
            {
                return allowed;
            }

            CurrentScene = target;
            return Result.Ok();
        }

        private Result Check(SceneId target)
        {
            if (!IsEdgeAllowed(CurrentScene, target))
            {
                return RunErrors.Refuse(RunErrors.TransitionNotAllowed);
            }

            bool runScene = target == SceneId.Hub || target == SceneId.Encounter || target == SceneId.Result;
            if (runScene && !_run.IsStarted)
            {
                return RunErrors.Refuse(RunErrors.NotStarted);
            }

            return Result.Ok();
        }

        /// <summary>
        /// The edges of the loop. Adding a scene means adding its edges here, which is the one
        /// place that has to change.
        /// </summary>
        private static bool IsEdgeAllowed(SceneId from, SceneId to)
        {
            switch (from)
            {
                case SceneId.Boot:
                    return to == SceneId.Title;

                case SceneId.Title:
                    return to == SceneId.Hub;

                case SceneId.Hub:
                    return to == SceneId.Encounter || to == SceneId.Result;

                case SceneId.Encounter:
                    return to == SceneId.Hub || to == SceneId.Result;

                case SceneId.Result:
                    return to == SceneId.Title;

                default:
                    return false;
            }
        }
    }
}
