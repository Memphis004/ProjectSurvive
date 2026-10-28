using System.Collections.Generic;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// How much of the screen the Battle HUD covers, and the ceiling Stage 08i puts on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The 08g-2 item asked for a number rather than a look: "วัดพื้นที่ที่ UI บังจอ ต้อง ≤ 15 %
    /// ที่ 1920×1080 (มีเทสต์)". So the number has one definition, and this is it — the area of the
    /// panels the player cannot see past, over the canvas.
    /// </para>
    /// <para>
    /// <b>Five panels, and never the dev menu.</b> The mini ย่าม left in Stage 08g-2 and came back in
    /// Stage 08i, because the player needs to see which items are ready and which are on cooldown;
    /// it costs a measured 3.00 % (288 × 216 at 1080p). The four other panels kept their 08g size and
    /// position. The dev menu is hidden behind F12 and covers nothing until the key is pressed, and
    /// the pause overlay is not part of a running floor's HUD.
    /// </para>
    /// <para>
    /// <b>Why the ceiling moved from 15 % to 18 %.</b> The four panels alone are 13.95 %; the fifth
    /// cannot be added under a 15 % ceiling without shrinking a panel the player already reads, which
    /// the stage forbids. 18 % is the smallest round ceiling that holds 13.95 + 3.00 = 16.95 % and
    /// leaves the rule with a measurable margin. See
    /// <c>docs/adr/0040-the-hud-budget-holds-five-panels.md</c>.
    /// </para>
    /// <para>
    /// <b>The panels do not overlap</b> — measured, not assumed — so the sum of their rectangles is
    /// the area they occlude. A design that stacked translucent panels could not use this measure;
    /// the test that calls this asserts the rectangles are disjoint so the measure cannot quietly
    /// become wrong.
    /// </para>
    /// </remarks>
    public static class BattleHudArea
    {
        /// <summary>The fraction of the canvas the five HUD panels may cover: 18 %.</summary>
        public const float Budget = 0.18f;

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
