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
    /// One row of the template's worked-example table.
    /// </summary>
    /// <remarks>
    /// This table exists to keep the data pipeline's two reference mechanisms testable in a project
    /// that has no game tables yet: <see cref="NameKey"/> is a required <c>#ref</c> (Luban rejects a
    /// dangling cell during generation) and <see cref="NoteKey"/> is an optional localization key
    /// (empty is legal, so <c>Data.Validator</c> owns the rule). A game deletes this table with its
    /// bean, its CSV and this type, and copies whichever mechanism its own columns need.
    /// </remarks>
    public sealed class SampleEntry
    {
        /// <summary>Stable id, e.g. <c>sample.alpha</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Localization key holding this row's name (a required reference).</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>A number the row carries; the range is enforced by the schema.</summary>
        public int Value { get; set; }

        /// <summary>An optional localization key, or an empty string when the row has no note.</summary>
        public string NoteKey { get; set; } = string.Empty;
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
