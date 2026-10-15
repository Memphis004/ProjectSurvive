using NUnit.Framework;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 09d, item 2: where the piece tooltip goes for a pointer position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is a pure function (<see cref="CampTooltipPlacement.Place"/>), which is why the two
    /// things that break in practice — a panel hanging off the screen and a panel sitting under the
    /// cursor it belongs to — are checked here rather than in a screenshot. The sweep is over the
    /// <b>two sizes the GDD names</b>, at 0/25/50/75/100 % of each axis, because the failures only
    /// happen at an edge.
    /// </para>
    /// <para>
    /// The tooltip's own size is the scene's (440×330 in the canvas' 1920×1080 reference units), and
    /// at 1280×720 the canvas scaler hands the same units to a smaller canvas, so both sizes get the
    /// same panel.
    /// </para>
    /// </remarks>
    public sealed class CampTooltipPlacementTests
    {
        /// <summary>The tooltip panel's authored size, in canvas units.</summary>
        private static readonly Vector2 Panel = new Vector2(440f, 330f);

        /// <summary>The two sizes the GDD names, in canvas units.</summary>
        private static readonly Vector2[] Canvases =
        {
            new Vector2(1920f, 1080f),
            new Vector2(1280f, 720f),
        };

        [Test]
        public void The_panel_sits_beside_the_pointer_by_default()
        {
            Vector2 pointer = new Vector2(400f, 300f);
            Vector2 corner = CampTooltipPlacement.Place(pointer, Panel, new Vector2(1920f, 1080f));

            Assert.That(corner.x, Is.GreaterThan(pointer.x), "the panel was placed to the left of the pointer in the middle of the canvas");
            Assert.That(corner.y, Is.GreaterThan(pointer.y), "the panel was placed below the pointer in the middle of the canvas");
            Assert.That(corner.x, Is.EqualTo(pointer.x + CampTooltipPlacement.CursorOffset));
            Assert.That(corner.y, Is.EqualTo(pointer.y + CampTooltipPlacement.CursorOffset));

            // The pointer must stay out of the panel, or the tooltip covers the thing it describes.
            var panel = new Rect(corner.x, corner.y, Panel.x, Panel.y);
            Assert.That(panel.Contains(pointer), Is.False, "the panel covers the pointer it was placed for");
        }

        [Test]
        public void The_panel_flips_to_the_left_and_down_at_the_far_corner()
        {
            var canvas = new Vector2(1920f, 1080f);
            Vector2 pointer = new Vector2(canvas.x - 4f, canvas.y - 4f);
            Vector2 corner = CampTooltipPlacement.Place(pointer, Panel, canvas);

            Assert.That(
                corner.x + Panel.x, Is.LessThanOrEqualTo(pointer.x),
                "at the right edge the panel has to be to the left of the pointer, not pushed off the canvas");
            Assert.That(
                corner.y + Panel.y, Is.LessThanOrEqualTo(pointer.y),
                "at the top edge the panel has to be below the pointer, not pushed off the canvas");
        }

        [Test]
        public void The_panel_never_leaves_the_canvas_at_either_size()
        {
            float[] fractions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

            for (int c = 0; c < Canvases.Length; c++)
            {
                Vector2 canvas = Canvases[c];

                for (int ix = 0; ix < fractions.Length; ix++)
                {
                    for (int iy = 0; iy < fractions.Length; iy++)
                    {
                        var pointer = new Vector2(canvas.x * fractions[ix], canvas.y * fractions[iy]);
                        Vector2 corner = CampTooltipPlacement.Place(pointer, Panel, canvas);

                        Assert.That(
                            corner.x, Is.GreaterThanOrEqualTo(CampTooltipPlacement.Margin - 0.001f),
                            "at " + canvas + " with the pointer at " + pointer + " the panel starts off the left edge: " + corner);
                        Assert.That(
                            corner.y, Is.GreaterThanOrEqualTo(CampTooltipPlacement.Margin - 0.001f),
                            "at " + canvas + " with the pointer at " + pointer + " the panel starts off the bottom edge: " + corner);
                        Assert.That(
                            corner.x + Panel.x, Is.LessThanOrEqualTo(canvas.x - CampTooltipPlacement.Margin + 0.001f),
                            "at " + canvas + " with the pointer at " + pointer + " the panel runs off the right edge: " + corner);
                        Assert.That(
                            corner.y + Panel.y, Is.LessThanOrEqualTo(canvas.y - CampTooltipPlacement.Margin + 0.001f),
                            "at " + canvas + " with the pointer at " + pointer + " the panel runs off the top edge: " + corner);
                    }
                }
            }
        }

        [Test]
        public void A_panel_larger_than_the_canvas_is_pinned_to_the_margin_rather_than_pushed_off_it()
        {
            var canvas = new Vector2(320f, 240f);
            var panel = new Vector2(440f, 330f);

            Vector2 corner = CampTooltipPlacement.Place(new Vector2(160f, 120f), panel, canvas);

            // It cannot fit, so the top-left margin is the place that shows the most of it — and the
            // text still starts at a fixed place instead of moving with the pointer.
            Assert.That(corner.x, Is.EqualTo(CampTooltipPlacement.Margin));
            Assert.That(corner.y, Is.EqualTo(CampTooltipPlacement.Margin));
        }
    }
}
