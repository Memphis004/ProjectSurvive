using System;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// One loadable scene of the game (Stage 03 built three of them; Camp, Battle
    /// and Result arrive with Stages 08–10).
    /// </summary>
    /// <remarks>
    /// Kept next to <see cref="RunFlowState"/> rather than in Presentation so the
    /// run flow can answer "which scene should be showing?" without the
    /// Presentation layer having to own the rule. The name is what
    /// <c>SceneManager</c> loads, so the two must stay in step — the mapping is
    /// asserted by a test.
    /// </remarks>
    public enum SceneId
    {
        /// <summary>The title screen (<c>Title.unity</c>).</summary>
        Title = 0,

        /// <summary>The camp (ศาลาพัก) — Stage 09.</summary>
        Camp = 1,

        /// <summary>The battle arena — Stage 08.</summary>
        Battle = 2,

        /// <summary>The run summary — Stage 10.</summary>
        Result = 3,
    }

    /// <summary>The one place a <see cref="RunFlowState"/> becomes a <see cref="SceneId"/>.</summary>
    public static class SceneFlowMap
    {
        /// <summary>
        /// The scene that renders <paramref name="state"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The state is unknown.</exception>
        public static SceneId SceneOf(RunFlowState state)
        {
            switch (state)
            {
                case RunFlowState.Title: return SceneId.Title;
                case RunFlowState.Camp: return SceneId.Camp;
                case RunFlowState.Battle: return SceneId.Battle;
                case RunFlowState.Result: return SceneId.Result;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown run flow state.");
            }
        }
    }
}
