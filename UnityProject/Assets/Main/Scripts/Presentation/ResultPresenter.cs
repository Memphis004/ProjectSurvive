using System;
using System.Globalization;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Run;

// Aliased rather than imported: this file lives next to screens that use `Result` (the shared typed
// outcome) in almost every line, and the one Application call this presenter makes returns exactly
// that type. One alias here is cheaper than a qualified name at every call site in the folder.
using CoreResult = ProjectSurvive.Core.Shared.Result;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Turns a finished run into the strings the Result screen shows, and records which way out the
    /// player asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen computes nothing (AGENTS rule 10). Every number it shows comes from
    /// <see cref="RunSession.Summary"/> — how the run ended, the floor it reached, the ghosts it
    /// defeated, the bia and HP it ended with — and every word comes from the localization table
    /// (<see cref="LocalizationService"/>), including the format strings the numbers are placed
    /// into (AGENTS rule 5).
    /// </para>
    /// <para>
    /// <b>It owns the one domain call the screen makes.</b> Leaving for the title is the run's own
    /// transition (Result → Title, <see cref="RunSession.ReturnToTitle"/>), and a presenter is
    /// where an Application call belongs. The presenter does <i>not</i> make that call when the
    /// button is pressed — it records the intent first so the scene change can be committed, and
    /// the director calls <see cref="CompleteTitleTransition"/> only after the loader accepted the
    /// move. A refused load therefore leaves the run exactly as it was, still showing its summary.
    /// </para>
    /// <para>
    /// <b>Restart is not a command on the run.</b> "Restart สร้าง Run ใหม่ ไม่คง state จากรอบก่อน"
    /// is a new run, not a reset of this one, so the presenter only records the intent; the Run
    /// scope is disposed and rebuilt by its owner (<c>RunScopeOwner</c>) once the camp load is
    /// committed, and the camp starts the new run the way it starts the first one.
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

        /// <summary>The summary of that run, as it stands.</summary>
        public RunSummary Summary => _run.Summary();

        /// <summary>True when the run has ended and the screen has something to show.</summary>
        public bool IsFinished => Summary.IsFinished;

        /// <summary>
        /// Where the player asked to go, or null while no request is outstanding. One request at a
        /// time: a second press before the transition completes is refused rather than queued.
        /// </summary>
        public SceneId? RequestedScene { get; private set; }

        /// <summary>
        /// Fills the state from the run. Idempotent, so a director may call it after a view already
        /// bound to the (empty) state without the screen ending up with two sets of values.
        /// </summary>
        public void Begin()
        {
            RunSummary summary = Summary;

            _state.Headline.Value = _text.Get(summary.IsVictory ? ResultKeys.Victory : ResultKeys.Defeat);
            _state.FloorLine.Value = Format(ResultKeys.FloorFormat, summary.FloorReached, summary.TotalFloors);
            _state.KillsLine.Value = Format(ResultKeys.KillsFormat, summary.GhostsDefeated);
            _state.BiaLine.Value = Format(ResultKeys.BiaFormat, summary.Bia);
            _state.HpLine.Value = Format(ResultKeys.HpFormat, summary.Hp, summary.MaxHp);
            _state.RestartLabel.Value = _text.Get(ResultKeys.Restart);
            _state.TitleLabel.Value = _text.Get(ResultKeys.BackToTitle);
        }

        /// <summary>
        /// Asks for a new run: the camp is loaded and the finished run is replaced there.
        /// </summary>
        /// <returns>False when a request is already outstanding; nothing changes.</returns>
        public bool RequestRestart() => Request(SceneId.Camp);

        /// <summary>
        /// Asks to leave for the title, where the run ends for good.
        /// </summary>
        /// <returns>False when a request is already outstanding; nothing changes.</returns>
        public bool RequestTitle() => Request(SceneId.Title);

        /// <summary>
        /// Performs the run's own Result → Title transition. Called by the scene once the move is
        /// committed, so a refused load cannot leave the run in a phase its screen is not showing.
        /// </summary>
        public CoreResult CompleteTitleTransition() => _run.ReturnToTitle();

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
        /// One localized line with its numbers in it. Invariant culture: the table's own formats are
        /// the same in every language, and a Thai line must not print a locale-dependent separator
        /// (the same rule the camp's own formatting follows).
        /// </summary>
        private string Format(string key, params object[] arguments) =>
            string.Format(CultureInfo.InvariantCulture, _text.Get(key), arguments);
    }

    /// <summary>
    /// The localization keys the Result screen reads. Named here rather than written at each call
    /// site so a renamed row is one edit, and so the table and the screen can be checked against
    /// each other.
    /// </summary>
    public static class ResultKeys
    {
        /// <summary>Headline when every floor was cleared.</summary>
        public const string Victory = "result.victory";

        /// <summary>Headline when the run ended on a floor it did not clear.</summary>
        public const string Defeat = "result.defeat";

        /// <summary>"Reached floor {0}/{1}".</summary>
        public const string FloorFormat = "result.floor.format";

        /// <summary>"Ghosts defeated {0}".</summary>
        public const string KillsFormat = "result.kills.format";

        /// <summary>"Bia left {0}".</summary>
        public const string BiaFormat = "result.bia.format";

        /// <summary>"HP left {0}/{1}".</summary>
        public const string HpFormat = "result.hp.format";

        /// <summary>Caption of the restart control.</summary>
        public const string Restart = "result.restart";

        /// <summary>Caption of the back-to-title control.</summary>
        public const string BackToTitle = "result.title";

        /// <summary>Every key the screen reads, in the order the table lists them.</summary>
        public static readonly string[] All =
        {
            Victory, Defeat, FloorFormat, KillsFormat, BiaFormat, HpFormat, Restart, BackToTitle,
        };
    }
}
