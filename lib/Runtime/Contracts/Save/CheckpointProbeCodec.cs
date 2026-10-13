using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// Serialization entry point for <see cref="CheckpointProbe"/>, and the place
    /// where the project pins <b>how</b> MessagePack is used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MessagePack v3 generates the formatter for every
    /// <c>[MessagePackObject]</c> type at compile time, and
    /// <c>MessagePackSerializerOptions.Standard</c> already includes
    /// <c>SourceGeneratedFormatterResolver</c>, which discovers that generated
    /// resolver through an assembly attribute. So the ordinary path is AOT-safe
    /// without extra wiring (MessagePack-CSharp v3 README, "AOT Code Generation"),
    /// and that is deliberately what this type uses: the fewer knobs, the fewer
    /// ways for the Web build to differ from the Editor.
    /// </para>
    /// <para>
    /// <see cref="GeneratedFormatterIsPresent"/> exists because "it round-tripped"
    /// is not by itself proof that the <i>generated</i> formatter was used: at
    /// runtime MessagePack can still fall back to <c>Reflection.Emit</c>, which
    /// throws on IL2CPP/WebGL rather than failing a test. Asserting the generated
    /// formatter exists turns "we hope the generator ran" into a measurable check
    /// in the Editor, in the Web build and in <c>dotnet test</c>.
    /// </para>
    /// <para>
    /// <c>DefaultOptions</c> is never mutated: it is global mutable state, and a
    /// library changing it behind the caller's back is a debugging trap.
    /// </para>
    /// </remarks>
    public static class CheckpointProbeCodec
    {
        /// <summary>The formatter set used by every call here (the AOT-safe default).</summary>
        public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard;

        /// <summary>
        /// True when the source generator produced a formatter for
        /// <see cref="CheckpointProbe"/> in this assembly at compile time.
        /// </summary>
        public static bool GeneratedFormatterIsPresent =>
            MessagePack.GeneratedMessagePackResolver.Instance.GetFormatter<CheckpointProbe>() is not null;

        public static byte[] Serialize(CheckpointProbe probe) =>
            MessagePackSerializer.Serialize(probe, Options);

        public static CheckpointProbe Deserialize(byte[] bytes) =>
            MessagePackSerializer.Deserialize<CheckpointProbe>(bytes, Options);

        /// <summary>Serialize then deserialize; the value the smoke tests compare.</summary>
        public static CheckpointProbe RoundTrip(CheckpointProbe probe) =>
            Deserialize(Serialize(probe));
    }
}
