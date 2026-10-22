using System;
using R3;

namespace ProjectSurvive.Game.Presentation.Battle.Pause
{
    /// <summary>
    /// Everything the pause overlay shows: whether it is up, its four strings and the last refusal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// R3-level UI state only (AGENTS rule 10): a flag and localized strings. It carries no tick, no
    /// frame and no entity — the floor's own state stays in the runner and the run, and the overlay
    /// never reads them to decide what to draw.
    /// </para>
    /// <para>
    /// The strings are written by <see cref="BattlePausePresenter"/> when it opens the overlay, not
    /// once at bind: a run can switch language between floors, and looking the four lines up on open
    /// costs four dictionary reads per press rather than a subscription per label.
    /// </para>
    /// </remarks>
    public sealed class BattlePauseState : IDisposable
    {
        /// <summary>True while the overlay is on screen and the floor is held still.</summary>
        public ReactiveProperty<bool> IsOpen { get; } = new ReactiveProperty<bool>(false);

        /// <summary>The overlay's heading (\"หยุดพัก\").</summary>
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The warning that a retreat forfeits the floor.</summary>
        public ReactiveProperty<string> Warning { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The resume control's caption (\"เล่นต่อ\").</summary>
        public ReactiveProperty<string> ResumeLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The leave control's caption (\"กลับศาลา\").</summary>
        public ReactiveProperty<string> LeaveLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The HUD control that opens the overlay (\"หยุด\").</summary>
        public ReactiveProperty<string> OpenLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <inheritdoc />
        public void Dispose()
        {
            IsOpen.Dispose();
            Title.Dispose();
            Warning.Dispose();
            ResumeLabel.Dispose();
            LeaveLabel.Dispose();
            OpenLabel.Dispose();
        }
    }
}
