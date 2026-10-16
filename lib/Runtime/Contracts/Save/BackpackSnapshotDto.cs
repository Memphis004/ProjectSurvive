using System.Collections.Generic;
using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// One piece of a backpack snapshot on the wire.
    /// </summary>
    /// <remarks>
    /// Every field is a primitive and every enum is carried as its <c>int</c> value, so
    /// this assembly keeps the "carries only DTOs, references no other layer" property
    /// that ADR 0001/0006 give it — the mapping to and from the domain snapshot is the
    /// Infrastructure adapter's job (<c>MessagePackBackpackSnapshotCodec</c>). Keys are
    /// explicit and ordered: a reordered property must not silently change the format.
    /// </remarks>
    [MessagePackObject]
    public sealed class BackpackPieceDto
    {
        /// <summary>0 none / 1 bag / 2 item, matching the domain <c>PieceKind</c>.</summary>
        [Key(0)]
        public int Kind { get; set; }

        /// <summary>Run-unique instance id.</summary>
        [Key(1)]
        public int InstanceId { get; set; }

        /// <summary>Permanent data id.</summary>
        [Key(2)]
        public string DefId { get; set; } = string.Empty;

        /// <summary>Tier at capture time.</summary>
        [Key(3)]
        public int Tier { get; set; }

        /// <summary>0 board / 1 chest, matching the domain <c>PieceZone</c>.</summary>
        [Key(4)]
        public int Zone { get; set; }

        /// <summary>Board column of the footprint origin; 0 for a chest piece.</summary>
        [Key(5)]
        public int OriginX { get; set; }

        /// <summary>Board row of the footprint origin; 0 for a chest piece.</summary>
        [Key(6)]
        public int OriginY { get; set; }

        /// <summary>0 / 1 / 2 / 3 quarter turns, matching the domain <c>Rotation</c>.</summary>
        [Key(7)]
        public int Rotation { get; set; }

        /// <summary>Chest slot, or -1 for a board piece.</summary>
        [Key(8)]
        public int ChestSlot { get; set; } = -1;

        /// <summary>
        /// The upgrade variants this item chose, one entry per completed merge step, in step
        /// order (Stage 09f). Empty for a bag, and an entry is the empty string for a step
        /// that had no chooser. On the wire as a plain string list so the DTO still carries
        /// only primitives.
        /// </summary>
        [Key(9)]
        public List<string> VariantIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// The serializable form of a whole backpack: the unit of undo and of a future
    /// checkpoint's bag section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not the save format yet — Stage 12 owns the checkpoint envelope
    /// (schema version, migration, config hash). This DTO exists now because Stage 05's
    /// undo needs a real serialization boundary instead of a second live object graph.
    /// </para>
    /// <para>
    /// The board's occupancy maps and the chest's contents are <b>not</b> stored: they
    /// are derived from <see cref="Pieces"/> plus the data tables, so a restored
    /// backpack cannot disagree with itself.
    /// </para>
    /// </remarks>
    [MessagePackObject]
    public sealed class BackpackSnapshotDto
    {
        /// <summary>
        /// Bumped whenever the shape below changes. <c>2</c> added
        /// <see cref="MergeCount"/> and <see cref="BackpackPieceDto.VariantIds"/> (Stage 09f).
        /// </summary>
        [Key(0)]
        public int SchemaVersion { get; set; } = 2;

        /// <summary>The instance-id counter, so a restored backpack does not reuse ids.</summary>
        [Key(1)]
        public int NextInstanceId { get; set; } = 1;

        /// <summary>Every owned piece, bags first then items, each in creation order.</summary>
        [Key(2)]
        public List<BackpackPieceDto> Pieces { get; set; } = new List<BackpackPieceDto>();

        /// <summary>
        /// How many merges the run had confirmed when this snapshot was taken. Undoable
        /// state: the upgrade offer is drawn from it (Stage 09f, ADR 0021).
        /// </summary>
        [Key(3)]
        public int MergeCount { get; set; }
    }
}
