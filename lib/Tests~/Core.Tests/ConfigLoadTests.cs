using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Config;
using ProjectSurvive.Data.Validator;
using Xunit;
using ValidatorProgram = ProjectSurvive.Data.Validator.Program;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Stage 02 acceptance: "generated code compiles and the Core tests read the
    /// sample tables", plus "no stub that returns a constant instead of real
    /// config" - the latter is why every assertion below is on a value that can
    /// only come from <c>lib/Tests~/data</c>.
    /// </summary>
    public sealed class ConfigLoadTests
    {
        [Fact]
        public void Sample_tables_load_and_expose_every_row()
        {
            LubanGameConfig config = ConfigFixture.Load();

            // The Stage 04 prototype content: 3 bags + 11 items = 14 pieces.
            Assert.Equal(3, config.Bags.Count);
            Assert.Equal(11, config.Items.Count);
            Assert.Equal(5, config.Skills.Count);
            Assert.Equal(5, config.Synergies.Count);
            Assert.Equal(28, config.MergeRecipes.Count);
            Assert.Equal(4, config.Enemies.Count);
            Assert.Equal(5, config.Floors.Count);
            // 10 wave windows: floor 5 gained a filler band in Stage 11c so the boss has corpses
            // to eat on a real run, not only in a test.
            Assert.Equal(10, config.Waves.Count);
            Assert.Equal(14, config.ShopPool.Count);

            // Stage 09f: 10 upgrade choices (4 per arcane tier step plus one per step scoped
            // to the `fire` tag) and their 12 modifier rows, folded onto the variants.
            Assert.Equal(10, config.UpgradeVariants.Count);
            int modifiers = 0;
            for (int i = 0; i < config.UpgradeVariants.Count; i++)
            {
                modifiers += config.UpgradeVariants[i].Modifiers.Count;
            }

            Assert.Equal(12, modifiers);

            // 192 rows: the Stage 04 content, the Stage 08 HUD labels, the Stage 09 camp screen
            // with its failure codes (camp.* 53, error.* 28, hud.* 13, item.* 22, bag.* 6 and the
            // rest), the Stage 09f row error.inventory.item_spans_bags, the Stage 09f upgrade block
            // (10 `upgrade.*` names + the 3 new error.inventory.merge_* codes), the Stage 09g
            // chooser block (7 `camp.merge.*` labels + the 12 `upgrade.stat.*` names + the unknown
            // stat fallback), the Stage 10b summary block (8 `result.*` labels and formats, all
            // of them read by the Result screen — `ResultKeys.All`), and the Stage 10 title row
            // `title.start` (the one string the title control draws), and the Stage 08c mini-bag block
            // (4 `hud.*` rows: hud.kills, hud.bag, hud.status and the hud.floor.format floor label).
            // The count is pinned so a table that silently loses rows fails here - which is how this pin
            // was corrected once before: it said 144 while data/ held 145, because the 81 rows Stage 09
            // added to data/localization/localization.csv had not been regenerated yet. Stage 11a
            // added the four charm/offering tooltip rows (camp.tooltip.charm_effect and the three
            // offering lines), taking it to 199. Stage 11c added `hud.possession`, the line the HUD
            // draws while a boss's possession has the stick turned around, taking it to 200.
            // Stage 12 added the nine settings/save-warning rows, taking it to 209. Stage 09n
            // added the seven shelf-offer tooltip rows (ราคา, เช่าได้, weight, the combos header, its
            // row format, "อีก N ชิ้น" and the empty listing), taking it to 216. Stage 13 moved the
            // last two player-facing literals out of the UI code into this table — the chest
            // panel's heading (`camp.chest.title.format`) and the camp status bar's combined
            // HP/MP line (`camp.status.hpmp.format`) — taking it to 218. Stage 13b added the
            // ten step's checklist rows (a title, five steps, the "step N/5" line and the three
            // row states), taking it to 228. Stage 13c-2a added the four settings rows that name its
            // own controls (`ui.settings.decrease`/`.increase` and the two open/close captions),
            // taking it to 232 — the pin was left at 228 then, which this stage corrects. Stage 13c-2b
            // added the five pause rows (the HUD's own หยุด control, the overlay's title, its two ways
            // out and the line that says a retreat forfeits the floor), taking it to 237.
            Assert.Equal(237, config.Localization.Count);
        }

        /// <summary>
        /// Stage 13 acceptance: the shipped content is the GDD v0.3 prototype and nothing more.
        /// </summary>
        /// <remarks>
        /// The counts above prove the tables loaded; this case proves they are the *designed*
        /// content: the GDD's six categories at their stated counts, three ghosts with distinct
        /// roles plus the two-phase Pob boss, five floors whose escalation names the GDD's ghosts,
        /// and the five combo rules. "Approximately 14 items" is pinned as 14 exactly, and a later
        /// stage that wants a fifteenth piece has to say so here — which is the point: Stage 13's
        /// scope is content complete, not content growth.
        /// </remarks>
        [Fact]
        public void Gdd_prototype_content_is_complete_and_nothing_more()
        {
            IGameConfig config = ConfigFixture.Load();

            // 14 pieces: 3 bags (GDD "ย่าม 3") + 11 items
            // (4 arcane weapons, 3 charms, 1 spell tome, 2 offerings, 1 familiar).
            Assert.Equal(3, config.Bags.Count);
            Assert.Equal(4, config.Items.Count(i => i.Category == ItemCategory.ArcaneWeapon));
            Assert.Equal(3, config.Items.Count(i => i.Category == ItemCategory.Charm));
            Assert.Equal(1, config.Items.Count(i => i.Category == ItemCategory.SpellTome));
            Assert.Equal(2, config.Items.Count(i => i.Category == ItemCategory.Offering));
            Assert.Equal(1, config.Items.Count(i => i.Category == ItemCategory.Familiar));
            Assert.Equal(14, config.Bags.Count + config.Items.Count);

            // Every piece merges to the max tier: a ladder missing a rung is what the data
            // validator refuses, and this asserts the shipped data actually carries all 14.
            foreach (ItemConfig item in config.Items)
            {
                for (int tier = item.Tier; tier < config.Constant.MaxTier; tier++)
                {
                    int from = tier;
                    Assert.Contains(
                        config.MergeRecipes,
                        r => r.ItemId == item.Id && r.FromTier == from && r.ToTier == from + 1);
                }
            }

            foreach (BagConfig bag in config.Bags)
            {
                for (int tier = bag.Tier; tier < config.Constant.MaxTier; tier++)
                {
                    int from = tier;
                    Assert.Contains(
                        config.MergeRecipes,
                        r => r.BagId == bag.Id && r.FromTier == from && r.ToTier == from + 1);
                }
            }

            // Three ghosts with three distinct behaviours plus one boss (GDD "ผีไทยใน Prototype").
            EnemyConfig filler = ConfigFixture.Enemy(config, "enemy.wandering_spirit");
            Assert.Equal(EnemyRole.Filler, filler.Role);
            Assert.Equal(MovePattern.Chase, filler.MovePattern);

            EnemyConfig krasue = ConfigFixture.Enemy(config, "enemy.krasue");
            Assert.Equal(EnemyRole.Fast, krasue.Role);
            Assert.Equal(EnemyAttackPattern.DashDrain, krasue.AttackPattern);
            Assert.True(krasue.RetreatSeconds > 0f, "a krasue bites and leaves");

            EnemyConfig tani = ConfigFixture.Enemy(config, "enemy.nang_tani");
            Assert.Equal(EnemyRole.Elite, tani.Role);
            Assert.True(tani.IsElite, "นางตานี is the prototype's elite");
            Assert.Equal(MovePattern.Stationary, tani.MovePattern);

            EnemyConfig pob = ConfigFixture.Enemy(config, "enemy.pob");
            Assert.Equal(EnemyRole.Boss, pob.Role);
            Assert.True(pob.IsBoss, "ปอบ is the boss");
            Assert.InRange(pob.BossPhase2HpPercent, 1f, 99f);
            Assert.True(pob.BossCorpseHeal > 0, "the boss eats corpses to heal");
            Assert.True(pob.BossPossessSeconds > 0f, "the boss possesses the player");
            Assert.Equal(4, config.Enemies.Count);

            // Five floors: 1–4 are timed normal floors, 5 is the 90-second boss floor (GDD).
            Assert.Equal(5, config.Floors.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, config.Floors.Select(f => f.Index).OrderBy(i => i));
            foreach (FloorConfig floor in config.Floors)
            {
                if (floor.Index == 5)
                {
                    Assert.True(floor.IsBoss, "floor 5 is the boss floor");
                    Assert.Equal("enemy.pob", floor.BossEnemyId);
                    Assert.Equal(90f, floor.DurationSeconds);
                }
                else
                {
                    Assert.False(floor.IsBoss, "floor " + floor.Index + " is a normal floor");
                    Assert.Equal(string.Empty, floor.BossEnemyId);
                    Assert.Equal(60f, floor.DurationSeconds);
                }
            }

            // The GDD's escalation: 1 filler only, 2 adds krasue, 3 the elite, 4 all three.
            Assert.Equal(new[] { "enemy.wandering_spirit" }, GhostsOnFloor(config, "floor.forest_1"));
            Assert.Equal(
                new[] { "enemy.krasue", "enemy.wandering_spirit" },
                GhostsOnFloor(config, "floor.forest_2"));
            Assert.Equal(
                new[] { "enemy.krasue", "enemy.nang_tani" },
                GhostsOnFloor(config, "floor.forest_3"));
            Assert.Equal(
                new[] { "enemy.krasue", "enemy.nang_tani", "enemy.wandering_spirit" },
                GhostsOnFloor(config, "floor.forest_4"));
            Assert.Contains("enemy.pob", GhostsOnFloor(config, "floor.forest_5"));

            // Five combo rules, matching the GDD's table: four tag pairs and the bag-area bonus.
            Assert.Equal(5, config.Synergies.Count);
            Assert.Equal(4, config.Synergies.Count(s => s.Kind == SynergyKind.AdjacentPair));
            Assert.Equal(1, config.Synergies.Count(s => s.Kind == SynergyKind.BagArea));
        }

        private static string[] GhostsOnFloor(IGameConfig config, string floorId)
        {
            return config.Waves
                .Where(w => w.FloorId == floorId)
                .Select(w => w.EnemyId)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        [Fact]
        public void Singleton_rows_carry_the_authored_balance_numbers()
        {
            IGameConfig config = ConfigFixture.Load();

            // The board is 9x7 and the run starts with 15 bia (GDD v0.3).
            Assert.Equal(9, config.Constant.BoardWidth);
            Assert.Equal(7, config.Constant.BoardHeight);
            Assert.Equal(3, config.Constant.MaxTier);
            Assert.Equal(3, config.Constant.UpgradeChoiceCount);

            // Stage 07c: the end-of-floor sweep needs a positive speed and a positive cap;
            // their exact values are balance numbers a later stage may move.
            Assert.True(config.Constant.MagnetSpeed > 0f, "magnet_speed must be positive");
            Assert.True(config.Constant.SweepTimeoutSeconds > 0f, "sweep_timeout_seconds must be positive");

            Assert.Equal(15, config.Economy.StartBia);
            Assert.Equal(4, config.Economy.ShopSlotCount);
        }

        [Fact]
        public void Row_values_are_mapped_from_the_generated_columns()
        {
            IGameConfig config = ConfigFixture.Load();

            ItemConfig knife = ConfigFixture.Item(config, "item.mor_knife");
            Assert.Equal(ItemCategory.ArcaneWeapon, knife.Category);
            Assert.Equal(11, knife.Price);
            Assert.Equal("skill.knife_thrust", knife.SkillId);

            SkillConfig pierce = ConfigFixture.Skill(config, "skill.thread_pierce");
            Assert.Equal(SkillBehavior.Pierce, pierce.Behavior);
            Assert.Equal(3, pierce.PierceCount);

            // Tags are a `list#sep=|` column, so this also covers list decoding.
            // `weapon` is the tag the tome's same-row synergy matches on, so every
            // arcane weapon must carry it.
            ItemConfig yant = ConfigFixture.Item(config, "item.kali_flame_yant");
            Assert.Equal(new[] { "yant", "weapon", "fire" }, yant.Tags);

            EnemyConfig boss = ConfigFixture.Enemy(config, "enemy.pob");
            Assert.True(boss.IsBoss);
            Assert.Equal(EnemyRole.Boss, boss.Role);
            Assert.Equal(50f, boss.BossPhase2HpPercent, 3);
        }

        [Fact]
        public void Footprints_are_parsed_from_the_picture_into_normalised_cell_sets()
        {
            IGameConfig config = ConfigFixture.Load();

            // "XXX|XXX|XXX"
            BagConfig cotton = ConfigFixture.Bag(config, "bag.raw_cotton_bag");
            Assert.Equal(9, cotton.Footprint.CellCount);
            Assert.Equal(3, cotton.Footprint.Width);
            Assert.Equal(3, cotton.Footprint.Height);
            Assert.True(cotton.Footprint.Contains(new CellOffset(2, 2)));

            // "X|X|X": a one-wide, three-tall pouch.
            BagConfig pouch = ConfigFixture.Bag(config, "bag.yant_cloth_pouch");
            Assert.Equal(3, pouch.Footprint.CellCount);
            Assert.Equal(1, pouch.Footprint.Width);
            Assert.Equal(3, pouch.Footprint.Height);

            // "X.|X.|XX": the L-tetromino the rotation rules need.
            BagConfig basket = ConfigFixture.Bag(config, "bag.wicker_basket_l");
            Assert.Equal(4, basket.Footprint.CellCount);
            Assert.Equal(2, basket.Footprint.Width);
            Assert.Equal(3, basket.Footprint.Height);
            Assert.True(basket.Footprint.Contains(new CellOffset(1, 2)));
            Assert.False(basket.Footprint.Contains(new CellOffset(1, 0)));

            // Items occupy board cells too: a 2x2 tome and a 2x1 familiar.
            ItemConfig tome = ConfigFixture.Item(config, "item.spell_tome");
            Assert.Equal(4, tome.Footprint.CellCount);
            Assert.Equal(2, tome.Footprint.Width);
            Assert.Equal(2, tome.Footprint.Height);

            ItemConfig familiar = ConfigFixture.Item(config, "item.kuman_thong");
            Assert.Equal(2, familiar.Footprint.CellCount);
            Assert.Equal(2, familiar.Footprint.Width);
            Assert.Equal(1, familiar.Footprint.Height);

            ItemConfig charm = ConfigFixture.Item(config, "item.phra_amulet");
            Assert.Equal(1, charm.Footprint.CellCount);
        }

        [Fact]
        public void Lookups_are_ordinal_and_miss_on_unknown_ids()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.True(config.TryGetBag("bag.yant_cloth_pouch", out _));
            Assert.False(config.TryGetBag("bag.YANT_CLOTH_POUCH", out _));
            Assert.False(config.TryGetBag(string.Empty, out _));
            Assert.False(config.TryGetItem("item.does_not_exist", out _));
            Assert.False(config.TryGetSkill("skill.does_not_exist", out _));
            Assert.False(config.TryGetEnemy("enemy.does_not_exist", out _));
            Assert.False(config.TryGetFloor("floor.does_not_exist", out _));
            Assert.False(config.TryGetLocalization("no.such.key", out _));
        }

        [Fact]
        public void Player_text_comes_from_the_localization_table()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.Equal("มีดหมอ", config.GetText("item.mor_knife.name", Language.Th));
            Assert.Equal("Mor Knife", config.GetText("item.mor_knife.name", Language.En));

            // A missing key is a data bug and must never be papered over with the
            // key itself (AGENTS rule 5).
            Assert.Throws<KeyNotFoundException>(() => config.GetText("ui.missing", Language.Th));
        }

        [Fact]
        public void Config_hash_matches_the_metadata_the_generator_wrote()
        {
            Dictionary<string, byte[]> tables = ConfigFixture.ReadTables();

            string computed = ConfigHash.Compute(tables);

            // ConfigBuildMetadata is written by tools/gen.ps1 (PowerShell, over the
            // generated .bytes); ConfigHash is the C# reader's implementation. Equal
            // here means the two agree, which is what makes the hash usable to
            // identify a build from a bug report.
            Assert.Equal(ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash, computed);
            Assert.Equal(computed, ConfigFixture.Load().ConfigHash);

            Assert.Equal(64, computed.Length);
            Assert.Equal(computed.ToUpperInvariant(), computed);
            Assert.Matches("^[0-9A-F]{64}$", computed);
        }

        [Fact]
        public void Build_metadata_names_the_generator_and_schema_revision()
        {
            Assert.Matches("^\\d+\\.\\d+\\.\\d+$", ProjectSurvive.Config.ConfigBuildMetadata.LubanVersion);
            Assert.StartsWith("5.1", ProjectSurvive.Config.ConfigBuildMetadata.LubanVersion, StringComparison.Ordinal);
            // Stage 11a added the charm/offering columns on item and the status table, so
            // the schema revision moved to 6 (Stage 07c had taken it to 5).
            Assert.Equal(6, ProjectSurvive.Config.ConfigBuildMetadata.SchemaVersion);
        }

        [Fact]
        public void Sample_tables_pass_the_data_validator()
        {
            // Same entry point the pipeline uses (tools/validate-data.ps1 and
            // tools/verify-generated.ps1 both shell out to this). Asserting it here
            // means a rule that regresses the shipped data fails `dotnet test`
            // too, not only a PowerShell run.
            int findings = ValidatorProgram.Validate(RepoLayout.TestDataDir, expectedHash: null, json: true);

            Assert.Equal(0, findings);
        }

        // -- Stage 04 content invariants ----------------------------------------

        [Fact]
        public void Every_piece_reaches_the_maximum_tier_through_the_merge_recipes()
        {
            IGameConfig config = ConfigFixture.Load();
            int maxTier = config.Constant.MaxTier;

            var pieces = config.Bags.Select(bag => bag.Id)
                .Concat(config.Items.Select(item => item.Id))
                .ToList();

            // The prototype is 3 bags + 11 items, and each of them must be able to
            // reach tier 3: a missing rung strands a player holding two tier-2 copies.
            Assert.Equal(14, pieces.Count);

            foreach (string id in pieces)
            {
                for (int tier = 1; tier < maxTier; tier++)
                {
                    Assert.Contains(config.MergeRecipes, recipe =>
                        recipe.TargetId == id && recipe.FromTier == tier && recipe.ToTier == tier + 1);
                }
            }
        }

        [Fact]
        public void Every_piece_is_offered_by_the_shop_pool()
        {
            IGameConfig config = ConfigFixture.Load();
            var offered = config.ShopPool
                .Where(entry => entry.Weight > 0)
                .Select(entry => entry.OfferedId)
                .ToHashSet(StringComparer.Ordinal);

            Assert.All(config.Bags, bag => Assert.Contains(bag.Id, offered));
            Assert.All(config.Items, item => Assert.Contains(item.Id, offered));
        }

        [Fact]
        public void Every_synergy_tag_is_carried_by_some_item()
        {
            // A synergy matches by tag, so a tag no item carries is a dead rule.
            IGameConfig config = ConfigFixture.Load();
            var vocabulary = config.Items.SelectMany(item => item.Tags).ToHashSet(StringComparer.Ordinal);

            foreach (SynergyConfig synergy in config.Synergies.Where(s => s.Kind != SynergyKind.BagArea))
            {
                Assert.Contains(synergy.TagA, vocabulary);
                Assert.Contains(synergy.TagB, vocabulary);
            }
        }

        [Fact]
        public void The_four_cast_behaviours_are_all_represented()
        {
            // The prototype must exercise projectile, pierce, aura and melee, or the
            // battle code paths for one of them would ship untested.
            IGameConfig config = ConfigFixture.Load();
            var behaviours = config.Skills.Select(skill => skill.Behavior).ToHashSet();

            Assert.Contains(SkillBehavior.Projectile, behaviours);
            Assert.Contains(SkillBehavior.Pierce, behaviours);
            Assert.Contains(SkillBehavior.Aura, behaviours);
            Assert.Contains(SkillBehavior.Melee, behaviours);

            // And the offering/familiar/curse pieces the combo rules need.
            Assert.Contains(config.Items, item => item.Element == Element.Curse);
            Assert.Contains(config.Items, item => item.Tags.Contains("familiar"));
            Assert.Contains(config.Items, item => item.Tags.Contains("offering"));
        }

        [Fact]
        public void At_least_one_piece_shape_is_asymmetric_so_rotation_is_meaningful()
        {
            // Stage 04 asks for one asymmetric shape on purpose: if every piece were
            // a rectangle, "rotate 0/90/180/270" could never change the outcome and
            // the Stage 05 rule would be untestable.
            IGameConfig config = ConfigFixture.Load();

            Assert.True(
                config.Bags.Any(bag => IsChiral(bag.Footprint)) || config.Items.Any(item => IsChiral(item.Footprint)),
                "no bag or item shape is distinguishable from its mirror image");

            Assert.True(IsChiral(ConfigFixture.Bag(config, "bag.wicker_basket_l").Footprint));
            Assert.False(IsChiral(ConfigFixture.Bag(config, "bag.raw_cotton_bag").Footprint));
        }

        /// <summary>True when mirroring the shape left-to-right yields a different set.</summary>
        private static bool IsChiral(Footprint footprint)
        {
            var mirrored = new HashSet<CellOffset>();
            foreach (CellOffset cell in footprint.Cells)
            {
                mirrored.Add(new CellOffset(footprint.Width - 1 - cell.X, cell.Y));
            }

            return mirrored.Count != footprint.CellCount
                || footprint.Cells.Any(cell => !mirrored.Contains(cell));
        }

        // -- failure paths: the loader must throw, never invent a value --------

        [Fact]
        public void A_missing_table_is_a_typed_load_failure()
        {
            Dictionary<string, byte[]> tables = ConfigFixture.ReadTables();
            tables.Remove("tbitem");

            ConfigLoadException exception = Assert.Throws<ConfigLoadException>(() =>
                ConfigFixture.LoadWith(name => tables.ContainsKey(name) ? tables[name] : null!));

            Assert.Equal(ConfigError.TableMissing, exception.Code);
            Assert.Equal("tbitem", exception.Target);
        }

        [Fact]
        public void Table_bytes_that_are_not_a_table_are_a_typed_load_failure()
        {
            Dictionary<string, byte[]> tables = ConfigFixture.ReadTables();
            tables["tbitem"] = new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x01, 0x02, 0x03, 0x04 };

            ConfigLoadException exception = Assert.Throws<ConfigLoadException>(() =>
                ConfigFixture.LoadWith(name => tables[name]));

            Assert.Equal(ConfigError.TableUnreadable, exception.Code);
        }

        [Fact]
        public void Loading_requires_a_hash_so_a_build_is_always_identifiable()
        {
            Dictionary<string, byte[]> tables = ConfigFixture.ReadTables();

            Assert.Throws<ArgumentException>(() => LubanGameConfig.Load(name => tables[name], string.Empty));
        }

        [Fact]
        public void Table_bytes_that_are_too_short_fail_loudly()
        {
            // A one-byte "table" must not read as an EMPTY table: an empty item
            // table would let the game start with no content at all, which is the
            // "stub instead of real config" failure the stage forbids.
            Exception? failure = Record.Exception(() => ConfigFixture.LoadWith(_ => new byte[] { 0x00 }));

            Assert.True(
                failure is ConfigLoadException,
                "expected ConfigLoadException, got " + (failure?.GetType().Name ?? "no exception"));
        }

        [Fact]
        public void Test_data_directory_is_where_the_generator_writes()
        {
            Assert.True(Directory.Exists(RepoLayout.TestDataDir), RepoLayout.TestDataDir);
            Assert.True(File.Exists(Path.Combine(RepoLayout.TestDataDir, "tbitem.bytes")));
            Assert.Equal(
                Path.Combine(RepoLayout.Root, "lib", "Tests~", "data"),
                RepoLayout.TestDataDir);
        }
    }
}
