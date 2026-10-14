namespace ProjectSurvive.Core.Domain.Shop
{
    /// <summary>
    /// The stable, machine-readable failure codes of the camp shop (AGENTS rule 7).
    /// </summary>
    /// <remarks>
    /// Player-facing text comes from localization, keyed <c>error.&lt;code&gt;</c>.
    /// A chest that is too full to take a purchase is reported with
    /// <see cref="ProjectSurvive.Core.Domain.Inventory.InventoryError.ChestFull"/>,
    /// not a second code of its own: it is the same condition, and the UI must show
    /// the same reason.
    /// </remarks>
    public static class ShopError
    {
        /// <summary>The slot index is outside the stock (\"4 ช่อง\" per camp entry).</summary>
        public const string UnknownSlot = "shop.unknown_slot";

        /// <summary>The slot holds nothing — it was already rented and never refills for free.</summary>
        public const string SlotEmpty = "shop.slot_empty";

        /// <summary>The slot was already rented (\"ซื้อช่องเดิมซ้ำไม่ได้\").</summary>
        public const string SlotPurchased = "shop.slot_purchased";

        /// <summary>The offered id is not in the item or bag table (a data bug, not a player mistake).</summary>
        public const string UnknownPiece = "shop.unknown_piece";
    }
}
