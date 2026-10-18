using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// The read-only configuration the rule engine runs on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This port is the <b>only</b> way Domain / Application / Simulation see
    /// balance data. It is declared in Domain and implemented in Infrastructure
    /// by an adapter over the Luban-generated tables
    /// (<c>ProjectSurvive.Config.Tables</c>), so the generated row types never
    /// leak into the rule engine (AGENTS rule 3, ADR 0006).
    /// </para>
    /// <para>
    /// The template keeps the two tables every project needs: a singleton of
    /// constants and the localization table, plus a worked-example table
    /// (<see cref="Samples"/>) that a game deletes. A game adds its own tables and
    /// its own <c>TryGet*</c> lookups here, next to these.
    /// </para>
    /// <para>
    /// Every list is exposed in data-file order and every lookup is by the
    /// stable string id authored in <c>data/</c>. There is deliberately no
    /// "default" fallback that invents values: a successful load came from real
    /// <c>.bytes</c> tables, and a failed load throws instead of returning a stub.
    /// </para>
    /// </remarks>
    public interface IGameConfig
    {
        /// <summary>
        /// SHA-256 over the generated table bytes, upper-case hex. Recorded in
        /// build metadata so a bug report can be tied to exact balance data.
        /// </summary>
        string ConfigHash { get; }

        /// <summary>The singleton row of tuned numbers.</summary>
        ConstantConfig Constant { get; }

        /// <summary>Every localization row, in data-file order.</summary>
        IReadOnlyList<LocalizationEntry> Localization { get; }

        /// <summary>
        /// Every row of the template's worked-example table, in data-file order.
        /// Exists so the two reference mechanisms stay testable before a game has
        /// tables of its own; a game deletes it along with the CSV and the bean.
        /// </summary>
        IReadOnlyList<SampleEntry> Samples { get; }

        /// <summary>Looks one localization row up by key.</summary>
        bool TryGetLocalization(string key, out LocalizationEntry value);

        /// <summary>
        /// Player-facing text for <paramref name="key"/>. Throws
        /// <see cref="KeyNotFoundException"/> when the key is missing: a missing
        /// localization key is a data bug, never something to paper over with the
        /// key itself (AGENTS rule 5).
        /// </summary>
        string GetText(string key, Language language);
    }
}
