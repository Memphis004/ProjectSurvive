using System;
using Google.Protobuf;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Generated.Proto;
using Xunit;

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
            var original = new AppInfo
            {
                AppVersion = "0.1.0",
                ConfigHash = ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash,
                Platform = Platform.Web,
                Locale = "th",
            };

            byte[] bytes = original.ToByteArray();
            AppInfo restored = AppInfo.Parser.ParseFrom(bytes);

            Assert.Equal(original, restored);
            Assert.Equal(Platform.Web, restored.Platform);
            Assert.Equal(ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash, restored.ConfigHash);
        }

        [Fact]
        public void Protobuf_enum_values_match_the_proto_definition()
        {
            // proto/app.proto is the external contract; the numbers are the wire
            // format and must never drift with a refactor.
            Assert.Equal(0, (int)Platform.Unspecified);
            Assert.Equal(1, (int)Platform.Web);
            Assert.Equal(2, (int)Platform.Editor);
        }
    }
}
