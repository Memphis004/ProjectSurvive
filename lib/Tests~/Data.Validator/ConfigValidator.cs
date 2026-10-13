using System;
using System.Collections.Generic;
using System.Text;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Data.Validator
{
    /// <summary>One validation failure. <see cref="Code"/> is a stable identifier.</summary>
    public sealed class Finding
    {
        public Finding(string code, string target, string message)
        {
            Code = code;
            Target = target;
            Message = message;
        }

        public string Code { get; }

        /// <summary>What the finding is about, e.g. <c>item['item.mor_knife'].price</c>.</summary>
        public string Target { get; }

        public string Message { get; }

        public override string ToString() => Code + "  " + Target + "  " + Message;
    }

    /// <summary>
    /// Semantic validation of a loaded config: the rules Luban cannot express.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The split of responsibility is deliberate. Luban (run with <c>--strict</c>
    /// by tools/gen.ps1) enforces the SCHEMA: types, required <c>#ref</c> targets,
    /// unique primary keys and <c>#(range=...)</c> bounds. This validator enforces
    /// the GAME rules that span tables, including every optional reference, which
    /// Luban cannot check because "no value" is a legitimate cell there.
    /// </para>
    /// <para>
    /// It runs on <see cref="IGameConfig"/> — the domain projection — so a rule
    /// can never accidentally depend on a generated row type, and the same rules
    /// are unit-testable with a hand-built config.
    /// </para>
    /// </remarks>
    public static class ConfigValidator
    {
        public static IReadOnlyList<Finding> Validate(IGameConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var findings = new List<Finding>();
            ValidateIds(config, findings);
            ValidateBags(config, findings);
            ValidateItems(config, findings);
            ValidateSkills(config, findings);
            ValidateSynergies(config, findings);
            ValidateMergeRecipes(config, findings);
            ValidateEnemies(config, findings);
            ValidateFloors(config, findings);
            ValidateWaves(config, findings);
            ValidateShopPool(config, findings);
            ValidateEconomy(config, findings);
            ValidateConstant(config, findings);
            ValidateLocalization(config, findings);
            return findings;
        }

        // -- shared helpers ---------------------------------------------------

        private static void Check(bool condition, List<Finding> findings, string code, string target, string message)
        {
            if (!condition)
            {
                findings.Add(new Finding(code, target, message));
            }
        }

        private static bool Present(string? value) => !string.IsNullOrEmpty(value);

        private static string Row(string table, string id) => table + "['" + id + "']";

        // -- 1. stable ids ----------------------------------------------------

        private static void ValidateIds(IGameConfig config, List<Finding> findings)
        {
            // Item and bag ids share one namespace: the shop pool, the merge
            // recipes and the bag UI all address a piece by id, so a collision
            // would make one of them unreachable.
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (BagConfig bag in config.Bags)
            {
                Check(Present(bag.Id), findings, "id.empty", Row("bag", bag.Id), "bag id must not be empty");
                if (Present(bag.Id) && ids.TryGetValue(bag.Id, out string? owner))
                {
                    findings.Add(new Finding("id.duplicate",
                        Row("bag", bag.Id), "id '" + bag.Id + "' is already used by " + owner));
                }

                ids[bag.Id] = "tbbag";
            }

            foreach (ItemConfig item in config.Items)
            {
                Check(Present(item.Id), findings, "id.empty", Row("item", item.Id), "item id must not be empty");
                if (Present(item.Id) && ids.TryGetValue(item.Id, out string? owner))
                {
                    findings.Add(new Finding("id.duplicate",
                        Row("item", item.Id), "id '" + item.Id + "' is already used by " + owner));
                }

                ids[item.Id] = "tbitem";
            }
        }

        // -- 2. bags ---------------------------------------------------------

        private static void ValidateBags(IGameConfig config, List<Finding> findings)
        {
            foreach (BagConfig bag in config.Bags)
            {
                string row = Row("bag", bag.Id);

                // An empty footprint opens no ActiveSlot, so the piece could
                // never hold anything. Duplicated / malformed cells are caught
                // before this point (Footprint.TryParse -> ConfigLoadException).
                Check(!bag.Footprint.IsEmpty, findings, "footprint.empty", row + ".footprint",
                    "footprint has no cell");

                Check(bag.Price > 0, findings, "price.not_positive", row + ".price",
                    "price must be >= 1, was " + bag.Price);

                Check(bag.Weight >= 0f, findings, "weight.negative", row + ".weight",
                    "weight must not be negative");
            }
        }

        // -- 3. items --------------------------------------------------------

        private static void ValidateItems(IGameConfig config, List<Finding> findings)
        {
            foreach (ItemConfig item in config.Items)
            {
                string row = Row("item", item.Id);

                Check(item.Category != ItemCategory.Bag, findings, "item.category_bag",
                    row + ".category", "bag pieces belong in the bag table, not the item table");

                Check(item.Price > 0, findings, "price.not_positive", row + ".price",
                    "price must be >= 1, was " + item.Price);

                Check(item.Weight >= 0f, findings, "weight.negative", row + ".weight",
                    "weight must not be negative");

                // An item is placeable only where a bag covers every one of its
                // cells, so an item with no shape is content the shop sells and
                // nothing can hold.
                Check(!item.Footprint.IsEmpty, findings, "footprint.empty", row + ".footprint",
                    "footprint has no cell");

                if (Present(item.SkillId))
                {
                    Check(config.TryGetSkill(item.SkillId, out _), findings, "item.skill_missing",
                        row + ".skill_id", "skill '" + item.SkillId + "' does not exist");
                }

                if (Present(item.DescriptionKey))
                {
                    Check(config.TryGetLocalization(item.DescriptionKey, out _), findings,
                        "item.description_key_missing", row + ".description_key",
                        "localization key '" + item.DescriptionKey + "' does not exist");
                }
            }
        }

        // -- 4. skills -------------------------------------------------------

        private static void ValidateSkills(IGameConfig config, List<Finding> findings)
        {
            float minimum = config.Constant.MinCooldownSeconds;

            foreach (SkillConfig skill in config.Skills)
            {
                string row = Row("skill", skill.Id);

                Check(skill.CooldownSeconds > 0f, findings, "skill.cooldown_not_positive",
                    row + ".cooldown_seconds", "cooldown must be > 0, was " + skill.CooldownSeconds);

                // The combo rules can only ever LOWER a cooldown, so a base value
                // below constant.min_cooldown_seconds would already be illegal
                // before any synergy is applied.
                Check(skill.CooldownSeconds >= minimum, findings, "skill.cooldown_below_minimum",
                    row + ".cooldown_seconds",
                    "cooldown " + skill.CooldownSeconds + " is below constant.min_cooldown_seconds "
                    + minimum);

                Check(skill.MpCost >= 0, findings, "skill.mp_cost_negative", row + ".mp_cost",
                    "mp_cost must not be negative");

                Check(skill.Damage >= 0, findings, "skill.damage_negative", row + ".damage",
                    "damage must not be negative");

                Check(skill.ProjectileCount >= 1, findings, "skill.projectile_count_invalid",
                    row + ".projectile_count", "projectile_count must be >= 1");

                if (skill.Behavior == SkillBehavior.Pierce)
                {
                    Check(skill.PierceCount >= 1, findings, "skill.pierce_count_invalid",
                        row + ".pierce_count", "a Pierce skill needs pierce_count >= 1");
                }
            }
        }

        // -- 5. synergies ----------------------------------------------------

        private static void ValidateSynergies(IGameConfig config, List<Finding> findings)
        {
            // The tag vocabulary is whatever the item table carries: a synergy
            // matching by tag, so a tag no item carries is a rule that can never
            // fire. This is the check Stage 02 deliberately left open until the
            // Stage 04 content defined the vocabulary.
            var vocabulary = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemConfig item in config.Items)
            {
                foreach (string tag in item.Tags)
                {
                    vocabulary.Add(tag);
                }
            }

            foreach (SynergyConfig synergy in config.Synergies)
            {
                string row = Row("synergy", synergy.Id);

                Check(Math.Abs(synergy.ValuePercent) > float.Epsilon, findings, "synergy.value_zero",
                    row + ".value_percent", "a synergy that changes nothing is a dead rule");

                if (synergy.Kind != SynergyKind.BagArea)
                {
                    Check(Present(synergy.TagA), findings, "synergy.tag_missing", row + ".tag_a",
                        "kind " + synergy.Kind + " needs a matching tag");
                    Check(Present(synergy.TagB), findings, "synergy.tag_missing", row + ".tag_b",
                        "kind " + synergy.Kind + " needs a matching tag");
                }

                Check(synergy.MaxStacks >= 1, findings, "synergy.max_stacks_invalid",
                    row + ".max_stacks", "max_stacks must be >= 1");

                if (synergy.Kind != SynergyKind.BagArea)
                {
                    Check(vocabulary.Contains(synergy.TagA), findings, "synergy.tag_unknown",
                        row + ".tag_a", "no item carries the tag '" + synergy.TagA + "'");
                    Check(vocabulary.Contains(synergy.TagB), findings, "synergy.tag_unknown",
                        row + ".tag_b", "no item carries the tag '" + synergy.TagB + "'");
                }
            }
        }

        // -- 6. merge recipes -------------------------------------------------

        private static void ValidateMergeRecipes(IGameConfig config, List<Finding> findings)
        {
            int maxTier = config.Constant.MaxTier;

            foreach (MergeRecipeConfig recipe in config.MergeRecipes)
            {
                string row = Row("merge_recipe", recipe.Id);
                bool hasItem = Present(recipe.ItemId);
                bool hasBag = Present(recipe.BagId);

                if (hasItem == hasBag)
                {
                    findings.Add(new Finding("merge_recipe.target_ambiguous", row,
                        hasItem
                            ? "exactly one of item_id / bag_id may be set, both are"
                            : "one of item_id / bag_id must be set"));
                    continue;
                }

                if (hasItem)
                {
                    Check(config.TryGetItem(recipe.ItemId, out _), findings, "merge_recipe.target_missing",
                        row + ".item_id", "item '" + recipe.ItemId + "' does not exist");
                }
                else
                {
                    Check(config.TryGetBag(recipe.BagId, out _), findings, "merge_recipe.target_missing",
                        row + ".bag_id", "bag '" + recipe.BagId + "' does not exist");
                }

                // GDD: merging two pieces of the same id and tier yields EXACTLY
                // the next tier - a recipe that skips a tier would let a player
                // jump from Common straight to GrandArcane.
                Check(recipe.ToTier == recipe.FromTier + 1, findings, "merge_recipe.tier_not_sequential",
                    row,
                    "to_tier " + recipe.ToTier + " must be from_tier + 1 (" + (recipe.FromTier + 1) + ")");

                Check(recipe.FromTier >= 1, findings, "merge_recipe.from_tier_invalid", row + ".from_tier",
                    "from_tier must be >= 1");

                Check(recipe.ToTier <= maxTier, findings, "merge_recipe.tier_exceeds_max", row + ".to_tier",
                    "to_tier " + recipe.ToTier + " exceeds constant.max_tier " + maxTier);
            }

            // Every piece must be able to reach the maximum tier. A missing rung
            // - the classic typo, one recipe short - strands a player holding two
            // tier-2 copies of a piece with nothing left to merge them into.
            var rungs = new HashSet<string>(StringComparer.Ordinal);
            foreach (MergeRecipeConfig recipe in config.MergeRecipes)
            {
                if (Present(recipe.TargetId) && recipe.ToTier == recipe.FromTier + 1)
                {
                    rungs.Add(recipe.TargetId + "#" + recipe.FromTier);
                }
            }

            foreach (BagConfig bag in config.Bags)
            {
                CheckLadder(bag.Id, "bag", rungs, maxTier, findings);
            }

            foreach (ItemConfig item in config.Items)
            {
                CheckLadder(item.Id, "item", rungs, maxTier, findings);
            }
        }

        private static void CheckLadder(string id, string table, HashSet<string> rungs, int maxTier,
            List<Finding> findings)
        {
            if (maxTier < 1 || !Present(id))
            {
                return;
            }

            for (int tier = 1; tier < maxTier; tier++)
            {
                Check(rungs.Contains(id + "#" + tier), findings, "merge_recipe.ladder_incomplete",
                    Row(table, id) + ".merge",
                    "no merge recipe takes '" + id + "' from tier " + tier + " to " + (tier + 1));
            }
        }

        // -- 7. enemies -------------------------------------------------------

        private static void ValidateEnemies(IGameConfig config, List<Finding> findings)
        {
            foreach (EnemyConfig enemy in config.Enemies)
            {
                string row = Row("enemy", enemy.Id);

                Check(enemy.MaxHp > 0, findings, "enemy.max_hp_not_positive", row + ".max_hp",
                    "max_hp must be >= 1, was " + enemy.MaxHp);

                Check(enemy.MoveSpeed >= 0f, findings, "enemy.move_speed_negative", row + ".move_speed",
                    "move_speed must not be negative");

                Check(enemy.ContactDamage >= 0, findings, "enemy.contact_damage_negative",
                    row + ".contact_damage", "contact_damage must not be negative");

                Check(enemy.AttackCooldownSeconds > 0f, findings, "enemy.attack_cooldown_not_positive",
                    row + ".attack_cooldown_seconds", "attack_cooldown must be > 0");

                if (enemy.IsBoss)
                {
                    Check(enemy.BossPhase2HpPercent > 0f && enemy.BossPhase2HpPercent < 100f, findings,
                        "enemy.boss_phase2_percent_invalid", row + ".boss_phase2_hp_percent",
                        "a boss needs a phase-2 threshold strictly between 0 and 100");
                }

                bool roleIsBoss = enemy.Role == EnemyRole.Boss;
                Check(roleIsBoss == enemy.IsBoss, findings, "enemy.boss_role_mismatch",
                    row, "role Boss and is_boss must agree");
            }
        }

        // -- 8. floors -------------------------------------------------------

        private static void ValidateFloors(IGameConfig config, List<Finding> findings)
        {
            int maximum = 0;
            var seen = new HashSet<int>();

            foreach (FloorConfig floor in config.Floors)
            {
                string row = Row("floor", floor.Id);

                Check(floor.DurationSeconds > 0f, findings, "floor.duration_not_positive",
                    row + ".duration_seconds", "duration must be > 0");

                if (!seen.Add(floor.Index))
                {
                    findings.Add(new Finding("floor.index_duplicate", row + ".index",
                        "floor index " + floor.Index + " is used twice"));
                }

                if (floor.Index > maximum)
                {
                    maximum = floor.Index;
                }

                if (floor.IsBoss)
                {
                    Check(Present(floor.BossEnemyId), findings, "floor.boss_enemy_missing",
                        row + ".boss_enemy_id", "a boss floor must name its boss");

                    if (Present(floor.BossEnemyId)
                        && config.TryGetEnemy(floor.BossEnemyId, out EnemyConfig? boss))
                    {
                        Check(boss.IsBoss, findings, "floor.boss_enemy_not_boss",
                            row + ".boss_enemy_id", "enemy '" + boss.Id + "' is not marked is_boss");
                    }
                }

                if (Present(floor.BossEnemyId))
                {
                    Check(config.TryGetEnemy(floor.BossEnemyId, out _), findings,
                        "floor.boss_enemy_missing", row + ".boss_enemy_id",
                        "enemy '" + floor.BossEnemyId + "' does not exist");
                }
            }

            // Floors are a 1..N ladder; a gap would end the run early.
            for (int index = 1; index <= maximum; index++)
            {
                Check(seen.Contains(index), findings, "floor.index_gap", "floor.index",
                    "floor index " + index + " is missing from the ladder 1.." + maximum);
            }
        }

        // -- 9. waves --------------------------------------------------------

        private static void ValidateWaves(IGameConfig config, List<Finding> findings)
        {
            var floorsWithWaves = new HashSet<string>(StringComparer.Ordinal);

            foreach (WaveConfig wave in config.Waves)
            {
                string row = Row("wave", wave.Id);

                Check(config.TryGetFloor(wave.FloorId, out FloorConfig? floor), findings,
                    "wave.floor_missing", row + ".floor_id",
                    "floor '" + wave.FloorId + "' does not exist");

                // The explicit acceptance case: a wave that spawns an enemy which
                // is not in the enemy table would crash the battle at runtime.
                Check(config.TryGetEnemy(wave.EnemyId, out _), findings, "wave.enemy_missing",
                    row + ".enemy_id", "enemy '" + wave.EnemyId + "' does not exist");

                Check(wave.StartTimeSeconds >= 0f, findings, "wave.time_window_invalid",
                    row + ".start_time_seconds", "start time must not be negative");

                Check(wave.EndTimeSeconds > wave.StartTimeSeconds, findings, "wave.time_window_invalid",
                    row, "end time " + wave.EndTimeSeconds + " must be after start "
                    + wave.StartTimeSeconds);

                if (floor is not null)
                {
                    Check(wave.EndTimeSeconds <= floor.DurationSeconds, findings, "wave.exceeds_floor",
                        row + ".end_time_seconds",
                        "wave ends at " + wave.EndTimeSeconds + " but floor " + floor.Id
                        + " lasts " + floor.DurationSeconds);
                }

                Check(wave.SpawnCount >= 1, findings, "wave.spawn_count_invalid", row + ".spawn_count",
                    "spawn_count must be >= 1");

                Check(wave.SpawnIntervalSeconds > 0f, findings, "wave.spawn_interval_invalid",
                    row + ".spawn_interval_seconds", "spawn interval must be > 0");

                // The last spawn has to fit inside the window, otherwise the wave
                // silently promises more ghosts than it can ever deliver.
                float needed = (wave.SpawnCount - 1) * wave.SpawnIntervalSeconds;
                float window = wave.EndTimeSeconds - wave.StartTimeSeconds;
                Check(needed <= window, findings, "wave.spawn_window_too_short", row,
                    "spawn_count " + wave.SpawnCount + " at " + wave.SpawnIntervalSeconds
                    + "s needs " + needed + "s but the window is " + window + "s");

                floorsWithWaves.Add(wave.FloorId);
            }

            foreach (FloorConfig floor in config.Floors)
            {
                Check(floorsWithWaves.Contains(floor.Id), findings, "wave.floor_without_wave",
                    Row("floor", floor.Id), "floor has no wave, so it can never spawn anything");
            }
        }

        // -- 10. shop pool ---------------------------------------------------

        private static void ValidateShopPool(IGameConfig config, List<Finding> findings)
        {
            foreach (ShopPoolEntryConfig entry in config.ShopPool)
            {
                string row = Row("shop_pool", entry.Id);
                bool hasItem = Present(entry.ItemId);
                bool hasBag = Present(entry.BagId);

                if (hasItem == hasBag)
                {
                    findings.Add(new Finding("shop_pool.target_ambiguous", row,
                        hasItem
                            ? "exactly one of item_id / bag_id may be set, both are"
                            : "one of item_id / bag_id must be set"));
                }
                else if (hasItem)
                {
                    Check(config.TryGetItem(entry.ItemId, out _), findings, "shop_pool.target_missing",
                        row + ".item_id", "item '" + entry.ItemId + "' does not exist");
                }
                else
                {
                    Check(config.TryGetBag(entry.BagId, out _), findings, "shop_pool.target_missing",
                        row + ".bag_id", "bag '" + entry.BagId + "' does not exist");
                }

                Check(entry.Weight > 0, findings, "shop_pool.weight_not_positive", row + ".weight",
                    "weight must be >= 1, was " + entry.Weight);

                Check(entry.FloorMin >= 1 && entry.FloorMax >= entry.FloorMin, findings,
                    "shop_pool.floor_range_invalid", row,
                    "floor range [" + entry.FloorMin + "," + entry.FloorMax + "] is not ordered and 1-based");
            }

            // The explicit acceptance case: the roll is a weighted pick, so for
            // every reachable floor the weights that apply must sum to something
            // usable. All-zero (or empty) means the shop cannot produce stock.
            foreach (FloorConfig floor in config.Floors)
            {
                long total = 0;
                foreach (ShopPoolEntryConfig entry in config.ShopPool)
                {
                    if (entry.Weight > 0
                        && floor.Index >= entry.FloorMin
                        && floor.Index <= entry.FloorMax)
                    {
                        total += entry.Weight;
                    }
                }

                Check(total > 0, findings, "shop_pool.weight_total_not_usable",
                    Row("floor", floor.Id) + ".shop_pool",
                    "no shop_pool entry with a positive weight covers floor index " + floor.Index);
            }

            // Every piece must be obtainable. A bag or item that no shop_pool band
            // offers - and that the starting loadout does not grant - could never
            // enter a run, so it would be authored content nobody can ever see.
            var offered = new HashSet<string>(StringComparer.Ordinal);
            foreach (ShopPoolEntryConfig entry in config.ShopPool)
            {
                if (entry.Weight > 0 && Present(entry.OfferedId))
                {
                    offered.Add(entry.OfferedId);
                }
            }

            foreach (BagConfig bag in config.Bags)
            {
                CheckPieceOffered(bag.Id, "bag", offered, findings);
            }

            foreach (ItemConfig item in config.Items)
            {
                CheckPieceOffered(item.Id, "item", offered, findings);
            }
        }

        private static void CheckPieceOffered(string id, string table, HashSet<string> offered, List<Finding> findings)
        {
            if (!Present(id))
            {
                return;
            }

            Check(offered.Contains(id), findings, "shop_pool.piece_unreachable",
                Row(table, id) + ".shop_pool",
                "no shop_pool entry with a positive weight offers '" + id + "'");
        }

        // -- 13. localization --------------------------------------------------

        private static void ValidateLocalization(IGameConfig config, List<Finding> findings)
        {
            // Player-facing text comes from this table and nowhere else (AGENTS
            // rule 5), so a row with one language missing is a shipping bug: the
            // adapter would silently serve the other language.
            //
            // Deliberately NOT checked here: a key that no data row references.
            // `ui.*` keys are referenced by the View layer, not by data/, so
            // "unused" is not a mistake on its own.
            foreach (LocalizationEntry entry in config.Localization)
            {
                string row = Row("localization", entry.Key);

                Check(Present(entry.Key), findings, "localization.key_empty", "localization",
                    "localization key must not be empty");

                Check(Present(entry.Th), findings, "localization.text_missing", row + ".th",
                    "Thai text is missing");

                Check(Present(entry.En), findings, "localization.text_missing", row + ".en",
                    "English text is missing");
            }
        }

        // -- 11/12. singletons -----------------------------------------------

        private static void ValidateEconomy(IGameConfig config, List<Finding> findings)
        {
            EconomyConfig economy = config.Economy;

            Check(economy.StartBia >= 0, findings, "economy.start_bia_negative", "economy.start_bia",
                "start_bia must not be negative");

            Check(economy.ShopSlotCount >= 1, findings, "economy.shop_slot_count_invalid",
                "economy.shop_slot_count", "shop_slot_count must be >= 1");

            Check(economy.RefreshBaseCost >= 0, findings, "economy.refresh_cost_invalid",
                "economy.refresh_base_cost", "refresh_base_cost must not be negative");

            Check(economy.RefreshCostIncrement >= 0, findings, "economy.refresh_cost_invalid",
                "economy.refresh_cost_increment", "refresh_cost_increment must not be negative");

            Check(economy.SellBackPercent >= 0 && economy.SellBackPercent <= 100, findings,
                "economy.sell_back_percent_invalid", "economy.sell_back_percent",
                "sell_back_percent must be within 0..100");

            Check(economy.HealPercentOnClear >= 0 && economy.HealPercentOnClear <= 100, findings,
                "economy.heal_percent_invalid", "economy.heal_percent_on_clear",
                "heal_percent_on_clear must be within 0..100");

            Check(economy.ChestSlots >= 0, findings, "economy.chest_slots_invalid",
                "economy.chest_slots", "chest_slots must not be negative");
        }

        private static void ValidateConstant(IGameConfig config, List<Finding> findings)
        {
            ConstantConfig constant = config.Constant;

            Check(constant.BoardWidth >= 1 && constant.BoardHeight >= 1, findings,
                "constant.board_not_positive", "constant.board",
                "board must be at least 1x1, was " + constant.BoardWidth + "x" + constant.BoardHeight);

            Check(constant.MinCooldownSeconds > 0f, findings, "constant.min_cooldown_not_positive",
                "constant.min_cooldown_seconds", "min_cooldown_seconds must be > 0");

            Check(constant.MaxTier is >= 1 and <= 3, findings, "constant.max_tier_out_of_range",
                "constant.max_tier", "max_tier must be within 1..3, was " + constant.MaxTier);

            Check(constant.MaxHp > 0, findings, "constant.max_hp_not_positive", "constant.max_hp",
                "max_hp must be >= 1");

            Check(constant.MaxMp >= 0, findings, "constant.max_mp_negative", "constant.max_mp",
                "max_mp must not be negative");

            Check(constant.FocusPerSecond >= 0f, findings, "constant.focus_negative",
                "constant.focus_per_second", "focus_per_second must not be negative");

            Check(constant.MoveSpeed > 0f, findings, "constant.move_speed_not_positive",
                "constant.move_speed", "move_speed must be > 0");

            Check(constant.MaxSpeedPenaltyPercent is >= 0 and <= 100, findings,
                "constant.max_speed_penalty_invalid", "constant.max_speed_penalty_percent",
                "max_speed_penalty_percent must be within 0..100");
        }
    }
}
