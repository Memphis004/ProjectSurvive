using System.Collections.Generic;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// How much of the screen the Battle HUD covers, and the ceiling Stage 08g-2 puts on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stage's sixth item asks for a number rather than a look: "วัดพื้นที่ที่ UI บังจอ ต้อง ≤ 15 %
    /// ที่ 1920×1080 (มีเทสต์)". So the number needs one definition, and this is it — the area of the
    /// panels the player cannot see past, over the canvas.
    /// </para>
    /// <para>
    /// <b>Four panels, never five, and never the dev menu.</b> The mini ย่าม is gone (Stage 08g-2), the
    /// dev menu is hidden behind F1 and covers nothing until the key is pressed, and the pause overlay
    /// is not part of a running floor's HUD. What is left is the four surfaces a player reads while
    /// fighting: the floor block, the timeline bar, the run's numbers and the status frame.
    /// </para>
    /// <para>
    /// <b>The panels do not overlap</b> — measured, not assumed (the 08g report shows sum = union for
    /// the five panels that screen had) — so the sum of their rectangles is the area they occlude. A
    /// design that stacked translucent panels could not use this measure; the test that calls this
    /// asserts the rectangles are disjoint so the measure cannot quietly become wrong.
    /// </para>
    /// </remarks>
    public static class BattleHudArea
    {
        /// <summary>The fraction of the canvas the panels may cover: 15 %.</summary>
        public const float Budget = 0.15f;

        /// <summary>
        /// The fraction of the canvas the given rectangles cover.
        /// </summary>
        /// <remarks>
        /// A fraction rather than square pixels, so the same budget is the same budget at every size:
        /// the project's canvases use <c>ScaleWithScreenSize</c>, which scales both axes by one factor,
        /// so a rectangle authored in reference pixels keeps its share of the canvas at 1280×720 as at
        /// 1920×1080.
        /// </remarks>
        public static float Fraction(IReadOnlyList<Rect> panels, Vector2 canvas)
        {
            if (canvas.x <= 0f || canvas.y <= 0f)
            {
                return float.PositiveInfinity;
            }

            float covered = 0f;
            for (int i = 0; i < panels.Count; i++)
            {
                covered += panels[i].width * panels[i].height;
            }

            return covered / (canvas.x * canvas.y);
        }

        /// <summary>Whether two or more of the rectangles share pixels, which this measure cannot sum.</summary>
        public static bool AnyOverlap(IReadOnlyList<Rect> panels)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                for (int j = i + 1; j < panels.Count; j++)
                {
                    if (panels[i].Overlaps(panels[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
