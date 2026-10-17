using System;
using R3;

namespace ProjectSurvive.Game.Presentation.Encounter
{
    /// <summary>
    /// The UI-level state the Encounter screen renders: a heading, one progress line and two captions.
    /// </summary>
    /// <remarks>
    /// The template's encounter is a placeholder — it has no simulation of its own. A game replaces this
    /// screen with its arena and drives the fixed tick from there; what it keeps is the shape: the
    /// screen asks Application for every string and reports an outcome back.
    /// </remarks>
    public sealed class EncounterUiState : IDisposable
    {
        /// <summary>The screen's heading.</summary>
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Which encounter of the run this is.</summary>
        public ReactiveProperty<string> ProgressLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that wins the encounter.</summary>
        public ReactiveProperty<string> WinLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that loses it.</summary>
        public ReactiveProperty<string> LoseLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <inheritdoc />
        public void Dispose()
        {
            Title.Dispose();
            ProgressLine.Dispose();
            WinLabel.Dispose();
            LoseLabel.Dispose();
        }
    }
}
