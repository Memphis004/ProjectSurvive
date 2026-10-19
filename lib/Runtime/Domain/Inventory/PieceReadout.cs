using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>The three numbers a casting piece's tooltip line shows.</summary>
    /// <remarks>
    /// A plain value, because it is what the tooltip compares: the same three fields for the
    /// table's base row and for the number the loadout resolved, so a presenter can format
    /// "resolved (from base)" without deciding what a stat is.
    /// </remarks>
    public readonly struct PieceStatValues
    {
        /// <summary>Captures one set of numbers.</summary>
        public PieceStatValues(int damage, float cooldownSeconds, int mpCost)
        {
            Damage = damage;
            CooldownSeconds = cooldownSeconds;
            MpCost = mpCost;
        }

        /// <summary>Damage per hit after every modifier.</summary>
        public int Damage { get; }

        /// <summary>Seconds between casts, already floored at the constant.</summary>
        public float CooldownSeconds { get; }

        /// <summary>Focus (สมาธิ) one cast costs.</summary>
        public int MpCost { get; }
    }

    /// <summary>
    /// One completed merge step of a piece: which variant was kept and what it changed.
    /// </summary>
    /// <remarks>
    /// The modifiers are the variant's own rows, not a pre-rendered sentence: the presenter
    /// formats them through the same helpers the upgrade-choice card uses, so one item's
    /// step reads the same in the tooltip and in the chooser.
    /// </remarks>
    public readonly struct PieceUpgradeStep
    {
        /// <summary>Captures one step.</summary>
        public PieceUpgradeStep(
            int step, int fromTier, int toTier, string variantId, string nameKey,
            IReadOnlyList<UpgradeModifierConfig> modifiers)
        {
            Step = step;
            FromTier = fromTier;
            ToTier = toTier;
            VariantId = variantId ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            Modifiers = modifiers ?? Array.Empty<UpgradeModifierConfig>();
        }

        /// <summary>1-based merge step (a tier-3 piece has two).</summary>
        public int Step { get; }

        /// <summary>Tier before the step.</summary>
        public int FromTier { get; }

        /// <summary>Tier after the step.</summary>
        public int ToTier { get; }

        /// <summary>The variant the player kept, or empty for a step with no chooser.</summary>
        public string VariantId { get; }

        /// <summary>Localization key of the variant's name, or empty when there is no variant.</summary>
        public string NameKey { get; }

        /// <summary>Every modifier the variant applies, in table order.</summary>
        public IReadOnlyList<UpgradeModifierConfig> Modifiers { get; }

        /// <summary>True when this step kept a named variant ("บรรทัดรุ่นย่อย").</summary>
        public bool HasVariant => !string.IsNullOrEmpty(VariantId);
    }

    /// <summary>
    /// One piece explained in the numbers the battle will actually use (Stage 09h).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camp tooltip used to read the <c>skill</c> row directly, so a piece at 3/3 showed the
    /// same damage and cooldown as at 1/1 and a chosen upgrade never appeared. This type is the
    /// tooltip's only source: it is produced by <see cref="LoadoutResolver"/>, which means the
    /// resolved numbers are the snapshot's numbers rather than a second calculation — for a piece
    /// on the board <see cref="Resolved"/> is copied out of the very
    /// <see cref="ProjectSurvive.Core.Domain.Combat.Battle.WeaponLoadout"/> the battle receives.
    /// </para>
    /// <para>
    /// A presenter formats these fields; it never recomputes them (AGENTS rules 5 and 10).
    /// </para>
    /// </remarks>
    public sealed class PieceReadout
    {
        /// <summary>The piece this explains.</summary>
        public PieceRef Piece { get; set; }

        /// <summary>Permanent data id (<c>item.kali_flame_yant</c> or <c>bag.raw_cotton_bag</c>).</summary>
        public string DefId { get; set; } = string.Empty;

        /// <summary>Skill id, or empty for a piece that does not cast.</summary>
        public string SkillId { get; set; } = string.Empty;

        /// <summary>True for a bag (bags have no stats and no upgrade steps).</summary>
        public bool IsBag { get; set; }

        /// <summary>1..<c>constant.max_tier</c>.</summary>
        public int Tier { get; set; }

        /// <summary>True when the piece has a skill row, i.e. has damage/cooldown/focus to show.</summary>
        public bool Casts { get; set; }

        /// <summary>
        /// True when the piece is on the board and therefore part of the loadout the next floor
        /// receives. A chest piece resolves on its own numbers with no combo.
        /// </summary>
        public bool InLoadout { get; set; }

        /// <summary>The <c>skill</c> row's own numbers, before any tier or combo.</summary>
        public PieceStatValues Base { get; set; }

        /// <summary>
        /// The numbers the battle uses: tier upgrades and active combos applied, cooldown floored at
        /// <c>constant.min_cooldown_seconds</c>.
        /// </summary>
        public PieceStatValues Resolved { get; set; }

        /// <summary><c>constant.min_cooldown_seconds</c> — the floor a combo cannot cross.</summary>
        public float MinCooldownSeconds { get; set; }

        /// <summary>
        /// True when the floor, not the piece's own modifiers, decided the resolved cooldown. The
        /// tooltip says so instead of letting a reduction look as if it did nothing.
        /// </summary>
        public bool CooldownAtMinimum { get; set; }

        /// <summary>One entry per completed merge step, in step order (empty at tier 1).</summary>
        public IReadOnlyList<PieceUpgradeStep> Steps { get; set; } = Array.Empty<PieceUpgradeStep>();

        /// <summary>The ids of the synergy rules acting on this piece, in evaluation order.</summary>
        public IReadOnlyList<string> ComboRuleIds { get; set; } = Array.Empty<string>();

        /// <summary>How many rule applications landed on the piece (0 when none).</summary>
        public int ActiveSynergyCount { get; set; }

        /// <summary>The damage multiplier those rules add up to.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>The cooldown multiplier those rules apply.</summary>
        public float CooldownMultiplier { get; set; } = 1f;
    }
}
