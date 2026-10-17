using System;
using R3;

namespace ProjectSurvive.Game.Presentation.Hub
{
    /// <summary>
    /// The UI-level state the Hub screen renders: two strings, one counter line and two captions.
    /// </summary>
    /// <remarks>
    /// R3 carries only UI-level state (AGENTS rule 10). Every value is written by
    /// <see cref="HubPresenter"/>; the view mirrors it and nothing else.
    /// </remarks>
    public sealed class HubUiState : IDisposable
    {
        /// <summary>The screen's heading.</summary>
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>One line of instructions.</summary>
        public ReactiveProperty<string> Instructions { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>How many encounters this run has cleared.</summary>
        public ReactiveProperty<string> EncountersLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that enters the next encounter.</summary>
        public ReactiveProperty<string> DepartLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that ends the run.</summary>
        public ReactiveProperty<string> FinishLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <inheritdoc />
        public void Dispose()
        {
            Title.Dispose();
            Instructions.Dispose();
            EncountersLine.Dispose();
            DepartLabel.Dispose();
            FinishLabel.Dispose();
        }
    }
}
