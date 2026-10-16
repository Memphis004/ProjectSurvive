using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Shop;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// Everything that makes one run different from another, as plain values — the
    /// thing a replay compares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GDD: \"replay จาก log ได้ RunState hash เดิม\". The hash is over this picture,
    /// so a field that matters to the outcome must be here: the phase, the floor,
    /// HP, bia, the bag (piece placement <i>and</i> instance ids, because a merge
    /// changes them), the shop's offers and flags, and the re-roll counter.
    /// </para>
    /// <para>
    /// It is a <b>dev / verification</b> tool, not a save format — Stage 12 builds
    /// its checkpoint from the DTOs instead, because this shape has no schema
    /// version. Ordering is fixed everywhere (pieces by creation order, slots by
    /// index), so two runs with equal state hash equal.
    /// </para>
    /// </remarks>
    public sealed class RunState
    {
        /// <summary>The flow phase.</summary>
        public RunFlowState FlowState { get; set; }

        /// <summary>1-based index of the floor the run is on (or about to attempt).</summary>
        public int FloorIndex { get; set; }

        /// <summary>Player HP carried between floors.</summary>
        public int Hp { get; set; }

        /// <summary>Player max HP (<c>constant.max_hp</c>).</summary>
        public int MaxHp { get; set; }

        /// <summary>Bia held.</summary>
        public int Bia { get; set; }

        /// <summary>How many floors have been cleared.</summary>
        public int FloorsCleared { get; set; }

        /// <summary>
        /// Ghosts the run has killed, over every floor it fought.
        /// </summary>
        /// <remarks>
        /// In the hash because the summary screen reports it ("ผีที่ปราบ") and it is the one run
        /// stat that comes from the battle rather than from a run command: a replay that killed a
        /// different number of ghosts has diverged, and this is where that shows up.
        /// </remarks>
        public int GhostsDefeated { get; set; }

        /// <summary>Id of the last settlement paid out.</summary>
        public int LastSettlementId { get; set; }

        /// <summary>Re-rolls paid for in the current camp.</summary>
        public int RefreshCount { get; set; }

        /// <summary>The backpack's revision — moves whenever a piece or a tier changed.</summary>
        public int BackpackRevision { get; set; }

        /// <summary>The backpack's next instance id.</summary>
        public int NextInstanceId { get; set; }

        /// <summary>
        /// How many merges the run has confirmed. In the hash because the upgrade offer is
        /// drawn from it: a replay that confirmed a different number of merges has to fail
        /// here rather than quietly resolve the same pieces into different variants.
        /// </summary>
        public int MergeCount { get; set; }

        /// <summary>Every shop slot, in panel order.</summary>
        public IReadOnlyList<ShopSlotState> ShopSlots { get; set; } = new List<ShopSlotState>();

        /// <summary>Every owned piece, bags first then items, in creation order.</summary>
        public IReadOnlyList<PieceSnapshot> Pieces { get; set; } = new List<PieceSnapshot>();

        /// <summary>
        /// A 32-bit hash of this state. Equal hashes mean the two runs agree
        /// field-for-field; a different hash means at least one of them differs
        /// somewhere above.
        /// </summary>
        public uint ComputeHash()
        {
            unchecked
            {
                uint hash = 2166136261u;

                hash = Mix(hash, (uint)FlowState);
                hash = Mix(hash, (uint)FloorIndex);
                hash = Mix(hash, (uint)Hp);
                hash = Mix(hash, (uint)MaxHp);
                hash = Mix(hash, (uint)Bia);
                hash = Mix(hash, (uint)FloorsCleared);
                hash = Mix(hash, (uint)GhostsDefeated);
                hash = Mix(hash, (uint)LastSettlementId);
                hash = Mix(hash, (uint)RefreshCount);
                hash = Mix(hash, (uint)BackpackRevision);
                hash = Mix(hash, (uint)NextInstanceId);
                hash = Mix(hash, (uint)MergeCount);
                hash = Mix(hash, (uint)ShopSlots.Count);
                hash = Mix(hash, (uint)Pieces.Count);

                for (int i = 0; i < ShopSlots.Count; i++)
                {
                    ShopSlotState slot = ShopSlots[i];
                    hash = Mix(hash, (uint)slot.Index);
                    hash = MixString(hash, slot.OfferedId);
                    hash = Mix(hash, (uint)slot.Kind);
                    hash = Mix(hash, (uint)slot.Price);
                    hash = Mix(hash, slot.IsPurchased ? 1u : 0u);
                    hash = Mix(hash, slot.IsLocked ? 1u : 0u);
                }

                for (int i = 0; i < Pieces.Count; i++)
                {
                    PieceSnapshot piece = Pieces[i];
                    hash = Mix(hash, (uint)piece.Kind);
                    hash = Mix(hash, (uint)piece.InstanceId);
                    hash = MixString(hash, piece.DefId);
                    hash = Mix(hash, (uint)piece.Tier);
                    hash = Mix(hash, (uint)piece.Zone);
                    hash = Mix(hash, (uint)piece.OriginX);
                    hash = Mix(hash, (uint)piece.OriginY);
                    hash = Mix(hash, (uint)piece.Rotation);
                    hash = Mix(hash, (uint)piece.ChestSlot);

                    // One per completed merge step, in step order: the upgrade choice changes
                    // the loadout without changing the piece's id, tier or placement, so the
                    // hash is the only place it would show up.
                    hash = Mix(hash, (uint)piece.VariantIds.Count);
                    for (int step = 0; step < piece.VariantIds.Count; step++)
                    {
                        hash = MixString(hash, piece.VariantIds[step]);
                    }
                }

                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString() =>
            FlowState + " floor " + FloorIndex + " hp " + Hp + "/" + MaxHp + " bia " + Bia
            + " cleared " + FloorsCleared + " kills " + GhostsDefeated
            + " pieces " + Pieces.Count + " merges " + MergeCount;

        private static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value & 0xFFu;
                hash *= 16777619u;
                hash ^= (value >> 8) & 0xFFu;
                hash *= 16777619u;
                hash ^= (value >> 16) & 0xFFu;
                hash *= 16777619u;
                hash ^= (value >> 24) & 0xFFu;
                hash *= 16777619u;
                return hash;
            }
        }

        private static uint MixString(uint hash, string value)
        {
            unchecked
            {
                hash = Mix(hash, (uint)value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    hash = Mix(hash, value[i]);
                }

                return hash;
            }
        }
    }
}
