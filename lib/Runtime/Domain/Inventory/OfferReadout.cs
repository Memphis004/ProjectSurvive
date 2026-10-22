using System;
using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// One combo an <b>offer</b> could take part in: the rule's name and the pieces it would pair
    /// with (Stage 09n).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A listing, not an evaluation. <see cref="SynergyEvaluator"/> answers "which rules act on the
    /// bag's layout now"; a piece standing on a shelf is not in a layout at all, so what a tooltip can
    /// honestly say is "this rule would pair this piece with one of these". Nothing here multiplies a
    /// stat: the numbers a player compares are the ones
    /// <see cref="PieceReadout"/> already carries.
    /// </para>
    /// <para>
    /// The partner is reported as the tag the rule names <b>and</b> the name keys of the pieces that
    /// carry it, so the presentation can print real names instead of the data vocabulary (AGENTS
    /// rule 5: no raw table value on screen). <see cref="PartnerCount"/> is how many pieces carry that
    /// tag in all, which is how a long list stays readable ("อีก 3 ชิ้น").
    /// </para>
    /// </remarks>
    public readonly struct OfferCombo
    {
        /// <summary>Captures one pairing.</summary>
        public OfferCombo(
            string ruleId, string nameKey, string partnerTag,
            IReadOnlyList<string> partnerNameKeys, int partnerCount)
        {
            RuleId = ruleId ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            PartnerTag = partnerTag ?? string.Empty;
            PartnerNameKeys = partnerNameKeys ?? Array.Empty<string>();
            PartnerCount = partnerCount < 0 ? 0 : partnerCount;
        }

        /// <summary>The <c>synergy</c> row's id.</summary>
        public string RuleId { get; }

        /// <summary>Localization key of the rule's own name.</summary>
        public string NameKey { get; }

        /// <summary>The tag of the rule that the offer does <b>not</b> carry — its partner.</summary>
        public string PartnerTag { get; }

        /// <summary>Name keys of the pieces carrying <see cref="PartnerTag"/> (the offer itself excluded).</summary>
        public IReadOnlyList<string> PartnerNameKeys { get; }

        /// <summary>How many pieces carry <see cref="PartnerTag"/> in all.</summary>
        public int PartnerCount { get; }

        /// <inheritdoc />
        public override string ToString() => RuleId + " with " + PartnerTag + " (" + PartnerCount + ")";
    }

    /// <summary>
    /// One piece offered on a shelf, explained <b>before</b> it is rented (Stage 09n).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same read model the จัดย่าม tooltip prints (<see cref="PieceReadout"/>), plus the three
    /// things only an offer has: what it costs, whether the run would accept the rent right now, and
    /// which combos it could join. Every number is resolved by <see cref="LoadoutResolver"/> on the
    /// same path a piece in the chest takes — an offer is a tier-1 piece that is not in a layout, so
    /// it has its own numbers and no combo (\"ของในหีบไม่ให้โบนัส\").
    /// </para>
    /// <para>
    /// <see cref="RefusalCode"/> is the code the run's own <c>Purchase</c> would return for the same
    /// slot at this instant: the presenter prints it rather than deciding anything, so the tooltip
    /// cannot say "เช่าได้" about an offer the button would refuse (AGENTS rule 7 and 10).
    /// </para>
    /// </remarks>
    public sealed class OfferReadout
    {
        /// <summary>Position in the panel, 0-based.</summary>
        public int SlotIndex { get; set; }

        /// <summary>Permanent data id (<c>item.takrut_tone</c> or <c>bag.raw_cotton_bag</c>).</summary>
        public string DefId { get; set; } = string.Empty;

        /// <summary>Bag or item.</summary>
        public PieceKind Kind { get; set; }

        /// <summary>What the shelf asks for it, in bia (the slot's own price, not the table's).</summary>
        public int Price { get; set; }

        /// <summary>The piece's weight from its table row (a bag's is what the shelf tooltip prints).</summary>
        public float Weight { get; set; }

        /// <summary>1..<c>constant.max_tier</c>; offered pieces are tier 1 in shipped data.</summary>
        public int Tier { get; set; }

        /// <summary>Localization key of the piece's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Localization key of its description, or empty when its table has none.</summary>
        public string DescriptionKey { get; set; } = string.Empty;

        /// <summary>The resolved numbers, exactly as a tier-1 copy in the chest would resolve.</summary>
        public PieceReadout Piece { get; set; } = null!;

        /// <summary>True when the run would accept the rent now.</summary>
        public bool Rentable { get; set; }

        /// <summary>The refusal <c>Purchase</c> would return, or empty when <see cref="Rentable"/>.</summary>
        public string RefusalCode { get; set; } = string.Empty;

        /// <summary>The combos this piece could join, in synergy-table order (empty for a bag).</summary>
        public IReadOnlyList<OfferCombo> Combos { get; set; } = Array.Empty<OfferCombo>();
    }
}
