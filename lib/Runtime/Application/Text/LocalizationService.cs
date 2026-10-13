using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Application.Text
{
    /// <summary>
    /// Turns a localization key into player-facing text, in the language the
    /// player selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam the Presentation layer is allowed to use (AGENTS rule 10:
    /// a Presenter calls Application, never Domain and never a table directly).
    /// It is deliberately tiny — it holds the chosen <see cref="Language"/> and
    /// delegates the lookup to the <see cref="IGameConfig"/> port — because it is
    /// the only piece of the localization story that is a <i>decision</i> rather
    /// than a lookup.
    /// </para>
    /// <para>
    /// A missing key is a data bug, not a display state: <see cref="Get"/>
    /// therefore lets the underlying <see cref="KeyNotFoundException"/> out
    /// (AGENTS rule 5 — never paper over a missing string with the key itself),
    /// while <see cref="TryGet"/> exists for the places that must degrade
    /// gracefully. Nothing here formats numbers or composes sentences from
    /// fragments: those are later Stages' problems, and doing it early is how
    /// untranslatable UI gets built.
    /// </para>
    /// </remarks>
    public sealed class LocalizationService
    {
        private readonly IGameConfig _config;

        public LocalizationService(IGameConfig config, Language language = Language.Th)
        {
            _config = config;
            Language = language;
        }

        /// <summary>Language used by <see cref="Get"/> and <see cref="TryGet"/>.</summary>
        public Language Language { get; set; }

        /// <summary>
        /// The text for <paramref name="key"/> in <see cref="Language"/>.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The key does not exist in the table.</exception>
        public string Get(string key) => _config.GetText(key, Language);

        /// <summary>
        /// The text for <paramref name="key"/>, or false when the key is unknown.
        /// </summary>
        public bool TryGet(string key, out string text)
        {
            text = string.Empty;
            if (!_config.TryGetLocalization(key, out LocalizationEntry entry))
            {
                return false;
            }

            text = entry.For(Language);
            return true;
        }
    }
}
