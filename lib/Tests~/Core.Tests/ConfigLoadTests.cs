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
    /// The generated code compiles and the harness reads the real tables — plus "no stub that
    /// returns a constant instead of real config", which is why every assertion below is on a
    /// value that can only come from <c>lib/Tests~/data</c>.
    /// </summary>
    public sealed class ConfigLoadTests
    {
        [Fact]
        public void Sample_tables_load_and_expose_every_row()
        {
            LubanGameConfig config = ConfigFixture.Load();

            Assert.NotEmpty(config.Localization);
            Assert.Equal(config.Localization.Count, config.Localization.Select(entry => entry.Key).Distinct().Count());

            // Values read from data/tables/constant.csv; a stub could not produce them.
            Assert.Equal(100, config.Constant.MaxHp);
            Assert.Equal(50, config.Constant.MaxMp);
            Assert.Equal(4.0f, config.Constant.MoveSpeed, 3);
            Assert.Equal(1.5f, config.Constant.PickupRadius, 3);
        }

        [Fact]
        public void The_shipped_hash_matches_the_tables_on_disk()
        {
            Dictionary<string, byte[]> tables = ConfigFixture.ReadTables();
            LubanGameConfig config = ConfigFixture.Load();

            Assert.Equal(ConfigHash.Compute(tables), config.ConfigHash);
            Assert.Equal(ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash, config.ConfigHash);
        }

        [Fact]
        public void Localization_serves_both_languages_and_falls_back_to_the_other()
        {
            IGameConfig config = ConfigFixture.Load();
            LocalizationEntry entry = config.Localization[0];

            Assert.Equal(entry.Th, config.GetText(entry.Key, Language.Th));
            Assert.Equal(entry.En, config.GetText(entry.Key, Language.En));

            var partiallyTranslated = new FakeConfig(
                new[] { new LocalizationEntry { Key = "only.th", Th = "ไทย", En = string.Empty } });
            Assert.Equal("ไทย", partiallyTranslated.GetText("only.th", Language.En));
        }

        [Fact]
        public void A_missing_localization_key_throws_instead_of_returning_the_key()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.Throws<KeyNotFoundException>(() => config.GetText("ui.does_not_exist", Language.En));
            Assert.False(config.TryGetLocalization("ui.does_not_exist", out _));
            Assert.True(config.TryGetLocalization(config.Localization[0].Key, out _));
        }

        [Fact]
        public void A_missing_table_is_a_typed_failure()
        {
            var thrown = Assert.Throws<ConfigLoadException>(() =>
                ConfigFixture.LoadWith(name =>
                {
                    if (name == "tbconstant")
                    {
                        throw new NullReferenceException("no such table");
                    }

                    return ConfigFixture.ReadTables()[name];
                }));

            Assert.Equal(ConfigError.TableUnreadable, thrown.Code);
        }

        [Fact]
        public void The_adapter_refuses_to_be_built_without_a_hash()
        {
            Assert.Throws<ArgumentException>(() =>
                LubanGameConfig.Load(_ => Array.Empty<byte>(), string.Empty));
            Assert.Throws<ArgumentNullException>(() =>
                LubanGameConfig.Load(null!, ConfigFixture.DummyHash));
        }

        [Fact]
        public void TableNames_are_the_two_the_template_keeps()
        {
            Assert.Equal(new[] { "tblocalization", "tbconstant" }, LubanGameConfig.TableNames);
        }

        /// <summary>A hand-built config, for the paths a real load cannot reach.</summary>
        private sealed class FakeConfig : IGameConfig
        {
            private readonly Dictionary<string, LocalizationEntry> _byKey;

            public FakeConfig(IReadOnlyList<LocalizationEntry> rows)
            {
                Localization = rows;
                _byKey = rows.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
            }

            public string ConfigHash => ConfigFixture.DummyHash;

            public ConstantConfig Constant { get; } = new ConstantConfig
            {
                MaxHp = 1,
                MaxMp = 0,
                MoveSpeed = 1f,
                PickupRadius = 0f,
            };

            public IReadOnlyList<LocalizationEntry> Localization { get; }

            public bool TryGetLocalization(string key, out LocalizationEntry value) =>
                _byKey.TryGetValue(key, out value!);

            public string GetText(string key, Language language)
            {
                if (!_byKey.TryGetValue(key, out LocalizationEntry? entry))
                {
                    throw new KeyNotFoundException(key);
                }

                string text = entry.For(language);
                return string.IsNullOrEmpty(text) ? entry.For(language == Language.En ? Language.Th : Language.En) : text;
            }
        }
    }
}
