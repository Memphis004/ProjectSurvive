using System.Globalization;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation.Smoke;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Turns Application-layer answers into the strings the Title screen shows.
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
    /// </remarks>
    public sealed class TitlePresenter
    {
        private readonly LocalizationService _text;
        private readonly UiState _ui;
        private readonly IGameConfig _config;

        public TitlePresenter(LocalizationService text, UiState ui, IGameConfig config)
        {
            _text = text;
            _ui = ui;
            _config = config;
        }

        /// <summary>Writes the localized title and the current build facts.</summary>
        public void Refresh()
        {
            _ui.Title.Value = _text.Get("ui.title");
            _ui.Diagnostics.Value = string.Format(
                CultureInfo.InvariantCulture,
                "build {0} | config {1}",
                Application.version,
                ShortHash(_config.ConfigHash));
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
}
