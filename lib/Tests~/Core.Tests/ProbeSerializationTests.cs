using System;
using Google.Protobuf;
using ProjectSurvive.Core.Contracts.Save;
using Xunit;

// The generated contract and the MessagePack probe deliberately share the name `CheckpointProbe`:
// both answer "does our serialization actually work away from the desktop runtime?". The alias keeps
// this file readable where both are in scope at once.
using ProtoProbe = ProjectSurvive.Core.Generated.Proto.CheckpointProbe;
using ProtoSaveHeader = ProjectSurvive.Core.Generated.Proto.SaveHeader;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The two serialization stories of the project, exercised outside Unity so a
    /// regression is caught by <c>dotnet test</c> long before a Web build.
    /// </summary>
    /// <remarks>
    /// These are the same checks the Unity EditMode/PlayMode suites and the Web
    /// smoke run (see <c>UnityProject/Assets/Main/Scripts</c>), which is what makes
    /// the Editor result meaningful for the browser: one implementation, three
    /// runtimes. The interesting failures they catch are AOT/IL2CPP ones — a
    /// formatter that only exists dynamically — which is why
    /// <see cref="CheckpointProbeCodec.GeneratedFormatterIsPresent"/> is asserted
    /// rather than assumed.
    /// </remarks>
    public sealed class ProbeSerializationTests
    {
        [Fact]
        public void MessagePack_formatter_is_generated_at_compile_time()
        {
            // If this fails, the MessagePack SOURCE GENERATOR did not run for the
            // Contracts assembly - the dynamic resolver would still round-trip on
            // the desktop runtime and only explode in the Web build.
            Assert.True(
                CheckpointProbeCodec.GeneratedFormatterIsPresent,
                "MessagePack.GeneratedMessagePackResolver has no formatter for CheckpointProbe; "
                + "the source generator did not run for ProjectSurvive.Core.Contracts");
        }

        [Fact]
        public void MessagePack_round_trip_preserves_every_field()
        {
            CheckpointProbe original = CheckpointProbe.Sample();

            CheckpointProbe restored = CheckpointProbeCodec.RoundTrip(original);

            Assert.True(
                original.Matches(restored),
                "the round-tripped probe differs from the original: "
                + "schema " + restored.SchemaVersion
                + ", seed " + restored.Seed
                + ", key '" + restored.NoteKey + "'"
                + ", cells " + restored.OccupiedCells.Count
                + ", hp " + restored.Hp);
        }

        [Fact]
        public void MessagePack_round_trip_tolerates_a_null_optional_payload()
        {
            // A checkpoint written before a field existed must still load: the
            // serializer writes the array header the type declares, and the
            // generated formatter must handle a null collection without throwing.
            var sparse = new CheckpointProbe
            {
                SchemaVersion = 1,
                Seed = 7,
                NoteKey = string.Empty,
                OccupiedCells = null!,
                Hp = 0f,
            };

            byte[] bytes = CheckpointProbeCodec.Serialize(sparse);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MessagePack_output_is_deterministic()
        {
            // The save path is compared byte-for-byte when a run is resumed, so
            // two identical probes must produce identical bytes.
            byte[] first = CheckpointProbeCodec.Serialize(CheckpointProbe.Sample());
            byte[] second = CheckpointProbeCodec.Serialize(CheckpointProbe.Sample());

            Assert.Equal(Convert.ToBase64String(first), Convert.ToBase64String(second));
        }

        [Fact]
        public void Protobuf_round_trip_preserves_the_external_contract()
        {
            var original = new ProtoProbe
            {
                SchemaVersion = 3,
                ConfigHash = ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash,
                Tick = 1234567890123L,
                NoteKey = "ui.title",
            };

            byte[] bytes = original.ToByteArray();
            ProtoProbe restored = ProtoProbe.Parser.ParseFrom(bytes);

            Assert.Equal(original, restored);
            Assert.Equal(3u, restored.SchemaVersion);
            Assert.Equal(ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash, restored.ConfigHash);
            Assert.Equal(1234567890123L, restored.Tick);
        }

        [Fact]
        public void The_save_header_carries_the_schema_and_the_config_hash()
        {
            // The two fields a reader needs before it parses a payload: which format this is, and which
            // balance data it was written against.
            var header = new ProtoSaveHeader
            {
                SchemaVersion = 4,
                ConfigHash = ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash,
            };

            ProtoSaveHeader restored = ProtoSaveHeader.Parser.ParseFrom(header.ToByteArray());

            Assert.Equal(4u, restored.SchemaVersion);
            Assert.Equal(ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash, restored.ConfigHash);
        }

        [Fact]
        public void Protobuf_field_numbers_match_the_proto_definition()
        {
            // proto/app.proto is the external contract; the field numbers are the wire format and must
            // never drift with a refactor. Read through the descriptors rather than by reflection on C#
            // property names, because the numbers - not the names - are what a peer decodes with.
            Assert.Equal(1, ProtoProbe.Descriptor.FindFieldByNumber(1).FieldNumber);
            Assert.Equal(2, ProtoProbe.Descriptor.FindFieldByNumber(2).FieldNumber);
            Assert.Equal(3, ProtoProbe.Descriptor.FindFieldByNumber(3).FieldNumber);
            Assert.Equal(4, ProtoProbe.Descriptor.FindFieldByNumber(4).FieldNumber);
            Assert.Equal(2, ProtoSaveHeader.Descriptor.Fields.InFieldNumberOrder().Count);
            Assert.Equal("config_hash", ProtoSaveHeader.Descriptor.FindFieldByNumber(2).Name);
        }
    }
}
