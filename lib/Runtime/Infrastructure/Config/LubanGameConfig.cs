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
    /// singleton row, or a malformed footprint all raise
    /// <see cref="ConfigLoadException"/> - there is no "return a constant instead"
    /// path, which is what the Stage 02 acceptance criterion forbids.
    /// </para>
    /// <para>
    /// It is engine-free on purpose: the caller supplies
    /// <paramref name="readTable"/>, which maps a Luban table name ("tbitem") to
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
            "tbbag",
            "tbskill",
            "tbstatus",
            "tbitem",
            "tbsynergy",
            "tbmergerecipe",
            "tbupgradevariant",
            "tbupgradevariantmodifier",
            "tbenemy",
            "tbfloor",
            "tbwave",
            "tbshoppool",
            "tbeconomy",
            "tbconstant",
        };

        private readonly Dictionary<string, BagConfig> _bags = new Dictionary<string, BagConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemConfig> _items = new Dictionary<string, ItemConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, SkillConfig> _skills = new Dictionary<string, SkillConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, StatusConfig> _statuses = new Dictionary<string, StatusConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, UpgradeVariantConfig> _upgradeVariants =
            new Dictionary<string, UpgradeVariantConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, EnemyConfig> _enemies = new Dictionary<string, EnemyConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, FloorConfig> _floors = new Dictionary<string, FloorConfig>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocalizationEntry> _localization = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);

        private readonly List<BagConfig> _bagList = new List<BagConfig>();
        private readonly List<ItemConfig> _itemList = new List<ItemConfig>();
        private readonly List<SkillConfig> _skillList = new List<SkillConfig>();
        private readonly List<StatusConfig> _statusList = new List<StatusConfig>();
        private readonly List<SynergyConfig> _synergyList = new List<SynergyConfig>();
        private readonly List<MergeRecipeConfig> _mergeRecipeList = new List<MergeRecipeConfig>();
        private readonly List<UpgradeVariantConfig> _upgradeVariantList = new List<UpgradeVariantConfig>();
        private readonly List<EnemyConfig> _enemyList = new List<EnemyConfig>();
        private readonly List<FloorConfig> _floorList = new List<FloorConfig>();
        private readonly List<WaveConfig> _waveList = new List<WaveConfig>();
        private readonly List<ShopPoolEntryConfig> _shopPoolList = new List<ShopPoolEntryConfig>();
        private readonly List<LocalizationEntry> _localizationList = new List<LocalizationEntry>();

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

            foreach (ProjectSurvive.Config.Bag row in tables.TbBag.DataList)
            {
                var bag = new BagConfig
                {
                    Id = row.Id,
                    NameKey = row.NameKey,
                    Tier = row.Tier,
                    Price = row.Price,
                    Weight = row.Weight,
                    Footprint = ParseFootprint("tbbag", row.Id, row.Footprint),
                };
                _bags.Add(bag.Id, bag);
                _bagList.Add(bag);
            }

            foreach (ProjectSurvive.Config.Skill row in tables.TbSkill.DataList)
            {
                var skill = new SkillConfig
                {
                    Id = row.Id,
                    NameKey = row.NameKey,
                    Behavior = (SkillBehavior)row.Behavior,
                    Element = (Element)row.Element,
                    CooldownSeconds = row.CooldownSeconds,
                    MpCost = row.MpCost,
                    Damage = row.Damage,
                    ProjectileSpeed = row.ProjectileSpeed,
                    Range = row.Range,
                    PierceCount = row.PierceCount,
                    ProjectileCount = row.ProjectileCount,
                };
                _skills.Add(skill.Id, skill);
                _skillList.Add(skill);
            }

            foreach (ProjectSurvive.Config.Status row in tables.TbStatus.DataList)
            {
                var status = new StatusConfig
                {
                    Id = row.Id,
                    Element = (Element)row.Element,
                    DamagePerTick = row.DamagePerTick,
                    TickIntervalSeconds = row.TickIntervalSeconds,
                    DurationSeconds = row.DurationSeconds,
                    MaxStacks = row.MaxStacks,
                };
                _statuses.Add(status.Id, status);
                _statusList.Add(status);
            }

            foreach (ProjectSurvive.Config.Item row in tables.TbItem.DataList)
            {
                var item = new ItemConfig
                {
                    Id = row.Id,
                    Category = (ItemCategory)row.Category,
                    NameKey = row.NameKey,
                    DescriptionKey = row.DescriptionKey,
                    Tier = row.Tier,
                    Element = (Element)row.Element,
                    Price = row.Price,
                    Weight = row.Weight,
                    SkillId = row.SkillId,
                    Tags = row.Tags.ToArray(),
                    Footprint = ParseFootprint("tbitem", row.Id, row.Footprint),
                    OfferingEffect = (OfferingEffect)row.OfferingEffect,
                    OfferingAmount = row.OfferingAmount,
                    OfferingUses = row.OfferingUses,
                    OfferingAutoHpPercent = row.OfferingAutoHpPercent,
                };

                // The charm columns are names, so a typo produces a finding that names the
                // row instead of a Luban parse failure (the same shape the upgrade rows use).
                if (!string.IsNullOrEmpty(row.CharmStat))
                {
                    if (!UpgradeVocabulary.TryParseStat(row.CharmStat, out UpgradeStat charmStat))
                    {
                        throw new ConfigLoadException(ConfigError.UpgradeStatUnknown,
                            "tbitem['" + row.Id + "'].charm_stat",
                            "stat '" + row.CharmStat + "' is not one the battle loadout carries ("
                            + string.Join(", ", UpgradeVocabulary.StatNames) + ")");
                    }

                    if (!UpgradeVocabulary.TryParseMethod(row.CharmMethod, out UpgradeMethod charmMethod))
                    {
                        throw new ConfigLoadException(ConfigError.UpgradeMethodUnknown,
                            "tbitem['" + row.Id + "'].charm_method",
                            "method '" + row.CharmMethod + "' is not known ("
                            + string.Join(", ", UpgradeVocabulary.MethodNames) + ")");
                    }

                    item.HasCharmEffect = true;
                    item.CharmStat = charmStat;
                    item.CharmMethod = charmMethod;
                    item.CharmMagnitude = row.CharmMagnitude;
                }

                _items.Add(item.Id, item);
                _itemList.Add(item);
            }

            foreach (ProjectSurvive.Config.Synergy row in tables.TbSynergy.DataList)
            {
                _synergyList.Add(new SynergyConfig
                {
                    Id = row.Id,
                    NameKey = row.NameKey,
                    Kind = (SynergyKind)row.Kind,
                    TagA = row.TagA,
                    TagB = row.TagB,
                    Stat = (SynergyStat)row.Stat,
                    ValuePercent = row.ValuePercent,
                    MaxStacks = row.MaxStacks,
                });
            }

            foreach (ProjectSurvive.Config.MergeRecipe row in tables.TbMergeRecipe.DataList)
            {
                _mergeRecipeList.Add(new MergeRecipeConfig
                {
                    Id = row.Id,
                    ItemId = row.ItemId,
                    BagId = row.BagId,
                    FromTier = row.FromTier,
                    ToTier = row.ToTier,
                });
            }

            // The variants first, so the modifier rows below have somewhere to attach. The
            // lists are held here while they fill: a variant's modifiers are one list, and
            // reading the DTO back is what a second copy of the same lookup would cost.
            var modifierLists = new Dictionary<string, List<UpgradeModifierConfig>>(StringComparer.Ordinal);

            foreach (ProjectSurvive.Config.UpgradeVariant row in tables.TbUpgradeVariant.DataList)
            {
                var modifiers = new List<UpgradeModifierConfig>();
                var variant = new UpgradeVariantConfig
                {
                    Id = row.Id,
                    Category = row.Category,
                    Tag = row.Tag,
                    FromTier = row.FromTier,
                    ToTier = row.ToTier,
                    NameKey = row.NameKey,
                    Weight = row.Weight,
                    Modifiers = modifiers,
                };
                _upgradeVariants.Add(variant.Id, variant);
                _upgradeVariantList.Add(variant);
                modifierLists.Add(variant.Id, modifiers);
            }

            foreach (ProjectSurvive.Config.UpgradeVariantModifier row in tables.TbUpgradeVariantModifier.DataList)
            {
                if (!modifierLists.TryGetValue(row.VariantId, out List<UpgradeModifierConfig>? modifiers))
                {
                    // Luban's `#ref` already rejects the dangling id under `--strict`;
                    // this guard exists so the adapter can never drop a modifier row if
                    // that ever stops being true (the same shape the localization guard has).
                    throw new ConfigLoadException(ConfigError.UpgradeModifierOrphan, "tbupgradevariantmodifier['" + row.Id + "']",
                        "variant '" + row.VariantId + "' does not exist");
                }

                if (!UpgradeVocabulary.TryParseStat(row.Stat, out UpgradeStat stat))
                {
                    throw new ConfigLoadException(ConfigError.UpgradeStatUnknown,
                        "tbupgradevariantmodifier['" + row.Id + "'].stat",
                        "stat '" + row.Stat + "' is not one the battle loadout carries ("
                        + string.Join(", ", UpgradeVocabulary.StatNames) + ")");
                }

                if (!UpgradeVocabulary.TryParseMethod(row.Method, out UpgradeMethod method))
                {
                    throw new ConfigLoadException(ConfigError.UpgradeMethodUnknown,
                        "tbupgradevariantmodifier['" + row.Id + "'].method",
                        "method '" + row.Method + "' is not known ("
                        + string.Join(", ", UpgradeVocabulary.MethodNames) + ")");
                }

                modifiers.Add(new UpgradeModifierConfig
                {
                    Id = row.Id,
                    Stat = stat,
                    Method = method,
                    Magnitude = row.Magnitude,
                });
            }

            foreach (ProjectSurvive.Config.Enemy row in tables.TbEnemy.DataList)
            {
                var enemy = new EnemyConfig
                {
                    Id = row.Id,
                    NameKey = row.NameKey,
                    Role = (EnemyRole)row.Role,
                    MaxHp = row.MaxHp,
                    MoveSpeed = row.MoveSpeed,
                    ContactDamage = row.ContactDamage,
                    AttackCooldownSeconds = row.AttackCooldownSeconds,
                    MovePattern = (MovePattern)row.MovePattern,
                    IsElite = row.IsElite,
                    IsBoss = row.IsBoss,
                    BossPhase2HpPercent = row.BossPhase2HpPercent,
                    DropValue = row.DropValue,
                    AttackPattern = (EnemyAttackPattern)row.AttackPattern,
                    AttackRange = row.AttackRange,
                    AttackTelegraphSeconds = row.AttackTelegraphSeconds,
                    AttackSeconds = row.AttackSeconds,
                    AttackDamage = row.AttackDamage,
                    AttackRadius = row.AttackRadius,
                    AttackLength = row.AttackLength,
                    FollowUpTelegraphSeconds = row.FollowUpTelegraphSeconds,
                    FollowUpSeconds = row.FollowUpSeconds,
                    FollowUpDamage = row.FollowUpDamage,
                    FollowUpRadius = row.FollowUpRadius,
                    FollowUpLength = row.FollowUpLength,
                    FollowUpTickSeconds = row.FollowUpTickSeconds,
                    RetreatSeconds = row.RetreatSeconds,
                    RetreatSpeed = row.RetreatSpeed,
                    BossPossessTelegraphSeconds = row.BossPossessTelegraphSeconds,
                    BossPossessSeconds = row.BossPossessSeconds,
                    BossPossessIntervalSeconds = row.BossPossessIntervalSeconds,
                    BossPossessRange = row.BossPossessRange,
                    BossCorpseRange = row.BossCorpseRange,
                    BossCorpseHeal = row.BossCorpseHeal,
                };
                _enemies.Add(enemy.Id, enemy);
                _enemyList.Add(enemy);
            }

            foreach (ProjectSurvive.Config.Floor row in tables.TbFloor.DataList)
            {
                var floor = new FloorConfig
                {
                    Id = row.Id,
                    Index = row.Index,
                    NameKey = row.NameKey,
                    DurationSeconds = row.DurationSeconds,
                    IsBoss = row.IsBoss,
                    BossEnemyId = row.BossEnemyId,
                };
                _floors.Add(floor.Id, floor);
                _floorList.Add(floor);
            }

            foreach (ProjectSurvive.Config.Wave row in tables.TbWave.DataList)
            {
                _waveList.Add(new WaveConfig
                {
                    Id = row.Id,
                    FloorId = row.FloorId,
                    EnemyId = row.EnemyId,
                    StartTimeSeconds = row.StartTimeSeconds,
                    EndTimeSeconds = row.EndTimeSeconds,
                    SpawnCount = row.SpawnCount,
                    SpawnIntervalSeconds = row.SpawnIntervalSeconds,
                    IsElite = row.IsElite,
                });
            }

            foreach (ProjectSurvive.Config.ShopPool row in tables.TbShopPool.DataList)
            {
                _shopPoolList.Add(new ShopPoolEntryConfig
                {
                    Id = row.Id,
                    FloorMin = row.FloorMin,
                    FloorMax = row.FloorMax,
                    ItemId = row.ItemId,
                    BagId = row.BagId,
                    Weight = row.Weight,
                });
            }

            ProjectSurvive.Config.TbConstant constant = tables.TbConstant;
            Constant = new ConstantConfig
            {
                BoardWidth = constant.BoardWidth,
                BoardHeight = constant.BoardHeight,
                MinCooldownSeconds = constant.MinCooldownSeconds,
                MaxSpeedPenaltyPercent = constant.MaxSpeedPenaltyPercent,
                WeightSpeedPercentPerUnit = constant.WeightSpeedPercentPerUnit,
                PickupRadius = constant.PickupRadius,
                MaxHp = constant.MaxHp,
                MaxMp = constant.MaxMp,
                FocusPerSecond = constant.FocusPerSecond,
                MoveSpeed = constant.MoveSpeed,
                MaxTier = constant.MaxTier,
                MagnetSpeed = constant.MagnetSpeed,
                SweepTimeoutSeconds = constant.SweepTimeoutSeconds,
                UpgradeChoiceCount = constant.UpgradeChoiceCount,
            };

            ProjectSurvive.Config.TbEconomy economy = tables.TbEconomy;
            Economy = new EconomyConfig
            {
                StartBia = economy.StartBia,
                ShopSlotCount = economy.ShopSlotCount,
                RefreshBaseCost = economy.RefreshBaseCost,
                RefreshCostIncrement = economy.RefreshCostIncrement,
                SellBackPercent = economy.SellBackPercent,
                HealPercentOnClear = economy.HealPercentOnClear,
                ChestSlots = economy.ChestSlots,
                ClearRewardBia = economy.ClearRewardBia,
            };
        }

        public string ConfigHash { get; }

        /// <summary>The data schema the generated tables were built from.</summary>
        public int SchemaVersion => ProjectSurvive.Config.ConfigBuildMetadata.SchemaVersion;

        public ConstantConfig Constant { get; }

        public EconomyConfig Economy { get; }

        public IReadOnlyList<BagConfig> Bags => _bagList;

        public IReadOnlyList<ItemConfig> Items => _itemList;

        public IReadOnlyList<SkillConfig> Skills => _skillList;

        public IReadOnlyList<StatusConfig> Statuses => _statusList;

        public IReadOnlyList<SynergyConfig> Synergies => _synergyList;

        public IReadOnlyList<MergeRecipeConfig> MergeRecipes => _mergeRecipeList;

        public IReadOnlyList<UpgradeVariantConfig> UpgradeVariants => _upgradeVariantList;

        public IReadOnlyList<EnemyConfig> Enemies => _enemyList;

        public IReadOnlyList<FloorConfig> Floors => _floorList;

        public IReadOnlyList<WaveConfig> Waves => _waveList;

        public IReadOnlyList<ShopPoolEntryConfig> ShopPool => _shopPoolList;

        public IReadOnlyList<LocalizationEntry> Localization => _localizationList;

        public bool TryGetBag(string id, out BagConfig value) => TryGet(_bags, id, out value);

        public bool TryGetItem(string id, out ItemConfig value) => TryGet(_items, id, out value);

        public bool TryGetSkill(string id, out SkillConfig value) => TryGet(_skills, id, out value);

        public bool TryGetStatus(string id, out StatusConfig value) => TryGet(_statuses, id, out value);

        public bool TryGetUpgradeVariant(string id, out UpgradeVariantConfig value) =>
            TryGet(_upgradeVariants, id, out value);

        public bool TryGetEnemy(string id, out EnemyConfig value) => TryGet(_enemies, id, out value);

        public bool TryGetFloor(string id, out FloorConfig value) => TryGet(_floors, id, out value);

        public bool TryGetLocalization(string key, out LocalizationEntry value) =>
            TryGet(_localization, key, out value);

        public string GetText(string key, Language language)
        {
            if (!_localization.TryGetValue(key, out LocalizationEntry entry))
            {
                throw new KeyNotFoundException("localization key '" + key + "' does not exist");
            }

            string text = entry.For(language);
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

        private static Footprint ParseFootprint(string table, string rowId, IReadOnlyList<string> rows)
        {
            if (Footprint.TryParse(rows, out Footprint footprint, out string? error))
            {
                return footprint;
            }

            throw new ConfigLoadException(error!, table + "['" + rowId + "'].footprint",
                "footprint is invalid (" + error + "): '" + string.Join("|", rows) + "'");
        }

        private static bool TryGet<T>(Dictionary<string, T> source, string id, out T value)
        {
            if (string.IsNullOrEmpty(id))
            {
                value = default!;
                return false;
            }

            return source.TryGetValue(id, out value!);
        }
    }

    /// <summary>Machine-readable config failure codes.</summary>
    public static class ConfigError
    {
        public const string TableMissing = "config.table_missing";
        public const string TableUnreadable = "config.table_unreadable";
        public const string DuplicateLocalizationKey = "localization.duplicate_key";
        public const string UpgradeStatUnknown = "upgrade_variant.unknown_stat";
        public const string UpgradeMethodUnknown = "upgrade_variant.unknown_method";
        public const string UpgradeModifierOrphan = "upgrade_variant.orphan_modifier";
    }

    /// <summary>
    /// Raised when the generated config cannot be turned into a usable snapshot.
    /// <see cref="Code"/> is a stable identifier (never player text) so the data
    /// validator can report it without parsing a message.
    /// </summary>
    public sealed class ConfigLoadException : Exception
    {
        public ConfigLoadException(string code, string target, string message)
            : base(message)
        {
            Code = code;
            Target = target;
        }

        public ConfigLoadException(string code, string target, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
            Target = target;
        }

        /// <summary>Stable failure identifier, e.g. <c>footprint.duplicate_cell</c>.</summary>
        public string Code { get; }

        /// <summary>Where the failure is, e.g. <c>tbbag['bag.x'].footprint</c>.</summary>
        public string Target { get; }
    }
}
