using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// The layout rule the Editor UI capture command applies, tested where it is readable.
    /// </summary>
    /// <remarks>
    /// The command's whole value is that it says <b>no</b> when two panels share pixels, so the
    /// failing case is the one that has to be proved: a verdict that never fails is worse than no
    /// verdict at all. <see cref="Two_panels_that_share_pixels_are_reported"/> feeds it a deliberately
    /// overlapping pair and requires the pair to come back named.
    /// </remarks>
    public sealed class UiLayoutCheckTests
    {
        private static UiRegion Surface(string name, float x, float y, float w, float h) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.Surface);

        private static UiRegion Control(string name, float x, float y, float w, float h) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.Control);

        /// <summary>The four panels the capture command is asked to keep apart, laid out as the camp has them at 1280×720.</summary>
        private static List<UiRegion> CampLayout() => new List<UiRegion>
        {
            Surface("Board", 26.667f, 213.333f, 493.333f, 386.667f),
            Surface(UiLayoutCheck.ChestPanel, 736f, 320f, 517.333f, 266.667f),
            Surface("Altar", 560f, 16f, 160f, 126.667f),
            Surface("StatusBar", 16f, 10.667f, 933.333f, 126.667f),
        };

        [Test]
        public void A_shorter_altar_clears_the_status_bar_and_reports_nothing()
        {
            List<UiRegion> layout = CampLayout();

            // The real screen has the altar (y 16..142.667) on top of the status bar (y 10.667..137.333)
            // because the panel is drawn over the bar. Move it clear of the bar — the arrangement the
            // command is asked to enforce — and the same call has to come back empty.
            layout[2] = Surface("Altar", 560f, 160f, 160f, 126.667f);

            Assert.That(UiLayoutCheck.FindViolations(layout), Is.Empty);
        }

        [Test]
        public void Two_panels_that_share_pixels_are_reported()
        {
            List<UiRegion> layout = CampLayout();

            // Deliberate overlap: the altar dropped onto the status bar, which is exactly the case a
            // stage asks about ("แถบสถานะ" against the panel's own blocks).
            List<string> violations = UiLayoutCheck.FindViolations(layout);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("Altar"));
            Assert.That(violations[0], Does.Contain("StatusBar"));
        }

        [Test]
        public void The_chest_grid_is_allowed_inside_its_own_panel()
        {
            var layout = new List<UiRegion>
            {
                Surface(UiLayoutCheck.ChestPanel, 736f, 320f, 517.333f, 266.667f),
                Surface(UiLayoutCheck.Chest, 760f, 340f, 460f, 220f),
            };

            Assert.That(UiLayoutCheck.FindViolations(layout), Is.Empty);
        }

        [Test]
        public void Two_other_surfaces_nested_are_not_allowed()
        {
            var layout = new List<UiRegion>
            {
                Surface("Board", 26.667f, 213.333f, 493.333f, 386.667f),
                Surface(UiLayoutCheck.Chest, 40f, 240f, 200f, 200f),
            };

            Assert.That(UiLayoutCheck.FindViolations(layout), Has.Count.EqualTo(1));
        }

        [Test]
        public void A_control_wholly_inside_its_surface_is_allowed()
        {
            var layout = new List<UiRegion>
            {
                Surface("Stats", 16f, 16f, 300f, 160f),
                Control("NextFloor", 40f, 40f, 200f, 48f),
            };

            Assert.That(UiLayoutCheck.FindViolations(layout), Is.Empty);
        }

        [Test]
        public void A_control_half_outside_its_surface_is_reported()
        {
            var layout = new List<UiRegion>
            {
                Surface("Stats", 16f, 16f, 300f, 160f),
                Control("NextFloor", 250f, 40f, 200f, 48f),
            };

            List<string> violations = UiLayoutCheck.FindViolations(layout);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("NextFloor"));
        }

        [Test]
        public void Two_controls_that_touch_are_not_overlapping()
        {
            var layout = new List<UiRegion>
            {
                Control("Rotate", 100f, 100f, 80f, 40f),
                Control("Undo", 180f, 100f, 80f, 40f),
            };

            Assert.That(UiLayoutCheck.FindViolations(layout), Is.Empty);
        }

        [Test]
        public void Two_controls_that_share_pixels_are_reported()
        {
            var layout = new List<UiRegion>
            {
                Control("Rotate", 100f, 100f, 80f, 40f),
                Control("Undo", 170f, 100f, 80f, 40f),
            };

            Assert.That(UiLayoutCheck.FindViolations(layout), Has.Count.EqualTo(1));
        }

        // ------------------------------------------------------------------
        // Stage 09d: the overlay and the opacity floor
        // ------------------------------------------------------------------

        [Test]
        public void The_tooltip_may_cover_the_board_because_that_is_what_it_is_for()
        {
            var layout = new List<UiRegion>
            {
                Surface("Board", 40f, 140f, 740f, 580f),
                Overlay(UiLayoutCheck.Tooltip, 100f, 200f, 440f, 330f),
            };

            Assert.That(
                UiLayoutCheck.FindViolations(layout), Is.Empty,
                "a tooltip drawn over the board is the point of it, and the overlap rule must not report it");
        }

        [Test]
        public void A_tooltip_over_a_panel_does_not_excuse_the_panels_own_collisions()
        {
            // The overlay is exempt, the pair behind it is not: a tooltip covering two panels that
            // overlap each other must not hide that defect.
            var layout = new List<UiRegion>
            {
                Surface("Altar", 822f, 16f, 210f, 106f),
                Surface("StatusBar", 24f, 16f, 1400f, 190f),
                Overlay(UiLayoutCheck.Tooltip, 800f, 100f, 440f, 330f),
            };

            List<string> violations = UiLayoutCheck.FindViolations(layout);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("Altar"));
            Assert.That(violations[0], Does.Contain("StatusBar"));
        }

        [Test]
        public void A_board_below_the_opacity_floor_is_reported_and_an_opaque_one_is_not()
        {
            Assert.That(
                UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
                {
                    Surface("Board", 40f, 140f, 740f, 580f, 0.92f),
                    Surface(UiLayoutCheck.ChestPanel, 1104f, 200f, 776f, 400f, 0.9f),
                }),
                Is.Empty,
                "0.9 is the floor itself, and a scene that writes 0.9 must not be reported");

            List<string> violations = UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
            {
                Surface("Board", 40f, 140f, 740f, 580f, 0.62f),
            });

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("Board"));
            Assert.That(violations[0], Does.Contain("0.62"));
        }

        [Test]
        public void The_panels_that_are_meant_to_be_translucent_are_not_held_to_the_floor()
        {
            // The จัดย่าม screen is an overlay on ศาลาพัก: the instruction scroll and the altar are
            // deliberately see-through, and the rule names only the two panels the stage names.
            Assert.That(
                UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
                {
                    Surface("Instructions", 1104f, 884f, 776f, 176f, 0.85f),
                    Surface("Altar", 822f, 214f, 210f, 106f, 0.5f),
                    Surface("StatusBar", 24f, 16f, 1400f, 190f, 0.4f),
                }),
                Is.Empty);
        }

        private static UiRegion Overlay(string name, float x, float y, float w, float h) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.Overlay, 0.95f);

        private static UiRegion Surface(string name, float x, float y, float w, float h, float alpha) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.Surface, alpha);
    }
}
