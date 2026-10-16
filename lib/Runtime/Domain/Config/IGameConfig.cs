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
    /// Every list is exposed in data-file order and every lookup is by the
    /// stable string id authored in <c>data/</c>. There is deliberately no
    /// "default" fallback that invents values: a successful load came from real
    /// `.bytes` tables, and a failed load throws instead of returning a stub.
    /// </para>
    /// </remarks>
    public interface IGameConfig
    {
        /// <summary>
        /// SHA-256 over the generated table bytes, upper-case hex. Recorded in
        /// build metadata so a bug report can be tied to exact balance data.
        /// </summary>
        string ConfigHash { get; }

        ConstantConfig Constant { get; }

        EconomyConfig Economy { get; }

        IReadOnlyList<BagConfig> Bags { get; }

        IReadOnlyList<ItemConfig> Items { get; }

        IReadOnlyList<SkillConfig> Skills { get; }

        IReadOnlyList<SynergyConfig> Synergies { get; }

        IReadOnlyList<MergeRecipeConfig> MergeRecipes { get; }

        /// <summary>
        /// Every upgrade choice, in data-file order (Stage 09f). The pool for one merge is
        /// selected from this list by the piece's category and tags; a piece no variant
        /// matches folds immediately, exactly as before the stage.
        /// </summary>
        IReadOnlyList<UpgradeVariantConfig> UpgradeVariants { get; }

        IReadOnlyList<EnemyConfig> Enemies { get; }

        IReadOnlyList<FloorConfig> Floors { get; }

        IReadOnlyList<WaveConfig> Waves { get; }

        IReadOnlyList<ShopPoolEntryConfig> ShopPool { get; }

        IReadOnlyList<LocalizationEntry> Localization { get; }

        bool TryGetBag(string id, out BagConfig value);

        bool TryGetItem(string id, out ItemConfig value);

        bool TryGetSkill(string id, out SkillConfig value);

        bool TryGetUpgradeVariant(string id, out UpgradeVariantConfig value);

        bool TryGetEnemy(string id, out EnemyConfig value);

        bool TryGetFloor(string id, out FloorConfig value);

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
