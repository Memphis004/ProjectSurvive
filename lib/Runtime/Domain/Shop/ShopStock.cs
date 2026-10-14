using System;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Shop
{
    /// <summary>
    /// The camp shop's stock: a fixed number of slots, created <b>once</b> per camp
    /// entry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GDD v0.3: \"4 ช่องสินค้า ... สร้าง stock ครั้งเดียว\", \"เปิดร้านซ้ำสินค้าเดิม\",
    /// \"Refresh 5 เบี้ย เพิ่มครั้งละ 2 รีเซ็ตเมื่อเข้าศาลาใหม่\". The stock object
    /// therefore lives as long as the camp visit: opening the shop again is a read,
    /// and only <see cref="BeginCamp"/> (a new camp entry) replaces the offers.
    /// </para>
    /// <para>
    /// This type owns the <i>slots</i>; who may buy, and for how much, is the run's
    /// decision (<c>RunSession</c>, in Application), because paying involves the
    /// wallet and the chest as well. That split is what lets a purchase validate
    /// everything before touching anything (AGENTS rule 7).
    /// </para>
    /// <para>
    /// The four commit methods (<see cref="BeginCamp"/>, <see cref="MarkPurchased"/>,
    /// <see cref="MarkRefreshed"/> and <see cref="SetSlotLocked"/>) are therefore public
    /// but can never move money: the run calls them only after it has validated and
    /// paid. Every genuinely dangerous primitive — taking bia out of the purse —
    /// stays behind <c>Wallet.TrySpend</c>, which is itself atomic.
    /// </para>
    /// </remarks>
    public sealed class ShopStock
    {
        private readonly ShopSlot[] _slots;

        /// <summary>Creates an empty stock of <paramref name="slotCount"/> slots.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotCount"/> is negative.</exception>
        public ShopStock(int slotCount)
        {
            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "A shop cannot have negative slots.");
            }

            SlotCount = slotCount;
            _slots = new ShopSlot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new ShopSlot(i);
            }
        }

        /// <summary>Number of slots (<c>economy.shop_slot_count</c>, 4).</summary>
        public int SlotCount { get; }

        /// <summary>The slot at <paramref name="index"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The index is outside the panel.</exception>
        public ShopSlot this[int index]
        {
            get
            {
                if (index < 0 || index >= SlotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Shop slot out of range.");
                }

                return _slots[index];
            }
        }

        /// <summary>How many re-rolls happened in this camp (\"เพิ่มครั้งละ 2 ภายในศาลาเดียวกัน\").</summary>
        public int RefreshCount { get; private set; }

        /// <summary>True when every slot has been rented.</summary>
        public bool IsSoldOut
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].IsPurchased)
                    {
                        return false;
                    }
                }

                return SlotCount > 0;
            }
        }

        /// <summary>How many slots hold something the player can still rent.</summary>
        public int AvailableCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].IsEmpty && !_slots[i].IsPurchased)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// What the next re-roll costs: <c>economy.refresh_base_cost</c> plus
        /// <c>economy.refresh_cost_increment</c> per refresh already spent in this
        /// camp.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="economy"/> is null.</exception>
        public int RefreshCost(EconomyConfig economy)
        {
            if (economy is null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            return economy.RefreshBaseCost + (economy.RefreshCostIncrement * RefreshCount);
        }

        /// <summary>A plain, copyable picture of every slot (state hash, checkpoint).</summary>
        public ShopSlotState[] Capture()
        {
            var captured = new ShopSlotState[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                captured[i] = _slots[i].Capture();
            }

            return captured;
        }

        /// <summary>
        /// Starts a new camp visit: every slot is emptied and the re-roll counter is
        /// reset (\"รีเซ็ตเมื่อเข้าศาลาใหม่\"). The caller re-rolls immediately after.
        /// </summary>
        public void BeginCamp()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Clear();
                _slots[i].SetLocked(false);
            }

            RefreshCount = 0;

        }

        /// <summary>Records that one re-roll was paid for. Called by the run after the purse moved.</summary>
        public void MarkRefreshed() => RefreshCount++;

        /// <summary>Marks a slot as rented. Precondition: the run validated the purchase and paid.</summary>
        public void MarkPurchased(int index)
        {
            if (index >= 0 && index < SlotCount)
            {
                _slots[index].MarkPurchased();
            }
        }

        /// <summary>Pins or unpins one slot. Precondition: the run validated the request.</summary>
        public void SetSlotLocked(int index, bool locked)
        {
            if (index >= 0 && index < SlotCount)
            {
                _slots[index].SetLocked(locked);
            }
        }
    }
}
