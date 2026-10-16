using System;
using System.Collections.Generic;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
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
        [InlineData("", "footprint.empty")]
        [InlineData("...", "footprint.empty")]
        [InlineData("..|..", "footprint.empty")]
        [InlineData("#", "footprint.malformed_cell")]
        [InlineData("Xx", "footprint.malformed_cell")]
        [InlineData("X*", "footprint.malformed_cell")]
        [InlineData("0,0", "footprint.malformed_cell")]
        [InlineData("XX|X", "footprint.ragged_row")]
        [InlineData("XXX|", "footprint.ragged_row")]
        [InlineData("X||X", "footprint.ragged_row")]
        public void A_broken_footprint_picture_is_rejected_with_a_reason(string cells, string expectedError)
        {
            // No row at all, and a picture with no covered cell, are both "no shape";
            // rows of different lengths are a ragged picture. A picture makes a
            // duplicated cell unrepresentable, which is why Stage 04 replaced that
            // finding with the ragged-row one.
            string[] rows = cells.Length == 0 ? Array.Empty<string>() : cells.Split('|');

            bool parsed = Footprint.TryParse(rows, out Footprint footprint, out string? error);

            Assert.False(parsed);
            Assert.Equal(expectedError, error);
            Assert.True(footprint.IsEmpty);
        }

        [Fact]
        public void A_footprint_is_normalised_to_the_origin()
        {
            // ".X.|.XX" is the L-tromino drawn one column in, so normalising has to
            // pull it back to x = 0.
            Assert.True(Footprint.TryParse(new[] { ".X.", ".XX" }, out Footprint footprint, out _));

            Assert.Equal(3, footprint.CellCount);
            Assert.Equal(2, footprint.Width);
            Assert.Equal(2, footprint.Height);
            Assert.True(footprint.Contains(new CellOffset(0, 0)));
            Assert.True(footprint.Contains(new CellOffset(0, 1)));
            Assert.True(footprint.Contains(new CellOffset(1, 1)));
            Assert.False(footprint.Contains(new CellOffset(1, 0)));
        }

        [Fact]
        public void An_item_with_no_footprint_cell_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            ConfigFixture.Item(config, "item.mor_knife").Footprint = Footprint.Empty;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("footprint.empty", Codes(findings));
            Assert.Contains("item['item.mor_knife'].footprint", Targets(findings));
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

        [Fact]
        public void A_piece_whose_merge_ladder_skips_a_rung_is_reported()
        {
            // Dropping the 2 -> 3 recipe strands two tier-2 copies with nothing to
            // merge them into, which is the failure this rule exists for.
            LubanGameConfig config = ConfigFixture.Load();
            MergeRecipeConfig rung = config.MergeRecipes.First(recipe =>
                recipe.ItemId == "item.mor_knife" && recipe.FromTier == 2);
            rung.ItemId = string.Empty;
            rung.BagId = "bag.raw_cotton_bag";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("merge_recipe.ladder_incomplete", Codes(findings));
            Assert.Contains("item['item.mor_knife'].merge", Targets(findings));
        }

        [Fact]
        public void A_piece_no_shop_pool_entry_offers_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            foreach (ShopPoolEntryConfig entry in config.ShopPool)
            {
                if (entry.OfferedId == "item.boiled_egg")
                {
                    entry.ItemId = "item.red_soda";
                }
            }

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("shop_pool.piece_unreachable", Codes(findings));
            Assert.Contains("item['item.boiled_egg'].shop_pool", Targets(findings));
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
        public void A_wave_whose_spawns_cannot_fit_its_window_is_reported()
        {
            // 30 spawns 3s apart need 87s of window; this wave only has 60s.
            LubanGameConfig config = ConfigFixture.Load();
            config.Waves[0].SpawnCount = 30;
            config.Waves[0].SpawnIntervalSeconds = 3f;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("wave.spawn_window_too_short", Codes(findings));
            Assert.Contains("wave['" + config.Waves[0].Id + "']", Targets(findings));
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
        public void A_synergy_naming_a_tag_no_item_carries_is_reported()
        {
            // The vocabulary is whatever the item table carries; a tag outside it
            // means the combo can never be assembled.
            LubanGameConfig config = ConfigFixture.Load();
            config.Synergies[0].TagA = "tag_that_no_item_carries";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("synergy.tag_unknown", Codes(findings));
            Assert.Contains("synergy['" + config.Synergies[0].Id + "'].tag_a", Targets(findings));
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

        // -- upgrade variants (Stage 09f) ---------------------------------------

        [Fact]
        public void An_upgrade_variant_with_a_non_positive_weight_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.UpgradeVariants[0].Weight = 0;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.weight_not_positive", Codes(findings));
            Assert.Contains(
                "upgrade_variant['" + config.UpgradeVariants[0].Id + "'].weight", Targets(findings));
        }

        [Fact]
        public void A_step_whose_pool_is_too_small_for_the_chooser_is_reported()
        {
            // Three of the four 1 -> 2 variants move to the next step, which leaves the pool
            // one choice short of constant.upgrade_choice_count with no fallback declared.
            LubanGameConfig config = ConfigFixture.Load();
            int moved = 0;
            foreach (UpgradeVariantConfig variant in config.UpgradeVariants)
            {
                if (variant.FromTier == 1 && variant.Category == "ArcaneWeapon" && moved < 3)
                {
                    variant.FromTier = 2;
                    variant.ToTier = 3;
                    moved++;
                }
            }

            Assert.Equal(3, moved);

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.too_few_choices", Codes(findings));
            Assert.Contains("item['item.mor_knife'].merge t1", Targets(findings));
        }

        [Fact]
        public void An_upgrade_variant_matching_a_tag_no_item_carries_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeVariantConfig variant = config.UpgradeVariants[4];
            Assert.Equal("fire", variant.Tag);
            variant.Tag = "phoenix";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.tag_unknown", Codes(findings));
            Assert.Contains("upgrade_variant['" + variant.Id + "'].tag", Targets(findings));
        }

        [Fact]
        public void An_upgrade_variant_naming_an_unknown_category_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeVariantConfig variant = config.UpgradeVariants[0];
            variant.Category = "Wand";

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.condition_unknown", Codes(findings));
            Assert.Contains("upgrade_variant['" + variant.Id + "'].category", Targets(findings));
        }

        [Fact]
        public void A_scope_of_bag_pieces_is_reported_because_it_could_never_match()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeVariantConfig variant = config.UpgradeVariants[0];
            variant.Category = "Bag";

            Assert.Contains("upgrade_variant.category_not_an_item", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void A_variant_with_both_or_neither_condition_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeVariantConfig both = config.UpgradeVariants[0];
            both.Tag = "weapon";
            UpgradeVariantConfig neither = config.UpgradeVariants[1];
            neither.Category = string.Empty;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.condition_ambiguous", Codes(findings));
            Assert.Contains(Targets(findings), target => target == "upgrade_variant['" + both.Id + "']");
            Assert.Contains(Targets(findings), target => target == "upgrade_variant['" + neither.Id + "']");
        }

        [Fact]
        public void A_variant_that_skips_a_tier_or_exceeds_the_maximum_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeVariantConfig skip = config.UpgradeVariants[0];
            skip.ToTier = 3;
            UpgradeVariantConfig beyond = config.UpgradeVariants[5];
            beyond.ToTier = 4;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.tier_not_sequential", Codes(findings));
            Assert.Contains("upgrade_variant.tier_exceeds_max", Codes(findings));
        }

        [Fact]
        public void A_variant_with_a_missing_name_key_or_no_modifier_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeVariantConfig unnamed = config.UpgradeVariants[0];
            unnamed.NameKey = "upgrade.does_not_exist.name";
            UpgradeVariantConfig empty = config.UpgradeVariants[1];
            empty.Modifiers = new List<UpgradeModifierConfig>();

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.name_key_missing", Codes(findings));
            Assert.Contains("upgrade_variant.no_modifier", Codes(findings));
            Assert.Contains(Targets(findings), target => target == "upgrade_variant['" + empty.Id + "']");
        }

        [Fact]
        public void An_upgrade_modifier_that_changes_nothing_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            UpgradeModifierConfig modifier = config.UpgradeVariants[0].Modifiers[0];
            modifier.Magnitude = 0f;

            IReadOnlyList<Finding> findings = ConfigValidator.Validate(config);

            Assert.Contains("upgrade_variant.magnitude_zero", Codes(findings));
            Assert.Contains(
                Targets(findings),
                target => target == "upgrade_variant['" + config.UpgradeVariants[0].Id + "'].modifier['"
                    + modifier.Id + "']");
        }

        [Fact]
        public void A_choice_count_a_chooser_cannot_use_is_reported()
        {
            LubanGameConfig config = ConfigFixture.Load();
            config.Constant.UpgradeChoiceCount = 1;

            Assert.Contains("constant.upgrade_choice_count_invalid", Codes(ConfigValidator.Validate(config)));
        }

        [Fact]
        public void Every_step_that_has_choices_can_fill_the_chooser()
        {
            // Guards the fixture the rule above breaks on purpose: the shipped data must have
            // pools of at least the configured choice count, which is what makes "1 จาก 3" real.
            IGameConfig config = ConfigFixture.Load();
            var pool = new List<UpgradeVariantConfig>(8);

            foreach (ItemConfig item in config.Items)
            {
                for (int tier = 1; tier < config.Constant.MaxTier; tier++)
                {
                    UpgradePool.Collect(config, item, tier, pool);
                    if (pool.Count == 0)
                    {
                        continue;
                    }

                    Assert.True(
                        pool.Count >= config.Constant.UpgradeChoiceCount,
                        "item '" + item.Id + "' tier " + tier + " has only " + pool.Count + " choice(s)");
                }
            }
        }

        private static HashSet<string> Codes(IReadOnlyList<Finding> findings) =>
            new HashSet<string>(findings.Select(finding => finding.Code), StringComparer.Ordinal);

        private static HashSet<string> Targets(IReadOnlyList<Finding> findings) =>
            new HashSet<string>(findings.Select(finding => finding.Target), StringComparer.Ordinal);
    }
}
