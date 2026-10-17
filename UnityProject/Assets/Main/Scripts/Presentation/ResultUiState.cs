using System;
using R3;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The UI-level state the Result screen renders: one headline, two lines and the two button
    /// captions. Deliberately tiny, exactly like <see cref="UiState"/>.
    /// </summary>
    /// <remarks>
    /// R3 carries <b>only</b> UI-level state here (AGENTS rule 10): finished strings, never a run
    /// number a view would have to format itself. Every value is written by
    /// <see cref="ResultPresenter"/> and only mirrored into a label by <see cref="ResultView"/>.
    /// Strings rather than numbers on purpose: the format string is player-facing text and lives in
    /// the localization table (AGENTS rule 5).
    /// </remarks>
    public sealed class ResultUiState : IDisposable
    {
        /// <summary>"Run complete" or "Run over" — the line the player reads first.</summary>
        public ReactiveProperty<string> Headline { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>How many encounters the run cleared.</summary>
        public ReactiveProperty<string> SummaryLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The seed the run was drawn from, so a report can be reproduced.</summary>
        public ReactiveProperty<string> SeedLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that starts a new run.</summary>
        public ReactiveProperty<string> RestartLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that leaves for the title.</summary>
        public ReactiveProperty<string> TitleLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <inheritdoc />
        public void Dispose()
        {
            Headline.Dispose();
            SummaryLine.Dispose();
            SeedLine.Dispose();
            RestartLabel.Dispose();
            TitleLabel.Dispose();
        }
    }
}
