using ProjectSurvive.Core.Domain.Config;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The upgrade tables carry a <c>stat</c>, a <c>method</c> and a <c>category</c> as plain
    /// strings, and <see cref="UpgradeVocabulary"/> is the single place that says which names
    /// exist (Stage 09f).
    /// </summary>
    /// <remarks>
    /// These are the same guarantee <c>ConfigEnumParityTests</c> gives the cast-based enums: the
    /// name at position <c>value - 1</c> has to be the enum member with that value, or a
    /// renumbered enum would silently start reading one stat as another. Unlike the cast enums
    /// there is no generated counterpart to compare with, because the columns are strings
    /// precisely so a typo becomes a row-level finding instead of a Luban parse error.
    /// </remarks>
    public sealed class UpgradeVocabularyTests
    {
        /// <summary>
        /// Every stat the enum declares, spelled once so the two direction tests below walk the same
        /// twelve and a new member cannot be added to one walk and forgotten in the other.
        /// </summary>
        private static readonly UpgradeStat[] AllStats =
        {
            UpgradeStat.Damage, UpgradeStat.Cooldown, UpgradeStat.MpCost,
            UpgradeStat.ProjectileSpeed, UpgradeStat.Range, UpgradeStat.PierceCount,
            UpgradeStat.ProjectileCount, UpgradeStat.MaxHp, UpgradeStat.MaxMp,
            UpgradeStat.MoveSpeed, UpgradeStat.PickupRadius, UpgradeStat.FocusPerSecond,
        };

        /// <summary>The two stats a smaller value is the upgrade for.</summary>
        private static readonly UpgradeStat[] Costs = { UpgradeStat.Cooldown, UpgradeStat.MpCost };

        [Fact]
        public void The_stat_names_are_the_values_of_the_stat_enum_in_order()
        {
            foreach (UpgradeStat stat in new[]
                     {
                         UpgradeStat.Damage, UpgradeStat.Cooldown, UpgradeStat.MpCost,
                         UpgradeStat.ProjectileSpeed, UpgradeStat.Range, UpgradeStat.PierceCount,
                         UpgradeStat.ProjectileCount, UpgradeStat.MaxHp, UpgradeStat.MaxMp,
                         UpgradeStat.MoveSpeed, UpgradeStat.PickupRadius, UpgradeStat.FocusPerSecond,
                     })
            {
                Assert.Equal(stat.ToString(), UpgradeVocabulary.StatNames[(int)stat - 1]);
                Assert.True(UpgradeVocabulary.TryParseStat(stat.ToString(), out UpgradeStat parsed));
                Assert.Equal(stat, parsed);
            }

            Assert.Equal(12, UpgradeVocabulary.StatNames.Length);
        }

        [Fact]
        public void The_method_names_are_the_values_of_the_method_enum_in_order()
        {
            foreach (UpgradeMethod method in new[]
                     {
                         UpgradeMethod.Add, UpgradeMethod.Percent, UpgradeMethod.Multiply,
                     })
            {
                Assert.Equal(method.ToString(), UpgradeVocabulary.MethodNames[(int)method - 1]);
                Assert.True(UpgradeVocabulary.TryParseMethod(method.ToString(), out UpgradeMethod parsed));
                Assert.Equal(method, parsed);
            }

            Assert.Equal(3, UpgradeVocabulary.MethodNames.Length);
        }

        [Fact]
        public void The_item_category_names_are_the_values_of_the_category_enum_in_order()
        {
            foreach (ItemCategory category in new[]
                     {
                         ItemCategory.Bag, ItemCategory.ArcaneWeapon, ItemCategory.Charm,
                         ItemCategory.SpellTome, ItemCategory.Offering, ItemCategory.Familiar,
                     })
            {
                Assert.Equal(category.ToString(), UpgradeVocabulary.ItemCategoryNames[(int)category - 1]);
                Assert.Equal(category.ToString(), UpgradeVocabulary.NameOf(category));
                Assert.True(UpgradeVocabulary.TryParseCategory(category.ToString(), out ItemCategory parsed));
                Assert.Equal(category, parsed);
            }
        }

        [Fact]
        public void An_unknown_name_is_rejected_rather_than_defaulted()
        {
            Assert.False(UpgradeVocabulary.TryParseStat("Damage_in_the_shade", out UpgradeStat stat));
            Assert.Equal(default, stat);
            Assert.False(UpgradeVocabulary.TryParseStat(string.Empty, out _));
            Assert.False(UpgradeVocabulary.TryParseStat(null, out _));

            Assert.False(UpgradeVocabulary.TryParseMethod("Divide", out UpgradeMethod method));
            Assert.Equal(default, method);

            Assert.False(UpgradeVocabulary.TryParseCategory("Weapon", out ItemCategory category));
            Assert.Equal(default, category);
            Assert.False(UpgradeVocabulary.TryParseCategory("arcaneweapon", out _));
        }

        /// <summary>
        /// Which direction of each stat helps the player: ten want a larger number and the two that
        /// are costs want a smaller one. The Stage 09g chooser paints a card line from this, so a
        /// stat whose direction is wrong would show a downgrade in green.
        /// </summary>
        [Fact]
        public void Every_stat_says_which_direction_helps_the_player()
        {
            foreach (UpgradeStat stat in AllStats)
            {
                bool isCost = false;

                foreach (UpgradeStat cost in Costs)
                {
                    if (cost == stat)
                    {
                        isCost = true;
                        break;
                    }
                }

                Assert.Equal(!isCost, UpgradeVocabulary.HigherIsBetter(stat));
            }

            // The walk is only meaningful while it covers the vocabulary: every name the tables may
            // spell has to be one of the twelve above.
            Assert.Equal(UpgradeVocabulary.StatNames.Length, AllStats.Length);

            foreach (string name in UpgradeVocabulary.StatNames)
            {
                bool walked = false;

                foreach (UpgradeStat stat in AllStats)
                {
                    if (stat.ToString() == name)
                    {
                        walked = true;
                        break;
                    }
                }

                Assert.True(walked, name + " is a stat name no direction test walks");
            }

            // An unknown value is not claimed to be good.
            Assert.False(UpgradeVocabulary.HigherIsBetter((UpgradeStat)99));
        }

        /// <summary>A change is an improvement only when it points the way the stat wants.</summary>
        [Fact]
        public void A_change_is_an_improvement_only_when_it_points_the_way_the_stat_wants()
        {
            Assert.True(UpgradeVocabulary.IsImprovement(UpgradeStat.Cooldown, -20f));
            Assert.False(UpgradeVocabulary.IsImprovement(UpgradeStat.Cooldown, 20f));
            Assert.True(UpgradeVocabulary.IsImprovement(UpgradeStat.MpCost, -2f));
            Assert.False(UpgradeVocabulary.IsImprovement(UpgradeStat.MpCost, 2f));
            Assert.True(UpgradeVocabulary.IsImprovement(UpgradeStat.Damage, 25f));
            Assert.False(UpgradeVocabulary.IsImprovement(UpgradeStat.Damage, -25f));
            Assert.True(UpgradeVocabulary.IsImprovement(UpgradeStat.ProjectileCount, 1f));

            // Both signs on every stat: the two answers cannot disagree, because the colour a card
            // shows and the arrow this test expects are the same rule, and a zero change is never
            // an improvement.
            foreach (UpgradeStat stat in AllStats)
            {
                bool better = UpgradeVocabulary.HigherIsBetter(stat);

                Assert.Equal(better, UpgradeVocabulary.IsImprovement(stat, 5f));
                Assert.Equal(!better, UpgradeVocabulary.IsImprovement(stat, -5f));
                Assert.False(UpgradeVocabulary.IsImprovement(stat, 0f), "a zero change is not an improvement");
            }

            // No third cost can be added to the vocabulary without this walk being told.
            Assert.Equal(2, Costs.Length);
            Assert.False(UpgradeVocabulary.IsImprovement((UpgradeStat)99, 5f));
        }
    }
}
