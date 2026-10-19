using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Config
{
    /*
     * Plain domain projections of the data tables.
     *
     * These are what Domain / Application / Simulation consume. They are NOT the
     * Luban-generated rows (ProjectSurvive.Config.*) - the Infrastructure adapter
     * maps one to the other, so a data-schema change touches the adapter and the
     * validator only, never gameplay rules (AGENTS rule 3, ADR 0006).
     *
     * Properties are plain get/set: they are written exactly once by the adapter
     * while it builds the snapshot, and `init` accessors are not available on
     * netstandard2.1 (the target Unity 6.3 compiles against).
     */

    /// <summary>One row of the <c>localization</c> table.</summary>
    public sealed class LocalizationEntry
    {
        public string Key { get; set; } = string.Empty;

        public string Th { get; set; } = string.Empty;

        public string En { get; set; } = string.Empty;

        public string For(Language language) => language == Language.En ? En : Th;
    }

    /// <summary>A bag piece: the thing that opens ActiveSlots on the Board.</summary>
    public sealed class BagConfig
    {
        public string Id { get; set; } = string.Empty;

        public string NameKey { get; set; } = string.Empty;

        public int Tier { get; set; }

        public int Price { get; set; }

        public float Weight { get; set; }

        public Footprint Footprint { get; set; } = Footprint.Empty;
    }

    /// <summary>Cast behaviour shared by arcane weapons and familiars.</summary>
    public sealed class SkillConfig
    {
        public string Id { get; set; } = string.Empty;

        public string NameKey { get; set; } = string.Empty;

        public SkillBehavior Behavior { get; set; }

        public Element Element { get; set; }

        public float CooldownSeconds { get; set; }

        public int MpCost { get; set; }

        public int Damage { get; set; }

        public float ProjectileSpeed { get; set; }

        public float Range { get; set; }

        public int PierceCount { get; set; }

        public int ProjectileCount { get; set; }
    }

    /// <summary>A non-bag item. <see cref="SkillId"/> is empty for passive pieces.</summary>
    public sealed class ItemConfig
    {
        public string Id { get; set; } = string.Empty;

        public ItemCategory Category { get; set; }

        public string NameKey { get; set; } = string.Empty;

        /// <summary>May be empty.</summary>
        public string DescriptionKey { get; set; } = string.Empty;

        public int Tier { get; set; }

        public Element Element { get; set; }

        public int Price { get; set; }

        public float Weight { get; set; }

        /// <summary>May be empty (charms, tomes, offerings).</summary>
        public string SkillId { get; set; } = string.Empty;

        /// <summary>
        /// Free-form tags a <see cref="SynergyConfig"/> matches on. The vocabulary
        /// the shipped data uses is listed in llmwiki/systems/data-schema.md; the
        /// data validator rejects a synergy that names a tag no item carries.
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// The board cells this item occupies, in the same picture syntax as
        /// <see cref="BagConfig.Footprint"/>. Never empty in shipped data: an item
        /// with no shape could not be placed on a bag at all.
        /// </summary>
        public Footprint Footprint { get; set; } = Footprint.Empty;
    }

    /// <summary>A combo rule, matched by item tag.</summary>
    public sealed class SynergyConfig
    {
        public string Id { get; set; } = string.Empty;

        public string NameKey { get; set; } = string.Empty;

        public SynergyKind Kind { get; set; }

        /// <summary>May be empty for <see cref="SynergyKind.BagArea"/>.</summary>
        public string TagA { get; set; } = string.Empty;

        /// <summary>May be empty for <see cref="SynergyKind.BagArea"/>.</summary>
        public string TagB { get; set; } = string.Empty;

        public SynergyStat Stat { get; set; }

        public float ValuePercent { get; set; }

        public int MaxStacks { get; set; }
    }

    /// <summary>
    /// One merge step: the piece identified by <see cref="ItemId"/> or
    /// <see cref="BagId"/> at <see cref="FromTier"/> folds into
    /// <see cref="ToTier"/>. Exactly one of the two ids is set.
    /// </summary>
    public sealed class MergeRecipeConfig
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>May be empty (bag recipe).</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>May be empty (item recipe).</summary>
        public string BagId { get; set; } = string.Empty;

        public int FromTier { get; set; }

        public int ToTier { get; set; }

        /// <summary>The id of the piece this recipe merges, item or bag.</summary>
        public string TargetId => string.IsNullOrEmpty(ItemId) ? BagId : ItemId;
    }

    /// <summary>
    /// One upgrade choice: the numbers a merge of two equal pieces may fold into the
    /// survivor when the player picks this variant (Stage 09f).
    /// </summary>
    /// <remarks>
    /// The variant matches a piece by <b>category or tag</b>, never by item id, so one
    /// row covers a whole family — exactly one of <see cref="Category"/> /
    /// <see cref="Tag"/> is set (the same shape <see cref="MergeRecipeConfig"/> uses for
    /// its two optional ids). <see cref="Modifiers"/> is attached by the config adapter
    /// from the <c>upgrade_variant_modifier</c> rows; it is never empty in shipped data,
    /// which the data validator enforces.
    /// </remarks>
    public sealed class UpgradeVariantConfig
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>An <see cref="ItemCategory"/> name, or empty for a tag-scoped variant.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>An item tag, or empty for a category-scoped variant.</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>The tier step this choice belongs to (<c>1</c> = the 1 → 2 merge).</summary>
        public int FromTier { get; set; }

        /// <summary>The tier the survivor reaches; always <see cref="FromTier"/> + 1.</summary>
        public int ToTier { get; set; }

        /// <summary>The localization key of the choice's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Draw weight inside its pool; a higher weight is offered more often.</summary>
        public int Weight { get; set; }

        /// <summary>What this choice changes. Never empty in shipped data.</summary>
        public IReadOnlyList<UpgradeModifierConfig> Modifiers { get; set; } =
            new List<UpgradeModifierConfig>();
    }

    /// <summary>One stat change of an <see cref="UpgradeVariantConfig"/>.</summary>
    public sealed class UpgradeModifierConfig
    {
        /// <summary>Row id, so a validator finding can name the modifier it is about.</summary>
        public string Id { get; set; } = string.Empty;

        public UpgradeStat Stat { get; set; }

        public UpgradeMethod Method { get; set; }

        /// <summary>How much, in the units <see cref="Method"/> implies. May be negative.</summary>
        public float Magnitude { get; set; }
    }

    public sealed class EnemyConfig
    {
        public string Id { get; set; } = string.Empty;

        public string NameKey { get; set; } = string.Empty;

        public EnemyRole Role { get; set; }

        public int MaxHp { get; set; }

        public float MoveSpeed { get; set; }

        public int ContactDamage { get; set; }

        public float AttackCooldownSeconds { get; set; }

        public MovePattern MovePattern { get; set; }

        public bool IsElite { get; set; }

        public bool IsBoss { get; set; }

        /// <summary>Boss second-phase HP threshold in percent; 0 when not a boss.</summary>
        public float BossPhase2HpPercent { get; set; }

        /// <summary>Bia one kill of this enemy pays (Stage 07b). Never negative.</summary>
        public int DropValue { get; set; }
    }

    public sealed class FloorConfig
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>1-based run order; shop-pool bands and wave checks use it.</summary>
        public int Index { get; set; }

        public string NameKey { get; set; } = string.Empty;

        public float DurationSeconds { get; set; }

        public bool IsBoss { get; set; }

        /// <summary>May be empty on a normal floor.</summary>
        public string BossEnemyId { get; set; } = string.Empty;
    }

    public sealed class WaveConfig
    {
        public string Id { get; set; } = string.Empty;

        public string FloorId { get; set; } = string.Empty;

        public string EnemyId { get; set; } = string.Empty;

        public float StartTimeSeconds { get; set; }

        public float EndTimeSeconds { get; set; }

        public int SpawnCount { get; set; }

        public float SpawnIntervalSeconds { get; set; }

        public bool IsElite { get; set; }
    }

    /// <summary>
    /// One weighted shop entry. Exactly one of <see cref="ItemId"/> /
    /// <see cref="BagId"/> is set.
    /// </summary>
    public sealed class ShopPoolEntryConfig
    {
        public string Id { get; set; } = string.Empty;

        public int FloorMin { get; set; }

        public int FloorMax { get; set; }

        /// <summary>May be empty (bag entry).</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>May be empty (item entry).</summary>
        public string BagId { get; set; } = string.Empty;

        public int Weight { get; set; }

        /// <summary>The id of the offered piece, item or bag.</summary>
        public string OfferedId => string.IsNullOrEmpty(ItemId) ? BagId : ItemId;
    }

    /// <summary>Singleton row of the <c>economy</c> table.</summary>
    public sealed class EconomyConfig
    {
        public int StartBia { get; set; }

        public int ShopSlotCount { get; set; }

        public int RefreshBaseCost { get; set; }

        public int RefreshCostIncrement { get; set; }

        public int SellBackPercent { get; set; }

        public int HealPercentOnClear { get; set; }

        public int ChestSlots { get; set; }

        /// <summary>Flat bia a cleared floor pays, on a win only (Stage 07b).</summary>
        public int ClearRewardBia { get; set; }
    }

    /// <summary>Singleton row of the <c>constant</c> table.</summary>
    public sealed class ConstantConfig
    {
        public int BoardWidth { get; set; }

        public int BoardHeight { get; set; }

        public float MinCooldownSeconds { get; set; }

        public int MaxSpeedPenaltyPercent { get; set; }

        /// <summary>
        /// Percent of move speed lost per unit of carried weight, before
        /// <see cref="MaxSpeedPenaltyPercent"/> clamps it (Stage 05).
        /// </summary>
        public int WeightSpeedPercentPerUnit { get; set; }

        public float PickupRadius { get; set; }

        public int MaxHp { get; set; }

        public int MaxMp { get; set; }

        public float FocusPerSecond { get; set; }

        public float MoveSpeed { get; set; }

        public int MaxTier { get; set; }

        /// <summary>
        /// How many upgrade choices a merge offers ("เลือก 1 จาก 3", Stage 09f). Read by
        /// the offer builder as the size cap and by the data validator as the minimum a
        /// non-empty pool must have: a pool of one or two could not fill the chooser.
        /// </summary>
        public int UpgradeChoiceCount { get; set; }
    }
}
