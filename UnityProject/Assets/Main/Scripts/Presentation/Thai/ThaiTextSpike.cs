using ProjectSurvive.Core.Application.Text;

namespace ProjectSurvive.Game.Presentation.Thai
{
    /// <summary>
    /// Supplies the Thai strings the spike renders, straight from the localization
    /// table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The spike must not carry its own copy of the text (AGENTS rule 5): if a
    /// literal lived here, the spike could pass while the real table was broken —
    /// exactly the failure the spike exists to catch. Keys are constants because
    /// they are identifiers, not player-visible content.
    /// </para>
    /// <para>
    /// <see cref="SpikeKeys"/> is public so the smoke report can name the keys it
    /// exercised without duplicating the list.
    /// </para>
    /// </remarks>
    public sealed class ThaiTextSpike
    {
        /// <summary>
        /// The strings under test: the title, the required Stage 03 word set, and a
        /// stress line built from tone marks, upper vowels and rare consonants.
        /// </summary>
        public static readonly string[] SpikeKeys =
        {
            "ui.title",
            "ui.spike.thai",
            "ui.spike.thai.marks",
        };

        private readonly LocalizationService _text;

        public ThaiTextSpike(LocalizationService text)
        {
            _text = text;
        }

        /// <summary>
        /// Resolves every spike key in <see cref="SpikeKeys"/> order. A missing key
        /// throws (AGENTS rule 5) — the spike failing loudly is the point.
        /// </summary>
        public string[] Resolve()
        {
            var texts = new string[SpikeKeys.Length];
            for (int i = 0; i < SpikeKeys.Length; i++)
            {
                texts[i] = _text.Get(SpikeKeys[i]);
            }

            return texts;
        }
    }
}
