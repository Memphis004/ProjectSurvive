using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// Serialization entry point for <see cref="BackpackSnapshotDto"/>, and the second
    /// place the project pins <b>how</b> MessagePack is used (the first is
    /// <see cref="CheckpointProbeCodec"/>).
    /// </summary>
    /// <remarks>
    /// MessagePack v3 generates the formatter for every <c>[MessagePackObject]</c> type at
    /// compile time, and <c>MessagePackSerializerOptions.Standard</c> already includes the
    /// source-generated resolver, so this path is AOT-safe under IL2CPP/WebGL without
    /// extra wiring. <c>DefaultOptions</c> is never mutated.
    /// </remarks>
    public static class BackpackSnapshotCodec
    {
        /// <summary>The formatter set used by every call here (the AOT-safe default).</summary>
        public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard;

        /// <summary>
        /// True when the source generator produced a formatter for
        /// <see cref="BackpackSnapshotDto"/> in this assembly at compile time.
        /// </summary>
        /// <remarks>
        /// "It round-tripped" is not by itself proof that the generated formatter was used:
        /// at runtime MessagePack can still fall back to <c>Reflection.Emit</c>, which
        /// throws on IL2CPP rather than failing a test. Asserting the formatter exists keeps
        /// that a measurable check.
        /// </remarks>
        public static bool GeneratedFormatterIsPresent =>
            MessagePack.GeneratedMessagePackResolver.Instance.GetFormatter<BackpackSnapshotDto>() is not null;

        public static byte[] Serialize(BackpackSnapshotDto snapshot) =>
            MessagePackSerializer.Serialize(snapshot, Options);

        public static BackpackSnapshotDto Deserialize(byte[] bytes) =>
            MessagePackSerializer.Deserialize<BackpackSnapshotDto>(bytes, Options);

        /// <summary>Serialize then deserialize; the value the tests compare.</summary>
        public static BackpackSnapshotDto RoundTrip(BackpackSnapshotDto snapshot) =>
            Deserialize(Serialize(snapshot));
    }
}
