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
    /// tooltip drawn over the board is the point of it. Everything an overlay <i>must</i> satisfy is
    /// checked where the canvas is known — <see cref="UiCaptureTool"/> measures it in the pixels of
    /// the shot it took.
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

        /// <summary>The piece tooltip, on its own canvas above the screen.</summary>
        public const string Tooltip = "Tooltip";

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
        public static readonly string[] OpaqueRegions = { "Board", ChestPanel };

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
                // checked separately (inside the canvas, on the higher canvas), not here.
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
