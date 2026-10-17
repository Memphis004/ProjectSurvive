namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>One localization row: a stable key and its text per language.</summary>
    /// <remarks>
    /// Player-facing text lives only in the data table (AGENTS rule 5); code holds keys, never
    /// sentences. A key that is missing at runtime is a data bug and throws.
    /// </remarks>
    public sealed class LocalizationEntry
    {
        /// <summary>The stable key, e.g. <c>ui.title.start</c>.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Thai text.</summary>
        public string Th { get; set; } = string.Empty;

        /// <summary>English text.</summary>
        public string En { get; set; } = string.Empty;

        /// <summary>The text for a language.</summary>
        public string For(Language language) => language == Language.En ? En : Th;
    }

    /// <summary>
    /// The singleton row of tuned numbers every build needs.
    /// </summary>
    /// <remarks>
    /// The template keeps only numbers that stay meaningful whatever the game is: a starting
    /// pool of health and of a secondary resource, a movement speed and a pickup radius. A game
    /// adds its own fields here and in <c>data/tables/constant.csv</c> — numbers live in data,
    /// never in code (AGENTS rule 5).
    /// </remarks>
    public sealed class ConstantConfig
    {
        /// <summary>Starting maximum health.</summary>
        public int MaxHp { get; set; }

        /// <summary>Starting maximum of the secondary resource.</summary>
        public int MaxMp { get; set; }

        /// <summary>Base movement speed, units per second.</summary>
        public float MoveSpeed { get; set; }

        /// <summary>Radius within which a pickup is collected.</summary>
        public float PickupRadius { get; set; }
    }
}
