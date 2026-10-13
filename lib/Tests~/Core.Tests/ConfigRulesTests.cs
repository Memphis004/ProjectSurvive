using System;
using System.Collections.Generic;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Config;
using ProjectSurvive.Data.Validator;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// One test per rule in the Stage 02 check list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each test starts from the REAL tables and breaks exactly one thing, then
    /// asserts both halves of the contract: the rule fires, and it names the row
    /// it is about. Injecting one violation into a snapshot that is otherwise
    /// valid is what keeps a rule from passing because some unrelated row was
    /// wrong all along.
    /// </para>
    /// <para>
    /// The same <see cref="ConfigValidator"/> runs in the pipeline through
    /// tools/validate-data.ps1, so these tests are the fast, exact half of the
    /// evidence and the fixture check in tools/check-fixtures.ps1 is the
    /// end-to-end half (data/ -&gt; Luban -&gt; .bytes -&gt; findings).
    /// </para>
    /// </remarks>
    public sealed class ConfigRulesTests
    {
        [Fact]
        public void The_shipped_data_has_no_findings()
        {
            // The baseline every other test breaks on purpose.
            Assert.Empty(ConfigValidator.Validate(ConfigFixture.Load()));
        }

        // -- duplicate ids ------------------------------------------------------

        [Fact]
        public void A_duplicate_item_id_is_reported_against_the_second_row()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.takrut_tone").Id = "item.mor_knife";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("id.duplicate", Codes(findings));
            Assert.Contains("item['item.mor_knife']", Targets(findings));
        }

        [Fact]
        public void An_item_may_not_reuse_a_bag_id()
        {
            // item and bag share one namespace because shop_pool, merge_recipe
            // and the bag UI all address a piece by a bare id.
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").Id = "bag.raw_cotton_bag";

            Assert.Contains("id.duplicate", Codes(ConfigValidator.Validate(config)));
        }

        // -- missing references -------------------------------------------------

        [Fact]
        public void An_item_skill_that_does_not_exist_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").SkillId = "skill.does_not_exist";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("item.skill_missing", Codes(findings));
            Assert.Contains("item['item.mor_knife'].skill_id", Targets(findings));
        }

        [Fact]
        public void An_item_description_key_that_does_not_exist_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").DescriptionKey = "item.mor_knife.nope";

            Assert.Contains("item.description_key_missing", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_wave_that_spawns_a_missing_enemy_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Waves[0].EnemyId = "enemy.does_not_exist";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("wave.enemy_missing", Codes(findings));
            Assert.Contains("wave['" + config.Waves[0].Id + "'].enemy_id", Targets(findings));
        }

        [Fact]
        public void A_wave_on_a_missing_floor_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Waves[0].FloorId = "floor.does_not_exist";

            Assert.Contains("wave.floor_missing", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_boss_floor_naming_a_missing_enemy_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Floor(config, "floor.forest_5").BossEnemyId = "enemy.does_not_exist";

            Assert.Contains("floor.boss_enemy_missing", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_boss_floor_naming_a_non_boss_enemy_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Floor(config, "floor.forest_5").BossEnemyId = "enemy.krasue";

            Assert.Contains("floor.boss_enemy_not_boss", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_merge_recipe_for_a_missing_piece_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.MergeRecipes[0].ItemId = "item.does_not_exist";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("merge_recipe.target_missing", Codes(findings));
            Assert.Contains("merge_recipe['" + config.MergeRecipes[0].Id + "'].item_id", Targets(findings));
        }

        [Fact]
        public void A_merge_recipe_targeting_both_an_item_and_a_bag_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.MergeRecipes[0].BagId = "bag.raw_cotton_bag";

            Assert.Contains("merge_recipe.target_ambiguous", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_merge_recipe_targeting_neither_an_item_nor_a_bag_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.MergeRecipes[0].ItemId = string.Empty;

            Assert.Contains("merge_recipe.target_ambiguous", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_shop_pool_row_offering_a_missing_piece_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.ShopPool[0].ItemId = "item.does_not_exist";

            Assert.Contains("shop_pool.target_missing", Codes(ConfigValidator.Validate(config)));
        }

        // -- footprints ---------------------------------------------------------

        [Fact]
        public void An_empty_bag_footprint_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Bag(config, "bag.yant_cloth_pouch").Footprint = Footprint.Empty;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("footprint.empty", Codes(findings));
            Assert.Contains("bag['bag.yant_cloth_pouch'].footprint", Targets(findings));
        }

        [Theory]
        [InlineData("0,0|0,0", "footprint.duplicate_cell")]
        [InlineData("0,1|0,1|1,1", "footprint.duplicate_cell")]
        [InlineData("", "footprint.empty")]
        [InlineData("0", "footprint.malformed_cell")]
        [InlineData("0,", "footprint.malformed_cell")]
        [InlineData(",0", "footprint.malformed_cell")]
        [InlineData("a,b", "footprint.malformed_cell")]
        [InlineData("0,0|", "footprint.malformed_cell")]
        public void A_malformed_footprint_is_rejected_with_a_reason(string cells, string expectedError)
        {
            // An empty cell list is "no shape at all"; a single empty token would
            // be a malformed cell instead, so the two cases stay distinguishable.
            string[] tokens = cells.Length == 0 ? Array.Empty<string>() : cells.Split('|');

            bool parsed = Footprint.TryParse(tokens, out Footprint footprint, out string? error);

            Assert.False(parsed);
            Assert.Equal(expectedError, error);
            Assert.True(footprint.IsEmpty);
        }

        [Fact]
        public void A_footprint_is_normalised_to_the_origin()
        {
            Assert.True(Footprint.TryParse(new[] { "2,3", "3,3", "2,4" }, out Footprint footprint, out _));

            Assert.Equal(3, footprint.CellCount);
            Assert.Equal(2, footprint.Width);
            Assert.Equal(2, footprint.Height);
            Assert.True(footprint.Contains(new CellOffset(0, 0)));
            Assert.True(footprint.Contains(new CellOffset(1, 0)));
            Assert.True(footprint.Contains(new CellOffset(0, 1)));
            Assert.False(footprint.Contains(new CellOffset(1, 1)));
        }

        // -- cooldowns ----------------------------------------------------------

        [Fact]
        public void A_non_positive_cooldown_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Skill(config, "skill.knife_thrust").CooldownSeconds = 0f;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("skill.cooldown_not_positive", Codes(findings));
            Assert.Contains("skill['skill.knife_thrust'].cooldown_seconds", Targets(findings));
        }

        [Fact]
        public void A_cooldown_below_the_configured_minimum_is_reported()
        {
            // Combos can only LOWER a cooldown, so the floor applies to the
            // authored value too (GDD: no infinite-cast build).
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Skill(config, "skill.knife_thrust").CooldownSeconds =
                config.Constant.MinCooldownSeconds / 2f;

            Assert.Contains("skill.cooldown_below_minimum", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_minimum_cooldown_of_zero_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Constant.MinCooldownSeconds = 0f;

            Assert.Contains("constant.min_cooldown_not_positive", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_pierce_skill_without_pierce_count_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Skill(config, "skill.thread_pierce").PierceCount = 0;

            Assert.Contains("skill.pierce_count_invalid", Codes(ConfigValidator.Validate(config)));
        }

        // -- shop pool weights --------------------------------------------------

        [Fact]
        public void A_shop_pool_that_cannot_fill_any_slot_on_a_floor_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            foreach (ShopPoolEntryConfig entry in config.ShopPool)
            {
                entry.Weight = 0;
            }

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("shop_pool.weight_not_positive", Codes(findings));
            Assert.Contains("shop_pool.weight_total_not_usable", Codes(findings));
        }

        [Fact]
        public void A_shop_pool_band_that_skips_a_floor_is_reported()
        {
            // Reducing every band to floor 1-2 leaves floors 3..5 with nothing.
            LubanGameConfig config = ConfigFixture.Load();
            foreach (ShopPoolEntryConfig entry in config.ShopPool)
            {
                entry.FloorMax = 2;
            }

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("shop_pool.weight_total_not_usable", Codes(findings));
            Assert.Contains("floor['floor.forest_3'].shop_pool", Targets(findings));
        }

        [Fact]
        public void A_shop_pool_floor_range_that_is_not_ordered_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.ShopPool[0].FloorMax = 0;

            Assert.Contains("shop_pool.floor_range_invalid", Codes(ConfigValidator.Validate(config)));
        }

        // -- merge tiers --------------------------------------------------------

        [Fact]
        public void A_merge_recipe_that_skips_a_tier_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.MergeRecipes[0].ToTier = 3;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("merge_recipe.tier_not_sequential", Codes(findings));
            Assert.Contains("merge_recipe['" + config.MergeRecipes[0].Id + "']", Targets(findings));
        }

        [Fact]
        public void A_merge_recipe_above_max_tier_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Constant.MaxTier = 2;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("merge_recipe.tier_exceeds_max", Codes(findings));
        }

        // -- prices and weights -------------------------------------------------

        [Fact]
        public void A_non_positive_item_price_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").Price = 0;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("price.not_positive", Codes(findings));
            Assert.Contains("item['item.mor_knife'].price", Targets(findings));
        }

        [Fact]
        public void A_negative_price_is_reported_for_items_and_bags_alike()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").Price = -1;
            ConfigFixture.Bag(config, "bag.yant_cloth_pouch").Price = -5;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("item['item.mor_knife'].price", Targets(findings));
            Assert.Contains("bag['bag.yant_cloth_pouch'].price", Targets(findings));
        }

        [Fact]
        public void A_negative_weight_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").Weight = -0.1f;

            Assert.Contains("weight.negative", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_shop_pool_weight_that_is_not_positive_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.ShopPool[0].Weight = -3;

            Assert.Contains("shop_pool.weight_not_positive", Codes(ConfigValidator.Validate(config)));
        }

        // -- floors and waves ---------------------------------------------------

        [Fact]
        public void A_duplicate_floor_index_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Floor(config, "floor.forest_5").Index = 4;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("floor.index_duplicate", Codes(findings));
            Assert.Contains("floor['floor.forest_5'].index", Targets(findings));
        }

        [Fact]
        public void A_gap_in_the_floor_ladder_is_reported()
        {
            // 1,2,3,4,7 leaves 5 and 6 missing, which would end the run early.
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Floor(config, "floor.forest_5").Index = 7;

            Assert.Contains("floor.index_gap", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_floor_with_no_wave_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            foreach (WaveConfig wave in config.Waves.Where(w => w.FloorId == "floor.forest_2"))
            {
                wave.FloorId = "floor.forest_1";
            }

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("wave.floor_without_wave", Codes(findings));
            Assert.Contains("floor['floor.forest_2']", Targets(findings));
        }

        [Fact]
        public void A_wave_that_outlives_its_floor_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Waves[0].EndTimeSeconds = 61f;

            Assert.Contains("wave.exceeds_floor", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_wave_with_a_backwards_time_window_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Waves[0].StartTimeSeconds = 30f;
            config.Waves[0].EndTimeSeconds = 30f;

            Assert.Contains("wave.time_window_invalid", Codes(ConfigValidator.Validate(config)));
        }

        // -- enemies ------------------------------------------------------------

        [Fact]
        public void An_enemy_whose_role_and_boss_flag_disagree_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Enemy(config, "enemy.pob").IsBoss = false;

            Assert.Contains("enemy.boss_role_mismatch", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_boss_without_a_phase_two_threshold_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Enemy(config, "enemy.pob").BossPhase2HpPercent = 0f;

            Assert.Contains("enemy.boss_phase2_percent_invalid", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void An_enemy_with_no_hp_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Enemy(config, "enemy.krasue").MaxHp = 0;

            Assert.Contains("enemy.max_hp_not_positive", Codes(ConfigValidator.Validate(config)));
        }

        // -- synergies ----------------------------------------------------------

        [Fact]
        public void A_synergy_that_changes_nothing_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Synergies[0].ValuePercent = 0f;

            Assert.Contains("synergy.value_zero", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_tag_matching_synergy_without_tags_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Synergies[1].TagB = string.Empty;

            Assert.Contains("synergy.tag_missing", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_bag_area_synergy_needs_no_tags()
        {
            LubanGameConfig config = ConfigFixture.Load();
            SynergyConfig area = config.Synergies.First(s => s.Kind == SynergyKind.BagArea);

            Assert.Equal(string.Empty, area.TagA);
            Assert.Equal(string.Empty, area.TagB);
            Assert.Empty(ConfigValidator.Validate(config));
        }

        // -- items, economy, constants ------------------------------------------

        [Fact]
        public void An_item_declared_as_a_bag_piece_in_the_item_table_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").Category = ItemCategory.Bag;

            Assert.Contains("item.category_bag", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_sell_back_percentage_outside_0_100_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Economy.SellBackPercent = 150;

            Assert.Contains("economy.sell_back_percent_invalid", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_max_tier_outside_the_supported_range_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Constant.MaxTier = 4;

            Assert.Contains("constant.max_tier_out_of_range", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_board_smaller_than_one_cell_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Constant.BoardWidth = 0;

            Assert.Contains("constant.board_not_positive", Codes(ConfigValidator.Validate(config)));
        }

        // -- localization -------------------------------------------------------

        [Fact]
        public void A_localization_row_missing_a_language_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            LocalizationEntry entry = config.Localization[0];
            entry.En = string.Empty;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("localization.text_missing", Codes(findings));
            Assert.Contains("localization['" + entry.Key + "'].en", Targets(findings));
        }

        [Fact]
        public void Every_shipped_localization_row_has_both_languages()
        {
            // Guards the fixture: the rule above must have something to hold on to.
            IGameConfig config = ConfigFixture.Load();
            Assert.All(config.Localization, entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Th), entry.Key + " has no Thai text");
                Assert.False(string.IsNullOrWhiteSpace(entry.En), entry.Key + " has no English text");
            });
        }

        private static HashSet<string> Codes(IReadOnlyList<Finding> findings) =>
            new HashSet<string>(findings.Select(finding => finding.Code), StringComparer.Ordinal);

        private static HashSet<string> Targets(IReadOnlyList<Finding> findings) =>
            new HashSet<string>(findings.Select(finding => finding.Target), StringComparer.Ordinal);
    }
}
