using System;
using System.Globalization;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Shared;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Encounter
{
    /// <summary>
    /// The Encounter screen's logic: one unit of play, and the two ways it can end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A win counts one cleared encounter and returns to the Hub; a loss ends the run and goes to the
    /// summary. Both are the run's own commands (AGENTS rule 7): <see cref="RunSession.EndEncounter"/>
    /// is refused unless an encounter is actually running, and it is applied before the flow is asked
    /// for the next scene, so the flow never claims a scene the run did not reach.
    /// </para>
    /// <para>
    /// The template's encounter decides nothing about play. A game replaces this class with its arena
    /// rules and calls <see cref="RunSession.EndEncounter"/> at the same point.
    /// </para>
    /// </remarks>
    public sealed class EncounterPresenter
    {
        private static readonly string[] ProgressArguments = new string[2];

        private readonly RunSession _run;
        private readonly SceneFlow _flow;
        private readonly LocalizationService _text;
        private readonly EncounterUiState _state;

        /// <summary>Creates the presenter for a run, the loop and the state it fills.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public EncounterPresenter(
            RunSession run, SceneFlow flow, LocalizationService text, EncounterUiState state)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Where the player asked to go, or null while no request is outstanding.</summary>
        public SceneId? RequestedScene { get; private set; }

        /// <summary>Why the last command refused, or an empty string.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>Writes every string the screen shows. Idempotent.</summary>
        public void Refresh()
        {
            _state.Title.Value = _text.Get(EncounterKeys.Title);
            _state.ProgressLine.Value = Format(_run.EncounterIndex + 1, EncounterProgressTotal);
            _state.WinLabel.Value = _text.Get(EncounterKeys.Win);
            _state.LoseLabel.Value = _text.Get(EncounterKeys.Lose);
        }

        /// <summary>How many encounters the progress line says there are.</summary>
        /// <remarks>
        /// The template has no schedule of its own, so the line reads "1 of N" against a constant N.
        /// A game replaces this with its own count (the floors of a run, the waves of a floor).
        /// </remarks>
        public const int EncounterProgressTotal = 1;

        /// <summary>Clears the running encounter and returns to the Hub.</summary>
        /// <returns>False when a request is outstanding or the run refused; nothing changes.</returns>
        public bool RequestWin() => Request(EncounterOutcome.Won, SceneId.Hub);

        /// <summary>Loses the running encounter, which ends the run.</summary>
        /// <returns>False when a request is outstanding or the run refused; nothing changes.</returns>
        public bool RequestLose() => Request(EncounterOutcome.Lost, SceneId.Result);

        private bool Request(EncounterOutcome outcome, SceneId next)
        {
            if (RequestedScene is not null)
            {
                return false;
            }

            Result ended = _run.EndEncounter(outcome);
            if (ended.IsFailure)
            {
                LastError = "the encounter refused to end: " + ended.Error.Code;
                Debug.LogError(LastError);
                return false;
            }

            Result allowed = _flow.TryGo(next);
            if (allowed.IsFailure)
            {
                LastError = "the loop refused the move to " + next + ": " + allowed.Error.Code;
                Debug.LogError(LastError);
                return false;
            }

            LastError = string.Empty;
            RequestedScene = next;
            return true;
        }

        private string Format(int index, int total)
        {
            ProgressArguments[0] = index.ToString(CultureInfo.InvariantCulture);
            ProgressArguments[1] = total.ToString(CultureInfo.InvariantCulture);
            return string.Format(
                CultureInfo.InvariantCulture, _text.Get(EncounterKeys.ProgressFormat), ProgressArguments);
        }
    }

    /// <summary>The localization keys the Encounter screen reads, named once.</summary>
    public static class EncounterKeys
    {
        /// <summary>The screen's heading.</summary>
        public const string Title = "encounter.title";

        /// <summary>"Round {0} of {1}".</summary>
        public const string ProgressFormat = "encounter.progress.format";

        /// <summary>Caption of the control that clears the encounter.</summary>
        public const string Win = "encounter.win";

        /// <summary>Caption of the control that loses it.</summary>
        public const string Lose = "encounter.lose";

        /// <summary>Every key the screen reads.</summary>
        public static readonly string[] All = { Title, ProgressFormat, Win, Lose };
    }
}
