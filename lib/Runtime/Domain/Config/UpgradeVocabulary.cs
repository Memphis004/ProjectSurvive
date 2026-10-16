namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// A stat an upgrade variant may adjust (Stage 09f).
    /// </summary>
    /// <remarks>
    /// Deliberately the same set the battle loadout carries
    /// (<c>WeaponLoadout</c> / <c>SummonLoadout</c> / <c>PlayerStatBlock</c> in
    /// <c>Domain/Combat/Battle/CombatLoadout.cs</c>): a variant may only change a number the
    /// battle already reads, so the upgrade system cannot smuggle a new combat mechanic in
    /// through <c>data/</c> (Stage 09f scope: "ตัวปรับค่าอ้างได้เฉพาะค่าสถานะที่
    /// CombatLoadoutSnapshot มีอยู่แล้ว").
    /// </remarks>
    public enum UpgradeStat
    {
        /// <summary>Weapon / summon damage.</summary>
        Damage = 1,

        /// <summary>Weapon / summon seconds between casts.</summary>
        Cooldown = 2,

        /// <summary>MP paid per cast.</summary>
        MpCost = 3,

        /// <summary>Projectile travel speed.</summary>
        ProjectileSpeed = 4,

        /// <summary>Cast range.</summary>
        Range = 5,

        /// <summary>How many enemies a piercing cast passes through.</summary>
        PierceCount = 6,

        /// <summary>How many projectiles one cast emits.</summary>
        ProjectileCount = 7,

        /// <summary>Player max HP.</summary>
        MaxHp = 8,

        /// <summary>Player max MP.</summary>
        MaxMp = 9,

        /// <summary>Player move speed.</summary>
        MoveSpeed = 10,

        /// <summary>Pickup radius.</summary>
        PickupRadius = 11,

        /// <summary>MP regenerated per second (สมาธิ).</summary>
        FocusPerSecond = 12,
    }

    /// <summary>How an <see cref="UpgradeStat"/> is adjusted.</summary>
    public enum UpgradeMethod
    {
        /// <summary><c>value += magnitude</c>.</summary>
        Add = 1,

        /// <summary><c>value += value * magnitude / 100</c> — a percentage of the resolved value.</summary>
        Percent = 2,

        /// <summary><c>value *= magnitude</c> — a plain factor.</summary>
        Multiply = 3,
    }

    /// <summary>
    /// The name vocabulary of the upgrade tables: how a category, a stat and a method are
    /// spelled in <c>data/tables/upgrade_variant*.csv</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The columns are plain strings rather than Luban enums on purpose. A typo in a
    /// <c>stat</c> cell has to come back as a finding that names the row it is in, and a
    /// Luban enum would fail the whole generate step with a parse error instead
    /// (<c>data/Defines/schema.xml</c>). The names are therefore checked against this one
    /// table - by the adapter when it maps a row, and by the data validator - so the
    /// vocabulary has exactly one definition and the battle stays the only implementer of
    /// it.
    /// </para>
    /// <para>
    /// The arrays are indexed by <c>value - 1</c>, and <c>UpgradeVocabularyTests</c> pins
    /// each array against the enum it names, so renumbering an enum without renaming the
    /// data cannot pass silently.
    /// </para>
    /// </remarks>
    public static class UpgradeVocabulary
    {
        /// <summary>Item categories, in enum order.</summary>
        public static readonly string[] ItemCategoryNames =
        {
            "Bag",
            "ArcaneWeapon",
            "Charm",
            "SpellTome",
            "Offering",
            "Familiar",
        };

        /// <summary>Adjustable stats, in enum order.</summary>
        public static readonly string[] StatNames =
        {
            "Damage",
            "Cooldown",
            "MpCost",
            "ProjectileSpeed",
            "Range",
            "PierceCount",
            "ProjectileCount",
            "MaxHp",
            "MaxMp",
            "MoveSpeed",
            "PickupRadius",
            "FocusPerSecond",
        };

        /// <summary>Adjustment methods, in enum order.</summary>
        public static readonly string[] MethodNames =
        {
            "Add",
            "Percent",
            "Multiply",
        };

        /// <summary>The category name <paramref name="category"/> is spelled with in data.</summary>
        public static string NameOf(ItemCategory category)
        {
            int index = (int)category - 1;
            return index >= 0 && index < ItemCategoryNames.Length ? ItemCategoryNames[index] : string.Empty;
        }

        /// <summary>Parses a category name. False when the name is not in the vocabulary.</summary>
        public static bool TryParseCategory(string? name, out ItemCategory category)
        {
            int index = IndexOf(ItemCategoryNames, name);
            category = index < 0 ? default : (ItemCategory)(index + 1);
            return index >= 0;
        }

        /// <summary>Parses a stat name. False when the name is not in the vocabulary.</summary>
        public static bool TryParseStat(string? name, out UpgradeStat stat)
        {
            int index = IndexOf(StatNames, name);
            stat = index < 0 ? default : (UpgradeStat)(index + 1);
            return index >= 0;
        }

        /// <summary>Parses a method name. False when the name is not in the vocabulary.</summary>
        public static bool TryParseMethod(string? name, out UpgradeMethod method)
        {
            int index = IndexOf(MethodNames, name);
            method = index < 0 ? default : (UpgradeMethod)(index + 1);
            return index >= 0;
        }

        /// <summary>
        /// True when a larger value of <paramref name="stat"/> is better for the player
        /// (Stage 09g: the chooser paints a helpful change green and a harmful one red).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The direction is a property of the <b>stat</b>, not of the row that changes it, so it
        /// lives here with the rest of the vocabulary rather than in the screen: a view that
        /// decided for itself that "-15" meant an improvement would be a balance rule in the
        /// View, which AGENTS rule 10 forbids.
        /// </para>
        /// <para>
        /// Ten of the twelve stats want to be larger. The two that do not are <b>costs</b>:
        /// <see cref="UpgradeStat.Cooldown"/> (seconds between casts) and
        /// <see cref="UpgradeStat.MpCost"/> (สมาธิ paid per cast) — a negative magnitude on those is
        /// the upgrade.
        /// </para>
        /// <para>
        /// An unknown value answers <c>false</c>: nothing about it is known to be good, and a
        /// screen showing "not an improvement" for a stat no data can name is the safe reading.
        /// <c>UpgradeVocabularyTests</c> walks the enum so a newly added stat cannot fall into
        /// this default unnoticed.
        /// </para>
        /// </remarks>
        public static bool HigherIsBetter(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Damage:
                case UpgradeStat.ProjectileSpeed:
                case UpgradeStat.Range:
                case UpgradeStat.PierceCount:
                case UpgradeStat.ProjectileCount:
                case UpgradeStat.MaxHp:
                case UpgradeStat.MaxMp:
                case UpgradeStat.MoveSpeed:
                case UpgradeStat.PickupRadius:
                case UpgradeStat.FocusPerSecond:
                    return true;

                case UpgradeStat.Cooldown:
                case UpgradeStat.MpCost:
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// True when a change of <paramref name="magnitude"/> to <paramref name="stat"/> helps the
        /// player. A zero change is not an improvement.
        /// </summary>
        public static bool IsImprovement(UpgradeStat stat, float magnitude) =>
            magnitude != 0f && (magnitude > 0f) == HigherIsBetter(stat);

        private static int IndexOf(string[] names, string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return -1;
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
