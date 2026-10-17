using System;
using System.Globalization;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Smoke;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Turns Application answers into the strings the Title screen shows, and owns the one thing the
    /// screen can do: start a run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only place Title's text is decided, and it calls Application
    /// (<see cref="LocalizationService"/>) rather than reading a generated table directly (AGENTS
    /// rule 10). The View that consumes <see cref="UiState"/> has no logic and no idea where the text
    /// came from.
    /// </para>
    /// <para>
    /// The diagnostics line is the one string not taken from localization: it carries build facts
    /// (version, config hash, smoke verdict) that exist for a developer reading a bug report.
    /// </para>
    /// <para>
    /// <b>The run is created here and nowhere else</b> (ADR 0022): pressing the start control asks
    /// <see cref="RunScopeOwner"/> for the run — which builds the Run scope — starts it, and records
    /// the move into the Hub. Arriving back at the title disposes that scope, so this presenter never
    /// holds a <see cref="RunSession"/>: it holds the owner and asks at the moment of the press.
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
        public TitlePresenter(LocalizationService text, UiState ui, IGameConfig config, RunScopeOwner runs)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        }

        /// <summary>Where the player asked to go, or null while no request is outstanding.</summary>
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
        /// Starts a run and records the move into the Hub.
        /// </summary>
        /// <returns>False when a request is already outstanding, or the run refused to start;
        /// nothing changes in either case.</returns>
        public bool RequestStart()
        {
            if (RequestedScene is not null)
            {
                return false;
            }

            RunSession run = _runs.RequireRun();

            if (!run.IsStarted)
            {
                var started = run.Start();
                if (started.IsFailure)
                {
                    StartError = "the run could not start: " + started.Error.Code;
                    Debug.LogError(StartError);
                    return false;
                }
            }

            StartError = string.Empty;
            RequestedScene = SceneId.Hub;
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
    /// The localization keys the Title screen reads, named once so a renamed row is one edit.
    /// </summary>
    public static class TitleKeys
    {
        /// <summary>The game's own name, the screen's heading.</summary>
        public const string Title = "ui.title";

        /// <summary>Caption of the control that starts a run.</summary>
        public const string Start = "title.start";

        /// <summary>Every key the screen reads.</summary>
        public static readonly string[] All = { Title, Start };
    }
}
