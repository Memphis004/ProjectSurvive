using System;
using ProjectSurvive.Core.Domain.Config;
using R3;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The UI-level state a View may render. Deliberately tiny.
    /// </summary>
    /// <remarks>
    /// <para>
    /// R3 carries <b>only</b> UI-level state here (AGENTS rule 10): a title
    /// string, a diagnostics line, the language, and a "ready" flag. It never
    /// carries per-entity positions or battle frames — those go through the
    /// battle contract's frame buffer, not through observables.
    /// </para>
    /// <para>
    /// Presenters write this; Views read it and assign text. Neither computes
    /// gameplay numbers (AGENTS rule 10) — text here always originated from the
    /// localization table.
    /// </para>
    /// </remarks>
    public sealed class UiState : IDisposable
    {
        /// <summary>Screen heading, e.g. the localized game title.</summary>
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// Secondary line. Used for build/diagnostics facts that are not
        /// player-facing content (version, config hash, smoke verdict), so it is
        /// the one string that is not required to come from localization.
        /// </summary>
        public ReactiveProperty<string> Diagnostics { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Language player-facing text is resolved in.</summary>
        public ReactiveProperty<Language> Language { get; } =
            new ReactiveProperty<Language>(ProjectSurvive.Core.Domain.Config.Language.Th);

        /// <summary>True once the root scope has finished booting.</summary>
        public ReactiveProperty<bool> Ready { get; } = new ReactiveProperty<bool>(false);

        public void Dispose()
        {
            Title.Dispose();
            Diagnostics.Dispose();
            Language.Dispose();
            Ready.Dispose();
        }
    }
}
