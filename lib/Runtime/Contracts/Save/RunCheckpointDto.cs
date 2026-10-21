using System.Collections.Generic;
using MessagePack;

namespace ProjectSurvive.Core.Contracts.Save
{
    /// <summary>
    /// One xoshiro128 position on the wire (Stage 12).
    /// </summary>
    [MessagePackObject]
    public sealed class RandomStreamStateDto
    {
        [Key(0)] public uint S0 { get; set; }
        [Key(1)] public uint S1 { get; set; }
        [Key(2)] public uint S2 { get; set; }
        [Key(3)] public uint S3 { get; set; }
    }

    /// <summary>
    /// One shop slot as it is persisted: the offer, its price and both flags.
    /// </summary>
    [MessagePackObject]
    public sealed class ShopSlotDto
    {
        [Key(0)] public int Index { get; set; }
        [Key(1)] public string OfferedId { get; set; } = string.Empty;

        /// <summary>0 none / 1 bag / 2 item, matching the domain <c>PieceKind</c>.</summary>
        [Key(2)] public int Kind { get; set; }

        [Key(3)] public int Price { get; set; }
        [Key(4)] public bool IsPurchased { get; set; }
        [Key(5)] public bool IsLocked { get; set; }
    }

    /// <summary>
    /// The Stage 12 checkpoint envelope, separate from Domain and from the ECS world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only plain values, snapshots and primitives: no <c>IBattleSession</c>, no ECS entity,
    /// no <c>GameObject</c> and no UnityEngine type appears here or anywhere reachable from
    /// here. The battle is represented by its <b>input</b> — the seed of the floor about to
    /// be fought — and never by its live state.
    /// </para>
    /// <para>
    /// Keys are explicit and ordered. Adding a field means appending a key and bumping
    /// <c>SchemaVersion</c>; a reordered property must not silently change the format.
    /// </para>
    /// </remarks>
    [MessagePackObject]
    public sealed class RunCheckpointDto
    {
        /// <summary>Envelope shape version.</summary>
        [Key(0)] public int SchemaVersion { get; set; }

        /// <summary>The config hash the run was saved against.</summary>
        [Key(1)] public string ConfigHash { get; set; } = string.Empty;

        /// <summary>The data schema version the run was saved against.</summary>
        [Key(2)] public int ConfigSchemaVersion { get; set; }

        /// <summary>Stable run id.</summary>
        [Key(3)] public string RunId { get; set; } = string.Empty;

        /// <summary>The run seed.</summary>
        [Key(4)] public uint Seed { get; set; }

        [Key(5)] public RandomStreamStateDto ShopRandom { get; set; } = new RandomStreamStateDto();
        [Key(6)] public RandomStreamStateDto BattleRandom { get; set; } = new RandomStreamStateDto();
        [Key(7)] public RandomStreamStateDto VisualRandom { get; set; } = new RandomStreamStateDto();

        /// <summary>1-based floor about to be entered.</summary>
        [Key(8)] public int FloorIndex { get; set; }

        [Key(9)] public int Hp { get; set; }
        [Key(10)] public int Bia { get; set; }
        [Key(11)] public int FloorsCleared { get; set; }
        [Key(12)] public int GhostsDefeated { get; set; }
        [Key(13)] public int LastSettlementId { get; set; }
        [Key(14)] public int RefreshCount { get; set; }

        /// <summary>The seed of the floor about to start, when the save was taken at departure.</summary>
        [Key(15)] public uint NextBattleSeed { get; set; }

        /// <summary>Whether <see cref="NextBattleSeed"/> is meaningful.</summary>
        [Key(16)] public bool HasNextBattleSeed { get; set; }

        /// <summary>The bag, including merge count and per-step variant ids (the one snapshot schema).</summary>
        [Key(17)] public BackpackSnapshotDto Backpack { get; set; } = new BackpackSnapshotDto();

        /// <summary>Every shop slot in panel order.</summary>
        [Key(18)] public List<ShopSlotDto> ShopSlots { get; set; } = new List<ShopSlotDto>();

        /// <summary>The bag revision at capture time, so a resumed run hashes identically.</summary>
        [Key(19)] public int BackpackRevision { get; set; }
    }
}
