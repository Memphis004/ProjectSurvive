using System;
using System.Globalization;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Shared;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Hub
{
    /// <summary>
    /// The Hub screen's logic: the run's preparation step between encounters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two commands, both atomic (AGENTS rule 7): <b>depart</b> begins the next encounter and records
    /// the move into it; <b>finish</b> ends the run and records the move to the summary. Each asks the
    /// <see cref="SceneFlow"/> first and only applies the run's own command once the edge is legal, so a
    /// refusal changes neither the run nor the screen.
    /// </para>
    /// <para>
    /// The run is handed in rather than resolved, because the scene scope already has it: the presenter
    /// is a scene-scope singleton, and the Run scope outlives the scene.
    /// </para>
    /// </remarks>
    public sealed class HubPresenter
    {
        private static readonly string[] FormatArguments = new string[1];

        private readonly RunSession _run;
        private readonly SceneFlow _flow;
        private readonly LocalizationService _text;
        private readonly HubUiState _state;

        /// <summary>Creates the presenter for a run, the loop and the state it fills.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public HubPresenter(RunSession run, SceneFlow flow, LocalizationService text, HubUiState state)
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
            _state.Title.Value = _text.Get(HubKeys.Title);
            _state.Instructions.Value = _text.Get(HubKeys.Instructions);
            _state.EncountersLine.Value = Format(HubKeys.EncountersFormat, _run.EncounterIndex);
            _state.DepartLabel.Value = _text.Get(HubKeys.Depart);
            _state.FinishLabel.Value = _text.Get(HubKeys.Finish);
        }

        /// <summary>Enters the next encounter.</summary>
        /// <returns>False when a request is outstanding or the loop refused; nothing changes.</returns>
        public bool RequestDepart()
        {
            Result allowed = _flow.TryGo(SceneId.Encounter);
            if (allowed.IsFailure)
            {
                Refuse(allowed);
                return false;
            }

            Result begun = _run.BeginEncounter();
            if (begun.IsFailure)
            {
                // Roll the flow back: it must not claim a scene the run never entered.
                _flow.SyncTo(SceneId.Hub);
                Refuse(begun);
                return false;
            }

            LastError = string.Empty;
            RequestedScene = SceneId.Encounter;
            return true;
        }

        /// <summary>Ends the run and goes to the summary.</summary>
        /// <returns>False when a request is outstanding or the run refused; nothing changes.</returns>
        public bool RequestFinish()
        {
            Result ended = _run.EndRun();
            if (ended.IsFailure)
            {
                Refuse(ended);
                return false;
            }

            Result allowed = _flow.TryGo(SceneId.Result);
            if (allowed.IsFailure)
            {
                Refuse(allowed);
                return false;
            }

            LastError = string.Empty;
            RequestedScene = SceneId.Result;
            return true;
        }

        private void Refuse(Result refusal)
        {
            LastError = "the hub refused: " + refusal.Error.Code;
            Debug.LogError(LastError);
        }

        private string Format(string key, int value)
        {
            FormatArguments[0] = value.ToString(CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, _text.Get(key), FormatArguments);
        }
    }

    /// <summary>The localization keys the Hub screen reads, named once.</summary>
    public static class HubKeys
    {
        /// <summary>The screen's heading.</summary>
        public const string Title = "hub.title";

        /// <summary>One line of instructions.</summary>
        public const string Instructions = "hub.instructions";

        /// <summary>"Cleared {0} encounters".</summary>
        public const string EncountersFormat = "hub.encounters.format";

        /// <summary>Caption of the control that enters the next encounter.</summary>
        public const string Depart = "hub.depart";

        /// <summary>Caption of the control that ends the run.</summary>
        public const string Finish = "hub.finish";

        /// <summary>Every key the screen reads.</summary>
        public static readonly string[] All = { Title, Instructions, EncountersFormat, Depart, Finish };
    }
}
