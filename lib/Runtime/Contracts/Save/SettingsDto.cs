using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// Player settings on the wire (Stage 12): master volume, the music and effects buses
    /// (Stage 13c-2a), language and the battle speed a new floor starts at.
    /// </summary>
    /// <remarks>
    /// Settings are not part of a run, so they have their own key and envelope. The
    /// language is carried as the domain enum's numeric value (0 Thai / 1 English), the
    /// same boundary the backpack DTO uses.
    /// </remarks>
    [MessagePackObject]
    public sealed class SettingsDto
    {
        /// <summary>Envelope shape version.</summary>
        [Key(0)] public int SchemaVersion { get; set; } = 1;

        /// <summary>Master audio volume, 0..1.</summary>
        [Key(1)] public float MasterVolume { get; set; } = 1f;

        /// <summary>1 Thai / 2 English, matching the domain <c>Language</c>.</summary>
        [Key(2)] public int Language { get; set; } = 1;

        /// <summary>The multiplier a battle starts at (1 or 2).</summary>
        [Key(3)] public float DefaultSpeed { get; set; } = 1f;

        /// <summary>
        /// True once the camp's first-run checklist has been finished (Stage 13b). A settings file
        /// written before the key existed decodes as <c>false</c>, so an existing player meets the
        /// checklist once — which is the point of it.
        /// </summary>
        [Key(4)] public bool TutorialCompleted { get; set; }

        /// <summary>
        /// The music bus, 0..1. Absent on a <see cref="SchemaVersion"/> 1 file, where MessagePack
        /// leaves it at 0 — which is why the codec reads these two through the schema version and
        /// not through the value (0 is a legal volume a player may choose).
        /// </summary>
        [Key(5)] public float MusicVolume { get; set; }

        /// <summary>The effects bus, 0..1. Absent on a schema 1 file, as above.</summary>
        [Key(6)] public float SfxVolume { get; set; }
    }

    /// <summary>Serialization entry point for <see cref="SettingsDto"/>.</summary>
    public static class SettingsCodec
    {
        /// <summary>The formatter set used by every call here (the AOT-safe default).</summary>
        public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard;

        /// <summary>True when the source generator produced a formatter for the settings.</summary>
        public static bool GeneratedFormatterIsPresent =>
            MessagePack.GeneratedMessagePackResolver.Instance.GetFormatter<SettingsDto>() is not null;

        public static byte[] Serialize(SettingsDto settings) =>
            MessagePackSerializer.Serialize(settings, Options);

        public static SettingsDto Deserialize(byte[] bytes) =>
            MessagePackSerializer.Deserialize<SettingsDto>(bytes, Options);
    }
}
