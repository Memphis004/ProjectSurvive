using System.Collections.Generic;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>Which layer of the screen a measured region belongs to.</summary>
    public enum UiRegionKind
    {
        /// <summary>A block that holds other things: the board, the chest panel, the altar, the status bar.</summary>
        Surface,

        /// <summary>A control a player presses.</summary>
        Control,

        /// <summary>
        /// Something drawn <b>on top of everything</b>, on a canvas above the screen's own: the piece
        /// tooltip. It may cover any region at all — that is what it is for — so the overlap rule does
        /// not apply to it; the rules that do are that it stays inside the canvas and that the canvas
        /// it lives on sorts above the screen's.
        /// </summary>
        Overlay,

        /// <summary>
        /// A control <b>inside</b> a modal: a card of the merge chooser and the window's own back
        /// button (Stage 09g).
        /// </summary>
        /// <remarks>
        /// Not a <see cref="Control"/> and not an <see cref="Overlay"/> either. A card sits on a modal
        /// that dims the whole screen, so it is expected to land on top of surfaces and controls that
        /// are not the modal at all — a rule that demanded it stand clear of the board it covers would
        /// fail every time. What it <i>must</i> not do is land on another card, because two cards on
        /// one another is the one collision a player cannot read past; that pair is reported by
        /// <see cref="IsAllowed"/>, and whether a card escaped the modal's own frame is reported by
        /// <see cref="FindModalEscapees"/>.
        /// </remarks>
        ModalControl,
    }

    /// <summary>One named screen rectangle, in pixels with the origin at the bottom-left (Unity's own convention).</summary>
    public readonly struct UiRegion
    {
        public UiRegion(string name, Rect rect, UiRegionKind kind)
            : this(name, rect, kind, 1f)
        {
        }

        /// <param name="alpha">
        /// The opacity of the region's own background, or 1 when it has none. Measured rather than
        /// assumed, so "the board cannot be read through" is a number in the report rather than a
        /// claim in a comment.
        /// </param>
        public UiRegion(string name, Rect rect, UiRegionKind kind, float alpha)
        {
            Name = name;
            Rect = rect;
            Kind = kind;
            Alpha = alpha;
        }

        public readonly string Name;
        public readonly Rect Rect;
        public readonly UiRegionKind Kind;

        /// <summary>The region's own background opacity (1 when it has no background of its own).</summary>
        public readonly float Alpha;

        public override string ToString() => Name + " " + Rect;
    }

    /// <summary>
    /// The จัดย่าม screen's layout rules as a predicate over named rectangles: two regions may share
    /// space only when one is drawn inside the other on purpose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Battle screen is judged by the same predicate (Stage 08c). Its surfaces are the panels
    /// and everything they hold — two bars, the timeline's track, the speed control, the mini ย่าม's
    /// grid, the dev menu's rows and the labels' own line boxes — is a control inside one of them, so
    /// the rule that answers "may these two share pixels" is asked of them exactly as it is asked of
    /// the จัดย่าม panel. Since Stage 08i that is five panels: the mini ย่าม came back at the bottom
    /// right after its 08g-2 removal, and its grid, caption and stacking rule came back with it. How
    /// much of the screen the five cover is `BattleHudArea`'s measure, and it is a budget rather
    /// than an overlap rule.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// This mirrors <c>CampBagDragPlayModeTests.IsAllowed</c> and its <c>NestedByDesign</c> pair, on
    /// purpose: the PlayMode test owns the rule for the panel the player sees, and the Editor capture
    /// command (<see cref="UiCaptureTool"/>) needs the same verdict without play mode, so that a
    /// layout can be judged from a shot taken in the Editor rather than from a WebGL build. Two
    /// copies of one rule is a drift risk; the mitigation is that both are checked against the same
    /// screen — the capture report prints every rectangle it measured, so a disagreement is visible
    /// in the artifact rather than silent.
    /// </para>
    /// <para>
    /// <see cref="UiRegionKind.Overlay"/> is the one kind the overlap rule never applies to: a
    /// tooltip drawn over the board is the point of it, and the merge chooser's frame and dim layer
    /// are overlays for exactly that reason. Everything an overlay <i>must</i> satisfy is checked
    /// where the canvas is known — <see cref="UiCaptureTool"/> measures it in the pixels of the shot
    /// it took. Inside a modal the rule turns back on for one pair only: two
    /// <see cref="UiRegionKind.ModalControl"/> regions may not share pixels, and none of them may
    /// leave the modal's frame (<see cref="FindModalEscapees"/>).
    /// </para>
    /// <para>
    /// The rules, in the words of the PlayMode test: a control drawn inside its own surface is by
    /// design (ไปชั้นถัดไป and กลับ live in the bottom-left block, the chest's slot grid lives in the
    /// chest panel). Everything else has to be disjoint — including two controls, where a button
    /// drawn inside another button would be a defect rather than nesting.
    /// </para>
    /// </remarks>
    public static class UiLayoutCheck
    {
        /// <summary>The chest panel's own background.</summary>
        public const string ChestPanel = "ChestPanel";

        /// <summary>The chest's slot grid, a child of <see cref="ChestPanel"/> on purpose.</summary>
        public const string Chest = "Chest";

        /// <summary>
        /// The Result screen's summary column: the surface the headline, the four lines and the two
        /// buttons are drawn on (Stage 10, panel added after the stage closed).
        /// </summary>
        public const string Summary = "Summary";

        /// <summary>The piece tooltip, on its own canvas above the screen.</summary>
        public const string Tooltip = "Tooltip";

        // ------------------------------------------------------------------
        // the battle HUD (Stage 08c)
        // ------------------------------------------------------------------

        /// <summary>
        /// The Battle screen's mini ย่าม: the run's bag drawn at the bottom right, at most 288 px wide
        /// and 216 px tall at 1080p (Stage 08i).
        /// </summary>
        public const string MiniBag = "MiniBagPanel";

        /// <summary>The floor's name and number, centred at the top.</summary>
        public const string FloorPanel = "FloorPanel";

        /// <summary>Time, bia, kills, the wave cursor and the speed control, at the top right.</summary>
        public const string ResourcePanel = "ResourcePanel";

        /// <summary>The figure and the two bars, at the bottom left.</summary>
        public const string StatusPanel = "StatusPanel";

        /// <summary>The floor's timeline, along the bottom between the status panel and the dev column.</summary>
        public const string TimelinePanel = "TimelinePanel";

        /// <summary>
        /// The boss bar. A band <b>inside</b> <see cref="ResourcePanel"/> rather than a panel of its
        /// own, which is why it is measured as a control: a second surface nested in a surface other
        /// than the chest's grid is the pair the rule rejects.
        /// </summary>
        public const string BossPanel = "BossPanel";

        /// <summary>The dev menu's column, bottom right, on its own canvas (a dev build only).</summary>
        public const string DevPanel = "DevPanel";

        /// <summary>The 1×/2× control, inside <see cref="ResourcePanel"/>.</summary>
        public const string SpeedButton = "SpeedButton";

        /// <summary>
        /// The HUD's หยุด control, in the band under the floor's name (Stage 13c-2b). A control inside
        /// <see cref="FloorPanel"/>, like the speed toggle is inside the resource panel.
        /// </summary>
        public const string PauseButton = "PauseButton";

        /// <summary>
        /// The pause overlay's card: the surface the heading, the warning and the two ways out are
        /// drawn on. It is an <see cref="UiRegionKind.Overlay"/> because it is meant to cover the HUD;
        /// the overlap rule therefore has nothing to say about it, and what it must satisfy is that it
        /// is opaque and that nothing inside it escapes its frame.
        /// </summary>
        public const string PausePanel = "PausePanel";

        /// <summary>
        /// The pause overlay's dim sheet, stretched over the whole canvas. It is the thing that takes
        /// the screen out of play, so it is an overlay as well and deliberately translucent.
        /// </summary>
        public const string PauseOverlayDim = "PauseOverlayDim";

        /// <summary>เล่นต่อ, inside <see cref="PausePanel"/>.</summary>
        public const string PauseResume = "PauseResume";

        /// <summary>กลับศาลา, inside <see cref="PausePanel"/>.</summary>
        public const string PauseLeave = "PauseLeave";

        /// <summary>The overlay's heading, inside <see cref="PausePanel"/>.</summary>
        public const string PauseTitle = "PauseTitle";

        /// <summary>The line that says a retreat forfeits the floor, inside <see cref="PausePanel"/>.</summary>
        public const string PauseWarning = "PauseWarning";

        /// <summary>The HP fill, inside <see cref="StatusPanel"/>.</summary>
        public const string HpBar = "HpBar";

        /// <summary>The MP fill, inside <see cref="StatusPanel"/>.</summary>
        public const string ManaBar = "ManaBar";

        /// <summary>
        /// The doctor's figure at the bottom left: a control inside <see cref="StatusPanel"/>, drawn
        /// from the Spine skeleton since Stage 17d. It is measured like the bars are, so "the picture
        /// does not land on the HP/MP rows" is a verdict rather than a look.
        /// </summary>
        public const string Portrait = "Portrait";

        /// <summary>The spawn-window track, inside <see cref="TimelinePanel"/>.</summary>
        public const string TimelineTrack = "TimelineTrack";

        /// <summary>The mini ย่าม's own grid area, inside <see cref="MiniBag"/>.</summary>
        public const string MiniBagContent = "MiniBagContent";

        /// <summary>
        /// The "ย่าม" caption at the top-left of <see cref="MiniBag"/>. The grid must start below it
        /// and share its left edge (Stage 08d) — a rule no overlap check can express, because the two
        /// boxes being disjoint is exactly what a grid floating in the middle of the panel also is.
        /// </summary>
        public const string BagCaption = "BagCaption";


        /// <summary>How far two edges may differ and still be called the same edge, in pixels.</summary>
        /// <remarks>
        /// The canvas scalar rounds each size's rectangles independently: at 1280×720 the panel's
        /// inset is 6.667 px and the content's is the same number written as a float, but a measured
        /// corner can come back a thousandth out. A half pixel is below what a player can see and
        /// above what the scalar's rounding produces.
        /// </remarks>
        public const float EdgeTolerance = 0.5f;

        /// <summary>
        /// The merge chooser's own frame — the modal panel the cards are drawn in (Stage 09g).
        /// </summary>
        /// <remarks>
        /// An overlay, like the tooltip, and for a stronger version of the same reason: a modal is a
        /// layer the player has to answer before anything behind it applies, so it is <i>supposed</i>
        /// to cover whatever panel it happens to land on. Reporting it as a collision with the panel
        /// under it would report the screen working as designed.
        /// </remarks>
        public const string MergeChooser = "MergeChooser";

        /// <summary>
        /// The merge chooser's dim layer: the translucent sheet that takes the board out of play while
        /// the decision is made. Also an overlay, and the one region that should cover the whole
        /// canvas.
        /// </summary>
        public const string MergeChooserDim = "MergeChooserDim";

        /// <summary>The chooser's back button, the control that closes the window without changing anything.</summary>
        public const string MergeChooserBack = "MergeChooserBack";

        /// <summary>
        /// One offer card's region id. A card is registered as one rectangle — its own background —
        /// rather than as a rectangle per label, because the collision that matters is card against
        /// card; the labels inside a card are that card's business.
        /// </summary>
        public static string MergeChooserCard(int index) => "MergeChooserCard" + index;

        /// <summary>
        /// The opacity a background has to reach for the world behind the overlay not to be read
        /// through it ("พาเนล Board และหีบทึบพอ (alpha ≥ 0.9) ไม่ให้ฉากด้านหลังโผล่ผ่านจนอ่านยาก").
        /// </summary>
        public const float OpaqueFloor = 0.9f;

        /// <summary>
        /// The regions the stage asks to be opaque, by the name they are measured under. The other
        /// panels are deliberately translucent — the จัดย่าม screen is an overlay on ศาลาพัก and the
        /// camp has to stay visible behind it.
        /// </summary>
        /// <remarks>
        /// <see cref="Summary"/> is the Result screen's column and is here for the reason the camp's
        /// two panels are: it is a panel of its own, authored at the camp panel's alpha, and the
        /// world behind it must not be read through it. The จัดย่าม overlay's own background (0.62) is
        /// the one panel that is translucent on purpose and is not listed.
        /// </remarks>
        /// <remarks>
        /// <see cref="PausePanel"/> is here for the strongest version of the same reason: a pause menu
        /// a player can read the frozen arena through is not a menu, and the player has to be able to
        /// read the line that says the retreat costs the floor.
        /// </remarks>
        public static readonly string[] OpaqueRegions = { "Board", ChestPanel, Summary, PausePanel };

        /// <summary>True when the two rectangles share any area at all.</summary>
        public static bool Overlaps(Rect a, Rect b) =>
            a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        /// <summary>True when <paramref name="outer"/> encloses <paramref name="inner"/> completely.</summary>
        public static bool Contains(Rect outer, Rect inner) =>
            outer.xMin <= inner.xMin && outer.yMin <= inner.yMin
            && outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;

        /// <summary>Whether two named regions may share space.</summary>
        public static bool IsAllowed(UiRegion a, UiRegion b)
        {
            if (!Overlaps(a.Rect, b.Rect))
            {
                return true;
            }

            if (a.Kind == UiRegionKind.Overlay || b.Kind == UiRegionKind.Overlay)
            {
                // An overlay is drawn above the screen on a canvas of its own; what it must satisfy is
                // checked separately (inside the canvas, on the higher canvas), not here. The merge
                // chooser's frame and its dim layer are overlays for this rule as well: a modal is
                // meant to cover the screen it is asking about.
                return true;
            }

            bool aModal = a.Kind == UiRegionKind.ModalControl;
            bool bModal = b.Kind == UiRegionKind.ModalControl;

            if (aModal && bModal)
            {
                // Two cards (or a card and the back button) drawn on each other: the one collision
                // inside a modal that has to be reported, because the player would have to read both.
                return false;
            }

            if (aModal || bModal)
            {
                // A card is drawn on the modal, and the modal dims everything behind it, so covering a
                // surface or a control underneath is the modal working rather than a defect. The one
                // thing a card may not do is escape the modal's own frame, which is a containment
                // question this method has no way to answer — FindModalEscapees answers it.
                return true;
            }

            if (a.Kind == UiRegionKind.Surface && b.Kind == UiRegionKind.Surface)
            {
                return IsNestedByDesign(a.Name, b.Name);
            }

            if (a.Kind == UiRegionKind.Surface || b.Kind == UiRegionKind.Surface)
            {
                // A control inside its surface is by design; a control half outside it is not.
                return Contains(a.Rect, b.Rect) || Contains(b.Rect, a.Rect);
            }

            return false;
        }

        /// <summary>True when the pair is one of the overlaps the screen intends.</summary>
        public static bool IsNestedByDesign(string first, string second) =>
            (first == ChestPanel && second == Chest) || (first == Chest && second == ChestPanel);

        /// <summary>
        /// Every modal control that is <b>not inside</b> the modal it belongs to, as readable lines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The overlap rule answers "may these two share pixels", and for a card it answers yes to
        /// almost everything — because the modal it sits on is allowed to cover the screen. What that
        /// leaves unchecked is the opposite mistake: a card drawn <i>outside</i> the modal's frame, on
        /// the panel's border or off the edge of the canvas, where it looks like a stray label rather
        /// than a choice.
        /// </para>
        /// <para>
        /// The modal is named rather than inferred: the caller measured which region <i>is</i> the modal
        /// (<see cref="MergeChooser"/>), and a run whose rectangles do not contain that name gets an
        /// empty list — the missing modal is the caller's failure to report, not a layout one, and
        /// inventing a violation here would double-report it.
        /// </para>
        /// </remarks>
        public static List<string> FindModalEscapees(IReadOnlyList<UiRegion> regions, string modalName)
        {
            var violations = new List<string>();

            Rect modal = default;
            bool measured = false;

            for (int i = 0; i < regions.Count; i++)
            {
                if (string.Equals(regions[i].Name, modalName, System.StringComparison.Ordinal))
                {
                    modal = regions[i].Rect;
                    measured = true;
                }
            }

            if (!measured)
            {
                return violations;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                UiRegion region = regions[i];
                if (region.Kind != UiRegionKind.ModalControl || Contains(modal, region.Rect))
                {
                    continue;
                }

                violations.Add(
                    region.Name + " " + region.Rect + " is not inside " + modalName + " " + modal);
            }

            return violations;
        }

        /// <summary>
        /// Every measured region the stage requires to be opaque that is not, as readable lines.
        /// </summary>
        /// <remarks>
        /// A separate call rather than part of <see cref="FindViolations"/>: that one is about \"can a
        /// player click the right thing\", this one is about \"can a player read it\", and the two fail
        /// for different reasons. The tolerance is a thousandth of an alpha step, because 0.90 written
        /// in a scene and read back as a float is not always exactly 0.9.
        /// </remarks>
        public static List<string> FindOpacityViolations(IReadOnlyList<UiRegion> regions)
        {
            var violations = new List<string>();

            for (int i = 0; i < regions.Count; i++)
            {
                string name = regions[i].Name;
                bool mustBeOpaque = false;

                for (int n = 0; n < OpaqueRegions.Length; n++)
                {
                    mustBeOpaque |= string.Equals(name, OpaqueRegions[n], System.StringComparison.Ordinal);
                }

                if (mustBeOpaque && regions[i].Alpha < OpaqueFloor - 0.001f)
                {
                    violations.Add(
                        name + " has alpha " + regions[i].Alpha.ToString("0.###") + ", below the "
                        + OpaqueFloor.ToString("0.###") + " floor: the world reads through it");
                }
            }

            return violations;
        }


        /// <summary>
        /// The mini ย่าม's own stacking rule (Stage 08d): the grid's area must sit <b>below</b> the
        /// caption, share the caption's left edge, and stay inside the panel — and the two must not
        /// overlap.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Why a rule of its own. <see cref="FindViolations"/> answers "may these two share pixels",
        /// and a grid centred in a large panel answers <i>yes, they are disjoint</i> while being the
        /// defect this rule is about: the ย่าม floats in the middle of the panel instead of starting
        /// under its caption. The stacked pair is the thing to measure, so it is measured directly.
        /// </para>
        /// <para>
        /// A run whose rectangles do not include all three names gets an empty list: the missing
        /// region is the caller's failure to measure, and inventing a violation here would double
        /// report it (the same convention as <see cref="FindModalEscapees"/>).
        /// </para>
        /// </remarks>
        public static List<string> FindMiniBagStackViolations(IReadOnlyList<UiRegion> regions)
        {
            var violations = new List<string>();

            Rect panel = default;
            Rect content = default;
            Rect caption = default;
            bool hasPanel = false;
            bool hasContent = false;
            bool hasCaption = false;

            for (int i = 0; i < regions.Count; i++)
            {
                if (string.Equals(regions[i].Name, MiniBag, System.StringComparison.Ordinal))
                {
                    panel = regions[i].Rect;
                    hasPanel = true;
                }
                else if (string.Equals(regions[i].Name, MiniBagContent, System.StringComparison.Ordinal))
                {
                    content = regions[i].Rect;
                    hasContent = true;
                }
                else if (string.Equals(regions[i].Name, BagCaption, System.StringComparison.Ordinal))
                {
                    caption = regions[i].Rect;
                    hasCaption = true;
                }
            }

            if (!hasPanel || !hasContent || !hasCaption)
            {
                return violations;
            }

            if (!Contains(panel, content))
            {
                violations.Add(
                    MiniBagContent + " " + content + " is not inside " + MiniBag + " " + panel);
            }

            if (Overlaps(content, caption))
            {
                violations.Add(
                    MiniBagContent + " " + content + " overlaps " + BagCaption + " " + caption);
            }

            if (content.yMax > caption.yMin + EdgeTolerance)
            {
                violations.Add(
                    MiniBagContent + " " + content + " is not below " + BagCaption + " " + caption
                    + ": its top edge is above the caption's bottom edge");
            }

            if (Mathf.Abs(content.xMin - caption.xMin) > EdgeTolerance)
            {
                violations.Add(
                    MiniBagContent + " " + content + " does not share " + BagCaption + "'s left edge "
                    + caption + ": the grid would not start under the caption");
            }

            return violations;
        }

        /// <summary>
        /// Every forbidden overlap in a measured layout, as readable lines. An empty list is a pass —
        /// so the same call reports the good case and the bad one.
        /// </summary>
        public static List<string> FindViolations(IReadOnlyList<UiRegion> regions)
        {
            var violations = new List<string>();

            for (int i = 0; i < regions.Count; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    if (!IsAllowed(regions[i], regions[j]))
                    {
                        violations.Add(
                            regions[i].ToString() + " overlaps " + regions[j].ToString());
                    }
                }
            }

            return violations;
        }
    }
}
