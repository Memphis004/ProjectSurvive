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

        public ReactiveProperty<string> TooltipTitle { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> TooltipStats { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<string> TooltipCombos { get; } = new ReactiveProperty<string>(string.Empty);

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
            TooltipStats.Dispose();
            TooltipCombos.Dispose();
            CanDepart.Dispose();
            CanRefresh.Dispose();
            CanReset.Dispose();
            BagLocked.Dispose();
            TooltipVisible.Dispose();
            WeightBar.Dispose();
            Shop.Dispose();
            Bag.Dispose();
            Drag.Dispose();
        }
    }
}
