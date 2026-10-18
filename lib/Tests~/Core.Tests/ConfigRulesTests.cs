using System;
using System.Collections.Generic;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Data.Validator;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// One test per rule in <see cref="ConfigValidator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each test starts from a valid snapshot and breaks exactly one thing, then asserts both
    /// halves of the contract: the rule fires, and it names the row it is about. Injecting one
    /// violation into a snapshot that is otherwise valid is what keeps a rule from passing
    /// because some unrelated row was wrong all along.
    /// </para>
    /// <para>
    /// The same <see cref="ConfigValidator"/> runs in the pipeline through
    /// tools/validate-data.ps1, so these tests are the fast, exact half of the evidence.
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

        // -- constant ----------------------------------------------------------

        [Fact]
        public void A_non_positive_health_pool_is_a_finding()
        {
            var config = new TestConfig();
            config.Constant = new ConstantConfig { MaxHp = 0, MaxMp = 50, MoveSpeed = 4f, PickupRadius = 1f };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.non_positive", finding.Code);
            Assert.Equal("tbconstant.max_hp", finding.Target);
        }

        [Fact]
        public void A_non_positive_move_speed_is_a_finding()
        {
            var config = new TestConfig();
            config.Constant = new ConstantConfig { MaxHp = 100, MaxMp = 50, MoveSpeed = 0f, PickupRadius = 1f };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.non_positive", finding.Code);
            Assert.Equal("tbconstant.move_speed", finding.Target);
        }

        [Fact]
        public void A_negative_secondary_pool_is_a_finding()
        {
            var config = new TestConfig();
            config.Constant = new ConstantConfig { MaxHp = 100, MaxMp = -1, MoveSpeed = 4f, PickupRadius = 1f };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.negative", finding.Code);
            Assert.Equal("tbconstant.max_mp", finding.Target);
        }

        [Fact]
        public void A_pickup_radius_ten_times_the_move_speed_is_a_finding()
        {
            var config = new TestConfig();
            config.Constant = new ConstantConfig { MaxHp = 100, MaxMp = 50, MoveSpeed = 1f, PickupRadius = 11f };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.out_of_range", finding.Code);
            Assert.Equal("tbconstant.pickup_radius", finding.Target);
        }

        // -- localization ------------------------------------------------------

        [Fact]
        public void A_duplicate_localization_key_is_a_finding()
        {
            var config = new TestConfig();
            config.Rows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.title", Th = "ไทย", En = "Title" },
                new LocalizationEntry { Key = "ui.title", Th = "ไทย 2", En = "Title 2" },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("localization.duplicate_key", finding.Code);
            Assert.Equal("tblocalization['ui.title']", finding.Target);
        }

        [Fact]
        public void A_blank_key_is_a_finding()
        {
            var config = new TestConfig();
            config.Rows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = string.Empty, Th = "ไทย", En = "English" },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("localization.blank_key", finding.Code);
        }

        [Fact]
        public void A_row_with_no_text_in_either_language_is_a_finding()
        {
            var config = new TestConfig();
            config.Rows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.empty", Th = string.Empty, En = string.Empty },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("localization.blank_text", finding.Code);
            Assert.Equal("tblocalization['ui.empty']", finding.Target);
        }

        [Fact]
        public void A_localization_row_missing_one_language_is_a_finding()
        {
            var config = new TestConfig();
            config.Rows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.half", Th = "ไทย", En = string.Empty },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("localization.missing_language", finding.Code);
            Assert.Equal("tblocalization['ui.half']", finding.Target);
        }

        // -- sample ------------------------------------------------------------

        [Fact]
        public void A_sample_row_pointing_at_a_missing_localization_key_is_a_finding()
        {
            var config = new TestConfig();
            config.SampleRows = new List<SampleEntry>
            {
                new SampleEntry { Id = "sample.one", NameKey = "ui.nothing.here", Value = 1 },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("sample.missing_name_key", finding.Code);
            Assert.Equal("tbsample['sample.one'].name_key", finding.Target);
        }

        [Fact]
        public void A_sample_row_with_an_optional_note_key_that_does_not_exist_is_a_finding()
        {
            var config = new TestConfig();
            config.SampleRows = new List<SampleEntry>
            {
                new SampleEntry { Id = "sample.one", NameKey = "ui.title", Value = 1, NoteKey = "ui.gone" },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("sample.missing_note_key", finding.Code);
            Assert.Equal("tbsample['sample.one'].note_key", finding.Target);
        }

        [Fact]
        public void An_empty_note_key_is_not_a_finding_because_the_column_is_optional()
        {
            var config = new TestConfig();
            config.SampleRows = new List<SampleEntry>
            {
                new SampleEntry { Id = "sample.one", NameKey = "ui.title", Value = 1, NoteKey = string.Empty },
            };

            Assert.Empty(ConfigValidator.Validate(config));
        }

        [Fact]
        public void A_duplicate_sample_id_is_a_finding()
        {
            var config = new TestConfig();
            config.SampleRows = new List<SampleEntry>
            {
                new SampleEntry { Id = "sample.one", NameKey = "ui.title", Value = 1 },
                new SampleEntry { Id = "sample.one", NameKey = "ui.title", Value = 2 },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("sample.duplicate_id", finding.Code);
            Assert.Equal("tbsample['sample.one']", finding.Target);
        }

        [Fact]
        public void A_blank_sample_id_is_a_finding()
        {
            var config = new TestConfig();
            config.SampleRows = new List<SampleEntry>
            {
                new SampleEntry { Id = "  ", NameKey = "ui.title", Value = 1 },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("sample.blank_id", finding.Code);
        }

        [Fact]
        public void An_empty_sample_table_is_one_finding_not_none()
        {
            var config = new TestConfig();
            config.SampleRows = new List<SampleEntry>();

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("sample.empty", finding.Code);
        }

        [Fact]
        public void An_empty_localization_table_is_one_finding_not_none()
        {
            var config = new TestConfig();
            config.Rows = new List<LocalizationEntry>();

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("localization.empty", finding.Code);
        }

        [Fact]
        public void Findings_are_ordered_so_two_runs_of_the_same_data_print_identically()
        {
            var config = new TestConfig();
            config.Rows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "b", Th = string.Empty, En = string.Empty },
                new LocalizationEntry { Key = "a", Th = string.Empty, En = string.Empty },
            };

            IReadOnlyList<Finding> first = ConfigValidator.Validate(config);
            IReadOnlyList<Finding> second = ConfigValidator.Validate(config);

            Assert.Equal(first.Select(finding => finding.Code + finding.Target),
                second.Select(finding => finding.Code + finding.Target));
            Assert.Equal("tblocalization['a']", first[0].Target);
        }

        /// <summary>A valid snapshot a test mutates one field at a time.</summary>
        private sealed class TestConfig : IGameConfig
        {
            public string ConfigHash => ConfigFixture.DummyHash;

            public ConstantConfig Constant { get; set; } = new ConstantConfig
            {
                MaxHp = 100,
                MaxMp = 50,
                MoveSpeed = 4f,
                PickupRadius = 1.5f,
            };

            public List<LocalizationEntry> Rows { get; set; } = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.title", Th = "ไทย", En = "Title" },
            };

            /// <summary>
            /// The sample rows, or a baseline row whose <c>name_key</c> resolves against
            /// <see cref="Rows"/>. A test that changes the localization table would otherwise make the
            /// baseline sample row dangle, and every one of these tests would fail for that reason
            /// instead of its own.
            /// </summary>
            public List<SampleEntry> SampleRows
            {
                get => _sampleRows ?? new List<SampleEntry>
                {
                    new SampleEntry
                    {
                        Id = "sample.one",
                        NameKey = Rows.FirstOrDefault(row => !string.IsNullOrEmpty(row.Key))?.Key ?? string.Empty,
                        Value = 1,
                    },
                };
                set => _sampleRows = value;
            }

            public IReadOnlyList<LocalizationEntry> Localization => Rows;

            public IReadOnlyList<SampleEntry> Samples => SampleRows;

            private List<SampleEntry>? _sampleRows;

            public bool TryGetLocalization(string key, out LocalizationEntry value) =>
                (value = Rows.FirstOrDefault(row => row.Key == key)!) is not null;

            public string GetText(string key, Language language)
            {
                LocalizationEntry? entry = Rows.FirstOrDefault(row => row.Key == key);
                if (entry is null)
                {
                    throw new KeyNotFoundException(key);
                }

                return entry.For(language);
            }
        }
    }
}
