using System;
using System.Globalization;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Smoke;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Turns Application-layer answers into the strings the Title screen shows, and owns the one
    /// thing the screen can do: start a run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only place Title's text is decided, and it calls Application
    /// (<see cref="LocalizationService"/>) rather than reading a generated table
    /// directly (AGENTS rule 10). The View that consumes <see cref="UiState"/> has
    /// no logic and no idea where the text came from.
    /// </para>
    /// <para>
    /// The diagnostics line is deliberately the one string not taken from
    /// localization: it carries build facts (version, config hash, smoke verdict)
    /// that exist for a developer reading a bug report, not for a player. Player-
    /// facing strings still come from the table only (AGENTS rule 5).
    /// </para>
    /// <para>
    /// <b>The start control (Stage 10d).</b> Pressing it is the way a run begins —
    /// <see cref="RequestStart"/> calls the <b>same</b> Application command the camp calls when it is
    /// opened without a run (<see cref="RunSession.StartRun"/>), so there is one place a run is
    /// started and one answer to "what does a new run look like". The camp keeps its own guard for
    /// the handovers that do not come through here (a direct <c>?scene=Camp</c> load), and by then the
    /// run is already in <see cref="RunFlowState.Camp"/>, so its guard does nothing.
    /// </para>
    /// <para>
    /// <b>The run is asked for lazily, never held.</b> This presenter is a singleton of the App
    /// scope, which outlives every scene — and arriving at the title <i>disposes</i> the run scope
    /// (<c>SceneLoader</c>). A presenter that had captured a <see cref="RunSession"/> would therefore
    /// be holding the run a previous game ended: it asks <see cref="RunScopeOwner.RequireRun"/> at the
    /// moment the player presses, which is what makes "กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run
    /// ใหม่" true by construction rather than by remembering to rebuild something.
    /// </para>
    /// <para>
    /// The intent is recorded, not performed: <see cref="RequestedScene"/> is what the scene's own
    /// director reads before it asks the <c>SceneLoader</c>, exactly like the summary screen's two
    /// ways out, so a refused scene change cannot leave the title screen having already left.
    /// </para>
    /// </remarks>
    public sealed class TitlePresenter
    {
        private readonly LocalizationService _text;
        private readonly UiState _ui;
        private readonly IGameConfig _config;
        private readonly RunScopeOwner _runs;

        /// <summary>Creates the presenter for the shell and the run it may start.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TitlePresenter(
            LocalizationService text, UiState ui, IGameConfig config, RunScopeOwner runs)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        }

        /// <summary>
        /// Where the player asked to go, or null while no request is outstanding. One request at a
        /// time: a second press before the transition completes is refused rather than queued.
        /// </summary>
        public SceneId? RequestedScene { get; private set; }

        /// <summary>Why the last start refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>Writes the localized title, the start caption and the current build facts.</summary>
        public void Refresh()
        {
            _ui.Title.Value = _text.Get(TitleKeys.Title);
            _ui.StartLabel.Value = _text.Get(TitleKeys.Start);
            _ui.Diagnostics.Value = string.Format(
                CultureInfo.InvariantCulture,
                "build {0} | config {1}",
                Application.version,
                ShortHash(_config.ConfigHash));
        }

        /// <summary>
        /// Starts a run and records the move into the camp.
        /// </summary>
        /// <remarks>
        /// A run that is already under way — a title screen opened over a live run rather than reached
        /// through the loader — is <b>not</b> started again: the run's own state machine refuses
        /// (<see cref="RunError.WrongState"/>) and the player is taken to the camp, which is where a
        /// live run belongs. That refusal is also the second half of "no double start": the first
        /// press leaves the run out of <see cref="RunFlowState.Title"/>, so a second one cannot start
        /// it twice even if it were asked to.
        /// </remarks>
        /// <returns>False when a request is already outstanding, or the run refused to start;
        /// nothing changes in either case.</returns>
        public bool RequestStart()
        {
            if (RequestedScene is not null)
            {
                return false;
            }

            RunSession run = _runs.RequireRun();
            if (run.FlowState == RunFlowState.Title)
            {
                Result started = run.StartRun();
                if (started.IsFailure)
                {
                    StartError = "the run could not start: " + started.Error.Code;
                    Debug.LogError(StartError);
                    return false;
                }
            }

            StartError = string.Empty;
            RequestedScene = SceneId.Camp;
            return true;
        }

        /// <summary>Replaces the diagnostics line with the smoke verdict.</summary>
        public void ReportSmoke(SmokeReport report)
        {
            _ui.Diagnostics.Value = string.Format(
                CultureInfo.InvariantCulture,
                "smoke {0} | {1} probes | {2} failed | config {3}",
                report.Platform,
                report.Results.Count,
                report.FailedCount,
                ShortHash(report.ConfigHash));

            _ui.Ready.Value = true;
        }

        private static string ShortHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return string.Empty;
            }

            return hash.Length < 8 ? hash : hash.Substring(0, 8);
        }
    }

    /// <summary>
    /// The localization keys the Title screen reads. Named here rather than written at each call site
    /// so a renamed row is one edit, and so the table and the screen can be checked against each other
    /// (the same contract <c>ResultKeys</c> carries for the summary screen).
    /// </summary>
    public static class TitleKeys
    {
        /// <summary>The game's own name, the screen's heading.</summary>
        public const string Title = "ui.title";

        /// <summary>Caption of the control that starts a run.</summary>
        public const string Start = "title.start";

        /// <summary>Every key the screen reads, in the order the table lists them.</summary>
        public static readonly string[] All = { Title, Start };
    }
}
