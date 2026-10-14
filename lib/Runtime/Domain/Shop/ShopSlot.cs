using System;
using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Core.Domain.Shop
{
    /// <summary>
    /// One of the shop's four slots (GDD: \"แผงวัตถุมงคลยายจันทร์ — 4 ช่องสินค้า ...
    /// เช่าแล้วช่องแสดงว่าเช่าแล้วและไม่เติมสินค้าให้ฟรี\").
    /// </summary>
    /// <remarks>
    /// <para>
    /// A slot holds one offered piece, its <b>data</b> price, and two flags. The
    /// price is copied off the table when the stock is rolled rather than looked up
    /// again at purchase time, so what the player was shown is what they pay even if
    /// the data changes under them at runtime.
    /// </para>
    /// <para>
    /// Mutators are <c>internal</c>: only <see cref="ShopStock"/> and the run may
    /// move a slot, which is what keeps \"a rejected purchase changes nothing\"
    /// checkable (AGENTS rule 7).
    /// </para>
    /// </remarks>
    public sealed class ShopSlot
    {
        internal ShopSlot(int index)
        {
            Index = index;
        }

        /// <summary>Position in the panel, 0-based.</summary>
        public int Index { get; }

        /// <summary>Offered piece id, or the empty string when the slot holds nothing.</summary>
        public string OfferedId { get; private set; } = string.Empty;

        /// <summary>Whether <see cref="OfferedId"/> is a bag piece or an item.</summary>
        public PieceKind Kind { get; private set; } = PieceKind.None;

        /// <summary>Standard price in bia, straight from the item or bag table.</summary>
        public int Price { get; private set; }

        /// <summary>True once the slot was rented; such a slot never refills for free.</summary>
        public bool IsPurchased { get; private set; }

        /// <summary>True while the player has pinned the offer across a re-roll (\"ล็อกสินค้าที่อยากเก็บไว้ก่อนสุ่มได้\").</summary>
        public bool IsLocked { get; private set; }

        /// <summary>True when the slot holds nothing to rent.</summary>
        public bool IsEmpty => OfferedId.Length == 0;

        /// <summary>
        /// True when a re-roll may replace this slot: it must be neither pinned nor
        /// already rented.
        /// </summary>
        public bool CanBeRerolled => !IsLocked && !IsPurchased;

        /// <summary>A plain, copyable picture of this slot (state hash, checkpoint).</summary>
        public ShopSlotState Capture() =>
            new ShopSlotState(Index, OfferedId, Kind, Price, IsPurchased, IsLocked);

        internal void Fill(PieceKind kind, string offeredId, int price)
        {
            Kind = kind;
            OfferedId = offeredId ?? string.Empty;
            Price = price < 0 ? 0 : price;
        }

        /// <summary>
        /// Marks the slot as rented and takes the offer off it: a rented slot shows as
        /// rented and holds nothing more to rent (GDD: "เช่าแล้วช่องแสดงว่าเช่าแล้วและไม่
        /// เติมสินค้าให้ฟรี"). What was bought is carried by the purchase outcome and by
        /// the run's inventory, not by this slot.
        /// </summary>
        internal void MarkPurchased()
        {
            IsPurchased = true;
            Clear();
        }

        internal void SetLocked(bool locked) => IsLocked = locked;

        /// <summary>Empties the slot. Only a re-roll does this, and only for a slot that can be re-rolled.</summary>
        internal void Clear()
        {
            Kind = PieceKind.None;
            OfferedId = string.Empty;
            Price = 0;
        }
    }

    /// <summary>
    /// One shop slot as plain values — the part of the run state a hash and a
    /// checkpoint need.
    /// </summary>
    public readonly struct ShopSlotState
    {
        /// <summary>Captures a slot.</summary>
        public ShopSlotState(int index, string offeredId, PieceKind kind, int price, bool isPurchased, bool isLocked)
        {
            Index = index;
            OfferedId = offeredId ?? string.Empty;
            Kind = kind;
            Price = price;
            IsPurchased = isPurchased;
            IsLocked = isLocked;
        }

        /// <summary>Position in the panel, 0-based.</summary>
        public int Index { get; }

        /// <summary>Offered piece id, or empty.</summary>
        public string OfferedId { get; }

        /// <summary>Bag piece or item.</summary>
        public PieceKind Kind { get; }

        /// <summary>Standard price in bia.</summary>
        public int Price { get; }

        /// <summary>Rented already.</summary>
        public bool IsPurchased { get; }

        /// <summary>Pinned across a re-roll.</summary>
        public bool IsLocked { get; }

        /// <inheritdoc />
        public override string ToString() =>
            "#" + Index + " " + (OfferedId.Length == 0 ? "-" : OfferedId) + " "
            + Price + (IsPurchased ? " rented" : string.Empty) + (IsLocked ? " locked" : string.Empty);

        /// <summary>True when the two pictures describe the same slot state.</summary>
        public bool Equals(ShopSlotState other) =>
            Index == other.Index && Price == other.Price && Kind == other.Kind
            && IsPurchased == other.IsPurchased && IsLocked == other.IsLocked
            && string.Equals(OfferedId, other.OfferedId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ShopSlotState other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            (Index * 397) ^ (Kind.GetHashCode() * 31) ^ Price ^ (IsPurchased ? 7 : 0) ^ (IsLocked ? 11 : 0)
            ^ StringComparer.Ordinal.GetHashCode(OfferedId);

        public static bool operator ==(ShopSlotState left, ShopSlotState right) => left.Equals(right);

        public static bool operator !=(ShopSlotState left, ShopSlotState right) => !left.Equals(right);
    }
}
