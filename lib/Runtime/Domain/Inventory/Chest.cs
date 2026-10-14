using System;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// The 10-slot chest (หีบไม้สัก, <c>economy.chest_slots</c>): where a purchase
    /// lands and where a player parks a piece before laying it out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A slot holds <b>one piece</b> — a bag piece or an item. That is deliberate:
    /// the shop offers bags as well as items (<c>shop_pool.bag_id</c>), the GDD says a
    /// purchase goes to the chest first, and the GDD's "ย้ายหรือถอดย่ามได้" implies a
    /// bag that is not on the board has to live somewhere. One slot per piece also
    /// matches how the bag UI draws the chest.
    /// </para>
    /// <para>
    /// Contents give no bonus and never act (GDD: "ของในหีบไม่ให้โบนัสและไม่โจมตี"):
    /// nothing in <see cref="SynergyEvaluator"/> or <see cref="LoadoutResolver"/> reads
    /// a chest slot.
    /// </para>
    /// </remarks>
    public sealed class Chest
    {
        /// <summary>Returned by <see cref="FirstFreeSlot"/> when the chest is full.</summary>
        public const int Full = -1;

        private readonly PieceRef[] _slots;

        /// <summary>Creates an empty chest with <paramref name="slotCount"/> slots.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The slot count is negative.</exception>
        public Chest(int slotCount)
        {
            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "A chest cannot have negative slots.");
            }

            SlotCount = slotCount;
            _slots = new PieceRef[slotCount];
        }

        /// <summary>Number of slots (<c>economy.chest_slots</c>, 10).</summary>
        public int SlotCount { get; }

        /// <summary>The piece in <paramref name="slot"/>, or <see cref="PieceRef.None"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The slot is outside the chest.</exception>
        public PieceRef this[int slot]
        {
            get
            {
                if (slot < 0 || slot >= SlotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(slot));
                }

                return _slots[slot];
            }
        }

        /// <summary>How many slots hold a piece.</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].IsValid)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>True when nothing is in <paramref name="slot"/>.</summary>
        public bool IsEmpty(int slot) => slot >= 0 && slot < SlotCount && !_slots[slot].IsValid;

        /// <summary>True when every slot is taken.</summary>
        public bool IsFull => FirstFreeSlot() == Full;

        /// <summary>The lowest empty slot, or <see cref="Full"/>.</summary>
        public int FirstFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsValid)
                {
                    return i;
                }
            }

            return Full;
        }

        /// <summary>The slot holding <paramref name="piece"/>, or <see cref="Full"/>.</summary>
        public int SlotOf(PieceRef piece)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == piece)
                {
                    return i;
                }
            }

            return Full;
        }

        /// <summary>
        /// Puts a piece into the first free slot. Precondition: the chest is not full
        /// and the piece is not already in it.
        /// </summary>
        /// <returns>The slot used, or <see cref="Full"/> when there was no room.</returns>
        internal int Store(PieceRef piece)
        {
            int slot = FirstFreeSlot();
            if (slot == Full)
            {
                return Full;
            }

            _slots[slot] = piece;
            return slot;
        }

        /// <summary>Empties one slot.</summary>
        internal void Clear(int slot)
        {
            if (slot >= 0 && slot < SlotCount)
            {
                _slots[slot] = PieceRef.None;
            }
        }

        /// <summary>Empties every slot (restoring a snapshot writes the recorded slots back).</summary>
        internal void ClearAllSlots() => Array.Clear(_slots, 0, _slots.Length);

        /// <summary>Writes one recorded slot while restoring a snapshot.</summary>
        internal void SetSlot(int slot, PieceRef piece)
        {
            if (slot >= 0 && slot < SlotCount)
            {
                _slots[slot] = piece;
            }
        }

        /// <summary>Empties the slot holding <paramref name="piece"/>, if any.</summary>
        internal void ClearByPiece(PieceRef piece)
        {
            int slot = SlotOf(piece);
            if (slot != Full)
            {
                _slots[slot] = PieceRef.None;
            }
        }
    }
}
