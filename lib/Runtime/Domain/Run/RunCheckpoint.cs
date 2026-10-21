using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// One checkpoint of a run, as plain values — the state a reload resumes from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately <b>not</b> the wire format. The MessagePack DTO lives in
    /// <c>Contracts/Save</c>; an Infrastructure codec maps this value to it. Domain
    /// therefore stays serializer-free (AGENTS rule 3) while the checkpoint is still
    /// a real serialized payload.
    /// </para>
    /// <para>
    /// A checkpoint is always a <b>camp</b> checkpoint. While a floor is being fought
    /// the run's camp state has not changed yet (the battle writes HP, bia and
    /// consumables only when it is settled), so <c>RunSession.CaptureCheckpoint</c>
    /// hands over the camp state plus the seed of the floor that is about to start.
    /// That seed is what makes a reload during Battle re-fight the same battle
    /// instead of accepting or inventing an unfinished result.
    /// </para>
    /// <para>
    /// No ECS world, <c>IBattleSession</c>, GameObject or Unity type is reachable
    /// from here: every field is a primitive or a snapshot value, which is the
    /// Stage 12 acceptance condition stated as a type.
    /// </para>
    /// </remarks>
    public sealed class RunCheckpoint
    {
        /// <summary>Shape version of the checkpoint envelope (Stage 12 starts at 1).</summary>
        public int SchemaVersion { get; set; } = RunCheckpointFormat.SchemaVersion;

        /// <summary>The config hash this run was saved against.</summary>
        public string ConfigHash { get; set; } = string.Empty;

        /// <summary>The data schema version this run was saved against.</summary>
        public int ConfigSchemaVersion { get; set; }

        /// <summary>Stable id of this run, so a reload can be told apart from a new run.</summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>The run seed every stream was derived from.</summary>
        public uint Seed { get; set; }

        /// <summary>The three stream positions at the moment of the save.</summary>
        public RandomStreamsState Random { get; set; }

        /// <summary>1-based index of the floor about to be entered.</summary>
        public int FloorIndex { get; set; }

        /// <summary>HP carried into the floor.</summary>
        public int Hp { get; set; }

        /// <summary>Bia held at the camp checkout.</summary>
        public int Bia { get; set; }

        /// <summary>Floors cleared so far.</summary>
        public int FloorsCleared { get; set; }

        /// <summary>Ghosts defeated over the run.</summary>
        public int GhostsDefeated { get; set; }

        /// <summary>Id of the last settlement paid out.</summary>
        public int LastSettlementId { get; set; }

        /// <summary>Re-rolls paid for in this camp.</summary>
        public int RefreshCount { get; set; }

        /// <summary>
        /// The bag's revision counter. It is part of <see cref="RunState"/>'s hash (and the
        /// cache key the loadout is resolved against), so restoring it keeps a resumed run
        /// byte-identical to the one that was saved.
        /// </summary>
        public int BackpackRevision { get; set; }

        /// <summary>
        /// The battle seed for <see cref="FloorIndex"/> when the checkpoint was written at
        /// departure. <see cref="HasNextBattleSeed"/> says whether it is meaningful; a camp
        /// checkpoint written before departure has none and the run draws one normally.
        /// </summary>
        public uint NextBattleSeed { get; set; }

        /// <summary>True when <see cref="NextBattleSeed"/> is the seed of the coming floor.</summary>
        public bool HasNextBattleSeed { get; set; }

        /// <summary>The complete bag: its pieces, chest slots, merge count and variant ids.</summary>
        public BackpackSnapshot Backpack { get; set; } = new BackpackSnapshot();

        /// <summary>Every shop slot in panel order, including the rent and pin flags.</summary>
        public IReadOnlyList<ShopSlotState> ShopSlots { get; set; } = new List<ShopSlotState>();
    }

    /// <summary>
    /// Versions and failure codes of the checkpoint format (Stage 12).
    /// </summary>
    /// <remarks>
    /// The version is intentionally explicit rather than "the latest the code can read":
    /// a save written by a newer build must be refused with a reason, not half-read, and
    /// an older fixture must fail in a test rather than silently migrate a format that
    /// never shipped.
    /// </remarks>
    public static class RunCheckpointFormat
    {
        /// <summary>The only schema this build writes.</summary>
        public const int SchemaVersion = 1;

        /// <summary>The oldest schema this build can read. No migration has shipped yet.</summary>
        public const int MinimumSchemaVersion = 1;
    }

    /// <summary>Stable failure codes for checkpoint load/save (AGENTS rule 7).</summary>
    public static class CheckpointError
    {
        /// <summary>No checkpoint is stored for this slot.</summary>
        public const string NotFound = "save.not_found";

        /// <summary>The bytes are not a checkpoint (truncated, wrong format, or a bad field).</summary>
        public const string Corrupt = "save.corrupt";

        /// <summary>The save was written by a schema newer than this build supports.</summary>
        public const string SchemaTooNew = "save.schema_too_new";

        /// <summary>The save was written by a schema this build no longer supports.</summary>
        public const string SchemaTooOld = "save.schema_too_old";

        /// <summary>The save belongs to a different config hash or data schema.</summary>
        public const string ConfigMismatch = "save.config_mismatch";

        /// <summary>The state inside the save is not a state a run could have.</summary>
        public const string InvalidState = "save.invalid_state";

        /// <summary>The storage adapter cannot read or write (private mode, full quota, no IndexedDB).</summary>
        public const string StorageUnavailable = "save.storage_unavailable";

        /// <summary>The storage adapter was available but the write failed.</summary>
        public const string WriteFailed = "save.write_failed";

        /// <summary>The storage adapter was available but the delete failed.</summary>
        public const string DeleteFailed = "save.delete_failed";
    }

    /// <summary>
    /// Turns a <see cref="RunCheckpoint"/> into bytes and back.
    /// </summary>
    /// <remarks>
    /// A port, like <c>IBackpackSnapshotCodec</c>: Domain owns the plain value, and the
    /// MessagePack adapter lives in Infrastructure so no serializer type leaks into the
    /// rule engine.
    /// </remarks>
    public interface IRunCheckpointCodec
    {
        /// <summary>Serializes a checkpoint.</summary>
        byte[] Encode(RunCheckpoint checkpoint);

        /// <summary>Rebuilds a checkpoint from <see cref="Encode"/> output.</summary>
        /// <exception cref="System.Exception">The bytes are not a checkpoint.</exception>
        RunCheckpoint Decode(byte[] bytes);
    }
}
