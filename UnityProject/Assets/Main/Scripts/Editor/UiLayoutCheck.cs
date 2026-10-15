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
    }

    /// <summary>One named screen rectangle, in pixels with the origin at the bottom-left (Unity's own convention).</summary>
    public readonly struct UiRegion
    {
        public UiRegion(string name, Rect rect, UiRegionKind kind)
        {
            Name = name;
            Rect = rect;
            Kind = kind;
        }

        public readonly string Name;
        public readonly Rect Rect;
        public readonly UiRegionKind Kind;

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
