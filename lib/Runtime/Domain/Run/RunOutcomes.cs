using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>What one successful shop purchase did.</summary>
    public readonly struct PurchaseOutcome
    {
        /// <summary>Creates a purchase report.</summary>
        public PurchaseOutcome(int slotIndex, string offeredId, PieceKind kind, int price, int pulledBia, PieceRef piece)
        {
            SlotIndex = slotIndex;
            OfferedId = offeredId;
            Kind = kind;
            Price = price;
            Bia = pulledBia;
            Piece = piece;
        }

        /// <summary>Slot the piece was rented from.</summary>
        public int SlotIndex { get; }

        /// <summary>The rented piece's id.</summary>
        public string OfferedId { get; }

        /// <summary>Bag piece or item.</summary>
        public PieceKind Kind { get; }

        /// <summary>Bia paid.</summary>
        public int Price { get; }

        /// <summary>The wallet balance after the purchase.</summary>
        public int Bia { get; }

        /// <summary>The new instance, already parked in the chest (\"ของที่ซื้อเข้าหีบก่อนเสมอ\").</summary>
        public PieceRef Piece { get; }

        /// <inheritdoc />
        public override string ToString() =>
            "rent " + OfferedId + " (" + Price + " bia) -> " + Piece + ", purse " + Bia;
    }

    /// <summary>What one successful shop refresh did (เสี่ยงเซียมซี).</summary>
    public readonly struct RefreshOutcome
    {
        /// <summary>Creates a refresh report.</summary>
        public RefreshOutcome(int refreshCount, int cost, int filled, int bia)
        {
            RefreshCount = refreshCount;
            Cost = cost;
            Filled = filled;
            Bia = bia;
        }

        /// <summary>Re-rolls paid for in this camp, including this one.</summary>
        public int RefreshCount { get; }

        /// <summary>What this re-roll cost (<c>refresh_base_cost + increment * (count - 1)</c>).</summary>
        public int Cost { get; }

        /// <summary>How many slots were replaced. Pinned and rented slots are left alone.</summary>
        public int Filled { get; }

        /// <summary>The wallet balance after paying.</summary>
        public int Bia { get; }

        /// <inheritdoc />
        public override string ToString() =>
            "refresh #" + RefreshCount + " cost " + Cost + " filled " + Filled + ", purse " + Bia;
    }
}
