using System;
using R3;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The UI-level state the Result screen renders: one headline, four summary lines and the two
    /// button captions. Deliberately tiny, exactly like <see cref="UiState"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// R3 carries <b>only</b> UI-level state here (AGENTS rule 10): finished strings, never a run
    /// number a view would have to format itself. Every value is written by
    /// <see cref="ResultPresenter"/> and only mirrored into a label by <see cref="ResultView"/>.
    /// </para>
    /// <para>
    /// Strings rather than numbers on purpose. The screen's own numbers (floor reached, ghosts
    /// defeated, bia, HP) are composed into one localized line each by the presenter, because the
    /// format string is player-facing text and lives in the localization table — a view that took
    /// the integers and joined them with a slash would be formatting player text in code
    /// (AGENTS rule 5).
    /// </para>
    /// </remarks>
    public sealed class ResultUiState : IDisposable
    {
        /// <summary>"Victory" or "Defeat" — the line the player reads first.</summary>
        public ReactiveProperty<string> Headline { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>How far the run got, e.g. "Reached floor 3/5".</summary>
        public ReactiveProperty<string> FloorLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Ghosts the run defeated.</summary>
        public ReactiveProperty<string> KillsLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Bia the run ended with.</summary>
        public ReactiveProperty<string> BiaLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>HP the run ended with.</summary>
        public ReactiveProperty<string> HpLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the restart control.</summary>
        public ReactiveProperty<string> RestartLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the back-to-title control.</summary>
        public ReactiveProperty<string> TitleLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <inheritdoc />
        public void Dispose()
        {
            Headline.Dispose();
            FloorLine.Dispose();
            KillsLine.Dispose();
            BiaLine.Dispose();
            HpLine.Dispose();
            RestartLabel.Dispose();
            TitleLabel.Dispose();
        }
    }
}
