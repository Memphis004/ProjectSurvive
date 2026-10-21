using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// Serialization entry point for <see cref="RunCheckpointDto"/>.
    /// </summary>
    /// <remarks>
    /// Same pin as the other codecs: MessagePack's source-generated resolver, never the
    /// reflection resolver, so the path is AOT-safe under IL2CPP/WebGL.
    /// </remarks>
    public static class RunCheckpointCodec
    {
        /// <summary>The formatter set used by every call here (the AOT-safe default).</summary>
        public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard;

        /// <summary>True when the source generator produced a formatter for the checkpoint.</summary>
        public static bool GeneratedFormatterIsPresent =>
            MessagePack.GeneratedMessagePackResolver.Instance.GetFormatter<RunCheckpointDto>() is not null;

        public static byte[] Serialize(RunCheckpointDto checkpoint) =>
            MessagePackSerializer.Serialize(checkpoint, Options);

        public static RunCheckpointDto Deserialize(byte[] bytes) =>
            MessagePackSerializer.Deserialize<RunCheckpointDto>(bytes, Options);
    }
}
