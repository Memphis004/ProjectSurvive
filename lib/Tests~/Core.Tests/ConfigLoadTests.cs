using System;
using System.Collections.Generic;
using System.IO;
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

            Assert.Equal(3, config.Bags.Count);
            Assert.Equal(9, config.Items.Count);
            Assert.Equal(4, config.Skills.Count);
            Assert.Equal(5, config.Synergies.Count);
            Assert.Equal(8, config.MergeRecipes.Count);
            Assert.Equal(4, config.Enemies.Count);
            Assert.Equal(5, config.Floors.Count);
            Assert.Equal(9, config.Waves.Count);
            Assert.Equal(5, config.ShopPool.Count);
            Assert.Equal(36, config.Localization.Count);
        }

        [Fact]
        public void Singleton_rows_carry_the_authored_balance_numbers()
        {
            IGameConfig config = ConfigFixture.Load();

            // The board is 9x7 and the run starts with 15 bia (GDD v0.3).
            Assert.Equal(9, config.Constant.BoardWidth);
            Assert.Equal(7, config.Constant.BoardHeight);
            Assert.Equal(3, config.Constant.MaxTier);
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
            ItemConfig yant = ConfigFixture.Item(config, "item.kali_flame_yant");
            Assert.Equal(new[] { "yant", "fire" }, yant.Tags);

            EnemyConfig boss = ConfigFixture.Enemy(config, "enemy.pob");
            Assert.True(boss.IsBoss);
            Assert.Equal(EnemyRole.Boss, boss.Role);
            Assert.Equal(50f, boss.BossPhase2HpPercent, 3);
        }

        [Fact]
        public void Footprints_are_parsed_into_normalised_cell_sets()
        {
            IGameConfig config = ConfigFixture.Load();

            BagConfig cotton = ConfigFixture.Bag(config, "bag.raw_cotton_bag");
            Assert.Equal(9, cotton.Footprint.CellCount);
            Assert.Equal(3, cotton.Footprint.Width);
            Assert.Equal(3, cotton.Footprint.Height);
            Assert.True(cotton.Footprint.Contains(new CellOffset(2, 2)));

            // The asymmetric shape the rotation rules will need: L-tromino.
            BagConfig basket = ConfigFixture.Bag(config, "bag.wicker_basket_l");
            Assert.Equal(3, basket.Footprint.CellCount);
            Assert.Equal(2, basket.Footprint.Width);
            Assert.Equal(2, basket.Footprint.Height);
            Assert.False(basket.Footprint.Contains(new CellOffset(1, 1)));
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
            Assert.Equal(1, ProjectSurvive.Config.ConfigBuildMetadata.SchemaVersion);
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
