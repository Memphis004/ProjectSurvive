using System;
using R3;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// Everything the Camp screen shows, as reactive values a View mirrors into widgets
    /// and models it re-renders from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10: R3 carries <b>UI-level state only</b>. There is no observable here
    /// that streams entity positions or a frame, and the two big things on this screen — the
    /// bag and the shop — are <i>replaced models</i> rather than streams: a command produces
    /// one new picture and every widget re-reads it. That is what makes "คำนวณใหม่เมื่อชุด
    /// อุปกรณ์เปลี่ยนเท่านั้น" true by construction, and it is why a drag can run for a
    /// thousand pointer moves without rebuilding anything.
    /// </para>
    /// <para>
    /// Both halves of a readout live here: the <i>caption</i>, which is a localization key's
    /// text, and the <i>value</i>, which <see cref="CampPresenter"/> formats. The View assigns
    /// and composes nothing (AGENTS rule 10, and the architecture test that forbids a
    /// presenter-side economy rule reads this file too).
    /// </para>
    /// </remarks>
    public sealed class CampUiState : IDisposable
    {
        // --- captions (localized) ---
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ShopCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> BagCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ChestCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> AltarCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> HintCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> NpcName { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> RentedLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> PinLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> UnpinLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> EmptySlotLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ResetLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> RotateLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> UndoLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> DepartLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> CloseLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> WeightCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> FocusCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ChestStatCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> BiaCaption { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> RefreshCostCaption { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The re-roll button's whole label, price included (composed by the presenter).</summary>
        public ReactiveProperty<string> RefreshButtonLabel { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> FloorCaption { get; } = new ReactiveProperty<string>(string.Empty);

        // --- values (formatted by the presenter, never by the view) ---
        public ReactiveProperty<string> WeightValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> FocusValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> ChestValue { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The chest panel's heading, the count folded into the localized format.</summary>
        public ReactiveProperty<string> ChestTitle { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> BiaValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> RefreshCostValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> FloorValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> HpValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> MpValue { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> HpMpValue { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// The last refusal or confirmation, localized from an error code
        /// (<c>error.&lt;code&gt;</c>). Written only when the code <i>changes</i>, so a drag
        /// across a wall of invalid cells builds one string, not one per pointer move.
        /// </summary>
        public ReactiveProperty<string> StatusLine { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The reason the depart button is disabled, or an empty string.</summary>
        public ReactiveProperty<string> DepartReason { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>What the altar would pay for the dragged piece, or an empty string.</summary>
        public ReactiveProperty<string> AltarOffer { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> LockLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The bag screen's forward control, "ไปชั้นถัดไป".</summary>
        public ReactiveProperty<string> NextFloorLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The lock-on-restore checkbox's caption, "ล็อกย่ามตอนคืนสภาพ".</summary>
        public ReactiveProperty<string> LockRestoreLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The instruction scroll's heading.</summary>
        public ReactiveProperty<string> InstructionsTitle { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The instruction scroll's four steps, in order.</summary>
        public ReactiveProperty<string> Instruction1 { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> Instruction2 { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> Instruction3 { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> Instruction4 { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// The tooltip, one field per <b>line</b>: ชื่อ, ระดับ, ค่าที่ใช้จริง, รุ่นย่อยที่เลือก,
        /// คำอธิบาย, คอมโบที่ทำงานอยู่.
        /// </summary>
        /// <remarks>
        /// One field per line rather than one string with newlines, because each line is a separate
        /// label the tooltip hides when it is empty (a bag has no คำอธิบาย, a charm has no upgrade
        /// variant) and because a label whose text was assembled by the view would be the view
        /// formatting, which AGENTS rule 10 forbids. A line may itself wrap onto several rows
        /// (ค่าที่ใช้จริง prints the resolved row over the base row), which is the presenter's
        /// decision and not the view's.
        /// </remarks>
        public ReactiveProperty<string> TooltipTitle { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>ระดับ <c>t/max</c> of the hovered piece.</summary>
        public ReactiveProperty<string> TooltipTier { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// The hovered piece's <b>resolved</b> ดาเมจ · คูลดาวน์ · สมาธิ over its table's base row, the
        /// cooldown floor when it decided the value, or a bag's usable slots.
        /// </summary>
        public ReactiveProperty<string> TooltipStatus { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// รุ่นย่อยที่เลือก: one line per completed merge step that kept a variant, or empty for a
        /// piece that has none (Stage 09h).
        /// </summary>
        public ReactiveProperty<string> TooltipUpgrades { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>The item's own description text; empty for a piece whose table has none.</summary>
        public ReactiveProperty<string> TooltipDescription { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>คอมโบที่ทำงานอยู่: the totals and the names of the rules that fired.</summary>
        public ReactiveProperty<string> TooltipCombos { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// ราคา and the rent verdict: the shelf's asking price with "เช่าได้" or the reason the run
        /// would refuse it (Stage 09n).
        /// </summary>
        /// <remarks>
        /// The reason is the code <c>RunSession.CheckPurchase</c> returned, localized through the
        /// same <c>error.&lt;code&gt;</c> rows the bottom bar uses — a tooltip cannot say "available"
        /// about an offer the rent button would refuse, because it does not decide that itself.
        /// </remarks>
        public ReactiveProperty<string> TooltipPrice { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>
        /// The shelf the player is standing at, written as the same readout the pointer's tooltip
        /// carries, so walking up to a station answers the same question as hovering it.
        /// </summary>
        /// <remarks>
        /// One line-broken string rather than the tooltip's fields: it is drawn on the shelf's own
        /// board, which is a single world label. Empty while no station is in reach.
        /// </remarks>
        public ReactiveProperty<string> FocusedShelfDetail { get; } = new ReactiveProperty<string>(string.Empty);

        // --- flags and bars ---
        public ReactiveProperty<bool> CanDepart { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> CanRefresh { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> CanReset { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> BagLocked { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> TooltipVisible { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<float> WeightBar { get; } = new ReactiveProperty<float>(0f);

        /// <summary>The shop panel, replaced when the stock or the purse changed.</summary>
        public ReactiveProperty<CampShopSlotModel[]> Shop { get; } =
            new ReactiveProperty<CampShopSlotModel[]>(Array.Empty<CampShopSlotModel>());

        /// <summary>The bag screen, replaced when a command moved the bag.</summary>
        public ReactiveProperty<CampBagScreen> Bag { get; } =
            new ReactiveProperty<CampBagScreen>(CampBagScreen.Empty);

        /// <summary>
        /// The drag in progress, assigned on every pointer move that changes something.
        /// </summary>
        /// <remarks>
        /// Its own channel rather than a field of <see cref="Bag"/>: publishing the whole bag
        /// screen per pointer move would rebuild an array sixty times a second, which is exactly
        /// the GC spike the stage forbids. A <c>CampDragState</c> is a value type with value
        /// equality, so republishing an unchanged drag costs one comparison and no allocation.
        /// </remarks>
        public ReactiveProperty<CampDragState> Drag { get; } =
            new ReactiveProperty<CampDragState>(CampDragState.None);

        /// <summary>
        /// The merge chooser (Stage 09g): the upgrade decision a merge of two equal pieces asks for,
        /// or <see cref="CampMergeChooserModel.Closed"/>.
        /// </summary>
        /// <remarks>
        /// A replaced model rather than a flag plus an array, for the same reason as <see cref="Bag"/>:
        /// a decision is one picture, and the view re-renders the whole modal from it. Closed is a
        /// shared constant, so shutting the chooser republishes one reference and allocates nothing.
        /// </remarks>
        public ReactiveProperty<CampMergeChooserModel> MergeChooser { get; } =
            new ReactiveProperty<CampMergeChooserModel>(CampMergeChooserModel.Closed);

        /// <summary>Assigns an int without republishing an unchanged value.</summary>
        public static void Set(ReactiveProperty<int> property, int value)
        {
            if (property.Value != value)
            {
                property.Value = value;
            }
        }

        /// <summary>Assigns a float without republishing an (approximately) unchanged value.</summary>
        public static void Set(ReactiveProperty<float> property, float value)
        {
            if (!UnityEngine.Mathf.Approximately(property.Value, value))
            {
                property.Value = value;
            }
        }

        /// <summary>Assigns a bool without republishing an unchanged value.</summary>
        public static void Set(ReactiveProperty<bool> property, bool value)
        {
            if (property.Value != value)
            {
                property.Value = value;
            }
        }

        /// <summary>Assigns a string without republishing an unchanged value.</summary>
        public static void Set(ReactiveProperty<string> property, string value)
        {
            string text = value ?? string.Empty;
            if (!string.Equals(property.Value, text, StringComparison.Ordinal))
            {
                property.Value = text;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Title.Dispose();
            ShopCaption.Dispose();
            BagCaption.Dispose();
            ChestCaption.Dispose();
            AltarCaption.Dispose();
            HintCaption.Dispose();
            NpcName.Dispose();
            RentedLabel.Dispose();
            PinLabel.Dispose();
            UnpinLabel.Dispose();
            EmptySlotLabel.Dispose();
            ResetLabel.Dispose();
            RotateLabel.Dispose();
            UndoLabel.Dispose();
            DepartLabel.Dispose();
            CloseLabel.Dispose();
            WeightCaption.Dispose();
            FocusCaption.Dispose();
            ChestStatCaption.Dispose();
            BiaCaption.Dispose();
            RefreshCostCaption.Dispose();
            RefreshButtonLabel.Dispose();
            FloorCaption.Dispose();
            WeightValue.Dispose();
            FocusValue.Dispose();
            ChestValue.Dispose();
            ChestTitle.Dispose();
            BiaValue.Dispose();
            RefreshCostValue.Dispose();
            FloorValue.Dispose();
            StatusLine.Dispose();
            DepartReason.Dispose();
            AltarOffer.Dispose();
            LockLabel.Dispose();
            NextFloorLabel.Dispose();
            LockRestoreLabel.Dispose();
            InstructionsTitle.Dispose();
            Instruction1.Dispose();
            Instruction2.Dispose();
            Instruction3.Dispose();
            Instruction4.Dispose();
            TooltipTitle.Dispose();
            TooltipTier.Dispose();
            TooltipStatus.Dispose();
            TooltipUpgrades.Dispose();
            TooltipDescription.Dispose();
            TooltipCombos.Dispose();
            TooltipPrice.Dispose();
            FocusedShelfDetail.Dispose();
            CanDepart.Dispose();
            CanRefresh.Dispose();
            CanReset.Dispose();
            BagLocked.Dispose();
            TooltipVisible.Dispose();
            WeightBar.Dispose();
            Shop.Dispose();
            Bag.Dispose();
            Drag.Dispose();
            MergeChooser.Dispose();
        }
    }
}
