using System;
using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// One piece captured as plain values: everything needed to rebuild it, and
    /// nothing that points at live state.
    /// </summary>
    /// <remarks>
    /// The <see cref="Shape"/> is deliberately <b>not</b> stored: it is derived from
    /// <see cref="Rotation"/> and the data table, so storing it would be a second
    /// source of truth that a rotation bug could desync.
    /// </remarks>
    public sealed class PieceSnapshot
    {
        /// <summary>Bag or item.</summary>
        public PieceKind Kind { get; set; }

        /// <summary>Run-unique handle; restored so undo keeps addressing the same piece.</summary>
        public int InstanceId { get; set; }

        /// <summary>Permanent data id.</summary>
        public string DefId { get; set; } = string.Empty;

        /// <summary>Tier at capture time.</summary>
        public int Tier { get; set; }

        /// <summary>Board or chest.</summary>
        public PieceZone Zone { get; set; }

        /// <summary>Board column of the footprint origin; 0 for a chest piece.</summary>
        public int OriginX { get; set; }

        /// <summary>Board row of the footprint origin; 0 for a chest piece.</summary>
        public int OriginY { get; set; }

        /// <summary>Orientation.</summary>
        public Rotation Rotation { get; set; }

        /// <summary>Chest slot; -1 for a board piece.</summary>
        public int ChestSlot { get; set; } = PieceInstance.NoChestSlot;

        /// <summary>
        /// The upgrade variants this piece chose, one entry per completed merge step, empty
        /// for a bag (Stage 09f). Part of the snapshot because undo, reset and the run-state
        /// hash all have to see them: a restored tier-3 item must keep the two choices that
        /// made it, or a replay would rebuild a different loadout from the same commands.
        /// </summary>
        public IReadOnlyList<string> VariantIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// A complete, serializable picture of a <see cref="Backpack"/>: the unit of undo
    /// and of "กลับสภาพตอนเข้าศาลา".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain data only — no serializer attribute and no framework type — because
    /// <c>Domain</c> may not depend on a serializer (AGENTS rule 3). The bytes come
    /// from an <see cref="IBackpackSnapshotCodec"/> implemented in Infrastructure, so
    /// the rule holds and the snapshot is still a real MessagePack payload that
    /// Stage 12 can put in a checkpoint.
    /// </para>
    /// <para>
    /// <see cref="Pieces"/> is written bags-first-then-items, each in creation order:
    /// deterministic for a given command history, which is what makes two snapshots of
    /// the same history byte-identical.
    /// </para>
    /// </remarks>
    public sealed class BackpackSnapshot
    {
        /// <summary>
        /// Shape version of this snapshot, so a future format can migrate. <c>2</c> added
        /// <see cref="MergeCount"/> and <see cref="PieceSnapshot.VariantIds"/> (Stage 09f).
        /// </summary>
        public int SchemaVersion { get; set; } = 2;

        /// <summary>The id counter, so a restored backpack does not reuse ids.</summary>
        public int NextInstanceId { get; set; } = 1;

        /// <summary>
        /// How many merges the run has confirmed so far. Undoable state, not a statistic:
        /// the upgrade offer is drawn from <c>(run seed, this count, item id, tier step)</c>,
        /// so restoring it is what makes repeating an undone merge offer the same choices
        /// again instead of re-rolling them (ADR 0021).
        /// </summary>
        public int MergeCount { get; set; }

        /// <summary>Every owned piece, board and chest.</summary>
        public IReadOnlyList<PieceSnapshot> Pieces { get; set; } = new List<PieceSnapshot>();
    }

    /// <summary>
    /// Turns a <see cref="BackpackSnapshot"/> into bytes and back.
    /// </summary>
    /// <remarks>
    /// A port, for the same reason <c>IGameConfig</c> is one: the rule engine must not
    /// know how state is encoded, and the WebGL target must use MessagePack's
    /// source-generated formatter rather than its reflection resolver (AGENTS rule 3).
    /// The implementation lives in Infrastructure
    /// (<c>MessagePackBackpackSnapshotCodec</c>).
    /// </remarks>
    public interface IBackpackSnapshotCodec
    {
        /// <summary>Serializes a snapshot.</summary>
        byte[] Encode(BackpackSnapshot snapshot);

        /// <summary>Rebuilds a snapshot from <see cref="Encode"/> output.</summary>
        BackpackSnapshot Decode(byte[] bytes);
    }
}
