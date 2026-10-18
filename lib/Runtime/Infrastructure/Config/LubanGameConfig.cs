using System;
using System.Collections.Generic;
using Luban;
using ProjectSurvive.Core.Domain.Config;
using GeneratedTables = ProjectSurvive.Config.Tables;

namespace ProjectSurvive.Core.Infrastructure.Config
{
    /// <summary>
    /// <see cref="IGameConfig"/> over the Luban-generated <c>ProjectSurvive.Config</c>
    /// tables. This is the single place where generated rows are read; everything
    /// upstream of it works with domain types (AGENTS rule 3, ADR 0006).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The adapter never invents a value. A table that cannot be read, a missing
    /// singleton row, or a duplicate localization key raises
    /// <see cref="ConfigLoadException"/> - there is no "return a constant instead"
    /// path, which is what the data gate forbids.
    /// </para>
    /// <para>
    /// It is engine-free on purpose: the caller supplies
    /// <paramref name="readTable"/>, which maps a Luban table name ("tblocalization") to
    /// its raw bytes. In Unity that comes from <c>Resources/Config/*.bytes</c>; in
    /// the dotnet harness and the data validator it comes from
    /// <c>lib/Tests~/data/</c> on disk. Nothing here touches
    /// <c>Application.streamingAssetsPath</c> (AGENTS rule 9).
    /// </para>
    /// </remarks>
    public sealed class LubanGameConfig : IGameConfig
    {
        /// <summary>Luban's generated loader key for each table, in report order.</summary>
        public static readonly string[] TableNames =
        {
            "tblocalization",
            "tbconstant",
            "tbsample",
        };

        private readonly Dictionary<string, LocalizationEntry> _localization =
            new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);

        private readonly List<LocalizationEntry> _localizationList = new List<LocalizationEntry>();

        private readonly List<SampleEntry> _samples = new List<SampleEntry>();

        private LubanGameConfig(string configHash, GeneratedTables tables)
        {
            ConfigHash = configHash;

            ProjectSurvive.Config.TbLocalization localization = tables.TbLocalization;
            foreach (ProjectSurvive.Config.Localization row in localization.DataList)
            {
                var entry = new LocalizationEntry { Key = row.Key, Th = row.Th, En = row.En };
                if (_localization.ContainsKey(entry.Key))
                {
                    // Luban's table index already makes a duplicate key an error;
                    // this guard exists so the adapter can never silently drop a
                    // row if that ever stops being true.
                    throw new ConfigLoadException(ConfigError.DuplicateLocalizationKey, "tblocalization",
                        "duplicate localization key '" + entry.Key + "'");
                }

                _localization.Add(entry.Key, entry);
                _localizationList.Add(entry);
            }

            ProjectSurvive.Config.TbConstant constant = tables.TbConstant;
            Constant = new ConstantConfig
            {
                MaxHp = constant.MaxHp,
                MaxMp = constant.MaxMp,
                MoveSpeed = constant.MoveSpeed,
                PickupRadius = constant.PickupRadius,
            };

            ProjectSurvive.Config.TbSample samples = tables.TbSample;
            foreach (ProjectSurvive.Config.Sample row in samples.DataList)
            {
                _samples.Add(new SampleEntry
                {
                    Id = row.Id,
                    NameKey = row.NameKey,
                    Value = row.Value,
                    NoteKey = row.NoteKey,
                });
            }
        }

        /// <inheritdoc />
        public string ConfigHash { get; }

        /// <inheritdoc />
        public ConstantConfig Constant { get; }

        /// <inheritdoc />
        public IReadOnlyList<LocalizationEntry> Localization => _localizationList;

        /// <inheritdoc />
        public IReadOnlyList<SampleEntry> Samples => _samples;

        /// <inheritdoc />
        public bool TryGetLocalization(string key, out LocalizationEntry value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = null!;
                return false;
            }

            return _localization.TryGetValue(key, out value!);
        }

        /// <inheritdoc />
        /// <exception cref="KeyNotFoundException">The key is not in the table.</exception>
        public string GetText(string key, Language language)
        {
            if (!_localization.TryGetValue(key, out LocalizationEntry? entry))
            {
                throw new KeyNotFoundException("no localization row for key '" + key + "'");
            }

            string text = entry.For(language);

            // An empty cell falls back to the other language rather than to the key: a build
            // missing one translation is still readable, while a build printing "ui.title" is not.
            return string.IsNullOrEmpty(text) ? entry.For(language == Language.En ? Language.Th : Language.En) : text;
        }

        /// <summary>
        /// Builds the config from raw table bytes.
        /// </summary>
        /// <param name="readTable">
        /// Maps a Luban table name (see <see cref="TableNames"/>) to its raw bytes.
        /// Must return non-null for every requested table.
        /// </param>
        /// <param name="configHash">SHA-256 over exactly those bytes; see tools/gen.ps1.</param>
        /// <exception cref="ConfigLoadException">
        /// A table is missing, the loader threw, or a mapped value is malformed.
        /// </exception>
        public static LubanGameConfig Load(Func<string, byte[]> readTable, string configHash)
        {
            if (readTable is null)
            {
                throw new ArgumentNullException(nameof(readTable));
            }

            if (string.IsNullOrEmpty(configHash))
            {
                throw new ArgumentException("configHash must be set", nameof(configHash));
            }

            GeneratedTables tables;
            try
            {
                tables = new GeneratedTables(name =>
                {
                    byte[]? bytes = readTable(name);
                    if (bytes is null)
                    {
                        throw new ConfigLoadException(ConfigError.TableMissing, name,
                            "table '" + name + "' was not provided");
                    }

                    return new ByteBuf(bytes);
                });
            }
            catch (ConfigLoadException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ConfigLoadException(ConfigError.TableUnreadable, "tables",
                    "Luban failed to read the tables: " + exception.Message, exception);
            }

            return new LubanGameConfig(configHash, tables);
        }
    }

    /// <summary>Machine-readable config failure codes.</summary>
    public static class ConfigError
    {
        /// <summary>A table the adapter asked for was not supplied.</summary>
        public const string TableMissing = "config.table_missing";

        /// <summary>A table was supplied but could not be parsed.</summary>
        public const string TableUnreadable = "config.table_unreadable";

        /// <summary>Two localization rows share a key.</summary>
        public const string DuplicateLocalizationKey = "localization.duplicate_key";
    }

    /// <summary>
    /// Raised when the generated config cannot be turned into a usable snapshot.
    /// <see cref="Code"/> is a stable identifier (never player text) so the data
    /// validator can report it without parsing a message.
    /// </summary>
    public sealed class ConfigLoadException : Exception
    {
        /// <summary>Creates a failure without an inner exception.</summary>
        public ConfigLoadException(string code, string target, string message)
            : base(message)
        {
            Code = code;
            Target = target;
        }

        /// <summary>Creates a failure that wraps the loader's own exception.</summary>
        public ConfigLoadException(string code, string target, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
            Target = target;
        }

        /// <summary>Stable failure identifier, e.g. <c>config.table_missing</c>.</summary>
        public string Code { get; }

        /// <summary>Where the failure is, e.g. <c>tblocalization</c>.</summary>
        public string Target { get; }
    }
}
