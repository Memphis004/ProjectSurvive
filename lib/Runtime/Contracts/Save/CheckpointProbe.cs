using System;
using System.Collections.Generic;
using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// The Stage 03 serialization probe: a small MessagePack DTO whose only job is
    /// to prove that the <b>source generator</b> — never the dynamic resolver —
    /// produces a formatter that round-trips under IL2CPP/AOT on WebGL2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MessagePack's dynamic (reflection + <c>Reflection.Emit</c>) resolver is not
    /// IL2CPP-compatible, so the project uses it through the generated formatter
    /// only (AGENTS rule 3, <c>docs/architecture/dependencies.md</c>). A DTO that
    /// only exists to be serialized is the cheapest way to keep that constraint
    /// testable: it is exercised by <c>dotnet test</c> (Net9), by the Unity
    /// EditMode/PlayMode smoke suite, and by the Web build itself.
    /// </para>
    /// <para>
    /// This is deliberately <b>not</b> the save format. The real
    /// <c>RunCheckpointDto</c> / <c>SettingsDto</c> arrive in Stage 12 with a
    /// schema version and a migration story; this type is expected to be deleted
    /// then. Its field types are chosen to cover what a checkpoint needs anyway:
    /// an integer schema version, an unsigned seed, a string, a list and a float.
    /// </para>
    /// </remarks>
    [MessagePackObject]
    public sealed class CheckpointProbe
    {
        /// <summary>Bumped whenever the shape below changes (Stage 12 owns the real one).</summary>
        [Key(0)]
        public int SchemaVersion { get; set; }

        /// <summary>The run seed, so a checkpoint can be replayed.</summary>
        [Key(1)]
        public uint Seed { get; set; }

        /// <summary>Player-facing text is never stored raw; a localization key is.</summary>
        [Key(2)]
        public string NoteKey { get; set; } = string.Empty;

        /// <summary>A variable-length payload, to exercise a collection formatter.</summary>
        [Key(3)]
        public List<int> OccupiedCells { get; set; } = new List<int>();

        /// <summary>Fractional state, to exercise a floating-point formatter.</summary>
        [Key(4)]
        public float Hp { get; set; }

        /// <summary>A probe with values that would betray a field-order or type bug.</summary>
        public static CheckpointProbe Sample() => new CheckpointProbe
        {
            SchemaVersion = 3,
            Seed = 0xDEADBEEFu,
            NoteKey = "ui.title",
            OccupiedCells = new List<int> { 0, 7, 14, 21 },
            Hp = 37.5f,
        };

        /// <summary>
        /// True when <paramref name="other"/> carries exactly the sample's values.
        /// Comparing field by field (instead of re-serializing) keeps a failure
        /// message readable about <i>which</i> field broke.
        /// </summary>
        public bool Matches(CheckpointProbe other)
        {
            if (other is null)
            {
                return false;
            }

            if (SchemaVersion != other.SchemaVersion
                || Seed != other.Seed
                || !string.Equals(NoteKey, other.NoteKey, StringComparison.Ordinal)
                || Math.Abs(Hp - other.Hp) > 0.0001f
                || OccupiedCells.Count != other.OccupiedCells.Count)
            {
                return false;
            }

            for (int i = 0; i < OccupiedCells.Count; i++)
            {
                if (OccupiedCells[i] != other.OccupiedCells[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
