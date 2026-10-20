using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /*
     * The combat-facing projection of a player's bag.
     *
     * Stage 05's LoadoutResolver produces a CombatLoadoutSnapshot and hands it to
     * the battle; the battle never reads the bag, the Board or the shop, and never
     * recomputes combos (docs/architecture/overview.md). These types are plain
     * domain data: properties are get/set because they are written exactly once by
     * the resolver while it builds the snapshot (`init` accessors are unavailable
     * on netstandard2.1, the surface Unity 6.3 compiles).
     *
     * Resolved numbers (cooldown, damage, ...) already include tier and synergy
     * bonuses, so the battle does not need a second lookup to be correct and two
     * resolvers cannot disagree at rule time.
     */

    /// <summary>The player's resolved stats for one battle.</summary>
    public sealed class PlayerStatBlock
    {
        public int MaxHp { get; set; }

        public int MaxMp { get; set; }

        public float MoveSpeed { get; set; }

        public float PickupRadius { get; set; }

        public float FocusPerSecond { get; set; }

        /// <summary>Multiplier applied to every weapon cooldown (<c>1</c> = unchanged).</summary>
        public float CooldownMultiplier { get; set; } = 1f;

        /// <summary>Multiplier applied to every weapon's damage (<c>1</c> = unchanged).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Movement-speed penalty in percent from total bag weight (<c>0</c> = none).</summary>
        public int SpeedPenaltyPercent { get; set; }
    }

    /// <summary>One arcane weapon, resolved for combat.</summary>
    public sealed class WeaponLoadout
    {
        /// <summary>
        /// Board instance id of the piece this entry was resolved from
        /// (<c>ItemInstance.InstanceId</c>), or <see cref="LoadoutCooldown.NoInstance"/> when the
        /// entry came from no piece.
        /// </summary>
        /// <remarks>
        /// The key a HUD binds a board cell to, and the only handle the battle has on "which piece
        /// is this": the loadout is a projection of the bag, so without it a frame's cooldown list
        /// could say "the second weapon is as good as ready" but not which piece to draw it on.
        /// Instance ids start at 1 and are never reused (Stage 09), so <c>0</c> is unambiguous.
        /// </remarks>
        public int InstanceId { get; set; } = LoadoutCooldown.NoInstance;

        /// <summary>Stable skill id, e.g. <c>skill.knife_thrust</c>.</summary>
        public string SkillId { get; set; } = string.Empty;

        public int Tier { get; set; }

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

    /// <summary>One summoned familiar, resolved for combat.</summary>
    public sealed class SummonLoadout
    {
        /// <summary>Board instance id of the piece this entry was resolved from, or <see cref="LoadoutCooldown.NoInstance"/>.</summary>
        public int InstanceId { get; set; } = LoadoutCooldown.NoInstance;

        public string SkillId { get; set; } = string.Empty;

        public int Tier { get; set; }

        public float CooldownSeconds { get; set; }

        public int MpCost { get; set; }

        public int Damage { get; set; }

        public float Range { get; set; }
    }

    /// <summary>One consumable (offering) the player carries into the battle.</summary>
    public sealed class ConsumableLoadout
    {
        public string ItemId { get; set; } = string.Empty;

        /// <summary>How many are available at the start of the floor.</summary>
        public int Count { get; set; }

        /// <summary>Skill id of the effect, or empty for a plain-use offering.</summary>
        public string SkillId { get; set; } = string.Empty;

        /// <summary>What one use does (Stage 11a), from <c>item.offering_effect</c>.</summary>
        public OfferingEffect Effect { get; set; }

        /// <summary>HP restored or bia granted by one use.</summary>
        public int EffectAmount { get; set; }

        /// <summary>
        /// How many times one carried piece can be used. <c>1</c> for a loadout built by a
        /// caller that predates the column, so the old "one use per piece" reading stays valid.
        /// </summary>
        public int UsesPerItem { get; set; } = 1;

        /// <summary>
        /// HP percentage (0–100) at or below which the battle spends a use automatically;
        /// <c>0</c> means the battle never uses it on its own.
        /// </summary>
        public int AutoUseHpPercent { get; set; }
    }

    /// <summary>
    /// Everything the battle needs to know about the player's build.
    /// </summary>
    public sealed class CombatLoadoutSnapshot
    {
        public PlayerStatBlock Stats { get; set; } = new PlayerStatBlock();

        public IReadOnlyList<WeaponLoadout> Weapons { get; set; } = new List<WeaponLoadout>();

        public IReadOnlyList<SummonLoadout> Summons { get; set; } = new List<SummonLoadout>();

        public IReadOnlyList<ConsumableLoadout> Consumables { get; set; } = new List<ConsumableLoadout>();
    }
}
