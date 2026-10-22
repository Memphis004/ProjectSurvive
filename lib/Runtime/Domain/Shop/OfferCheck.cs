using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Core.Domain.Shop
{
    /// <summary>
    /// What the run decided about renting one slot, without doing it (Stage 09n).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The verdict <c>RunSession.Purchase</c> acts on, produced by the same predicate it asks: the
    /// shelf tooltip prints <see cref="RefusalCode"/> exactly as the button would refuse, because it is
    /// the same answer rather than a rule printed twice (AGENTS rules 7 and 10). A dry run never moves
    /// bia and never touches the stock — a caller may ask it as often as a pointer moves.
    /// </para>
    /// <para>
    /// <see cref="RefusalCode"/> being empty is the whole of "เช่าได้": an offer that is cheap enough
    /// and fits in the chest. A slot that holds nothing (rented, or never filled) is not described by
    /// this type at all — the run reports it as a failed result, the same way <c>Purchase</c> does.
    /// </para>
    /// </remarks>
    public readonly struct OfferCheck
    {
        /// <summary>Captures one verdict.</summary>
        public OfferCheck(string offeredId, PieceKind kind, int price, int tier, string refusalCode)
        {
            OfferedId = offeredId ?? string.Empty;
            Kind = kind;
            Price = price;
            Tier = tier;
            RefusalCode = refusalCode ?? string.Empty;
        }

        /// <summary>The id the slot offers.</summary>
        public string OfferedId { get; }

        /// <summary>Bag or item.</summary>
        public PieceKind Kind { get; }

        /// <summary>What the shelf asks, in bia (the slot's price, not a fresh table lookup).</summary>
        public int Price { get; }

        /// <summary>The tier the piece would arrive at (1 in shipped data).</summary>
        public int Tier { get; }

        /// <summary>The code <c>Purchase</c> would return, or empty when the rent would be accepted.</summary>
        public string RefusalCode { get; }

        /// <summary>True when the run would accept the rent now.</summary>
        public bool Rentable => RefusalCode.Length == 0;
    }
}
