using System;
using System.Globalization;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Turns a finished run into the strings the Result screen shows, and records which way out the
    /// player asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen computes nothing (AGENTS rule 10). Every number it shows comes from the
    /// <see cref="RunSession"/> — how the run ended, how many encounters it cleared, the seed — and
    /// every word comes from the localization table, including the format strings the numbers are
    /// placed into (AGENTS rule 5).
    /// </para>
    /// <para>
    /// The presenter only <i>records</i> a way out; the scene change belongs to
    /// <see cref="ProjectSurvive.Game.ResultScene.ResultSceneDirector"/>, so a refused load cannot
    /// leave the screen already gone. Restart does not touch this run either: the director disposes
    /// the run scope and the Hub starts a new one, which is what "a new run, not a reset" means.
    /// </para>
    /// </remarks>
    public sealed class ResultPresenter
    {
        private readonly RunSession _run;
        private readonly LocalizationService _text;
        private readonly ResultUiState _state;

        /// <summary>Creates the presenter for a run and the state it fills.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ResultPresenter(RunSession run, LocalizationService text, ResultUiState state)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>The run being summarised.</summary>
        public RunSession Run => _run;

        /// <summary>True when the run has ended and the screen has something to show.</summary>
        public bool IsFinished => _run.IsFinished;

        /// <summary>Where the player asked to go, or null while no request is outstanding.</summary>
        public SceneId? RequestedScene { get; private set; }

        /// <summary>Fills the state from the run. Idempotent.</summary>
        public void Begin()
        {
            _state.Headline.Value = _text.Get(
                _run.Outcome == RunOutcome.Completed ? ResultKeys.Victory : ResultKeys.Defeat);
            _state.SummaryLine.Value = Format(ResultKeys.SummaryFormat, _run.EncounterIndex);
            _state.SeedLine.Value = Format(ResultKeys.SeedFormat, _run.Seed.ToString(CultureInfo.InvariantCulture));
            _state.RestartLabel.Value = _text.Get(ResultKeys.Restart);
            _state.TitleLabel.Value = _text.Get(ResultKeys.BackToTitle);
        }

        /// <summary>Asks for a new run: the Hub is loaded and a new run starts there.</summary>
        /// <returns>False when a request is already outstanding; nothing changes.</returns>
        public bool RequestRestart() => Request(SceneId.Hub);

        /// <summary>Asks to leave for the title, where the run ends for good.</summary>
        /// <returns>False when a request is already outstanding; nothing changes.</returns>
        public bool RequestTitle() => Request(SceneId.Title);

        private bool Request(SceneId scene)
        {
            if (RequestedScene is not null)
            {
                return false;
            }

            RequestedScene = scene;
            return true;
        }

        /// <summary>
        /// One localized line with its numbers in it. Invariant culture: a Thai line must not print a
        /// locale-dependent separator.
        /// </summary>
        private string Format(string key, params object[] arguments) =>
            string.Format(CultureInfo.InvariantCulture, _text.Get(key), arguments);
    }

    /// <summary>
    /// The localization keys the Result screen reads, named once so a renamed row is one edit.
    /// </summary>
    public static class ResultKeys
    {
        /// <summary>Headline when the run ended deliberately.</summary>
        public const string Victory = "result.victory";

        /// <summary>Headline when an encounter ended the run.</summary>
        public const string Defeat = "result.defeat";

        /// <summary>"Cleared {0} encounters".</summary>
        public const string SummaryFormat = "result.summary.format";

        /// <summary>"seed {0}".</summary>
        public const string SeedFormat = "result.seed.format";

        /// <summary>Caption of the restart control.</summary>
        public const string Restart = "result.restart";

        /// <summary>Caption of the back-to-title control.</summary>
        public const string BackToTitle = "result.title";

        /// <summary>Every key the screen reads.</summary>
        public static readonly string[] All =
        {
            Victory, Defeat, SummaryFormat, SeedFormat, Restart, BackToTitle,
        };
    }
}
