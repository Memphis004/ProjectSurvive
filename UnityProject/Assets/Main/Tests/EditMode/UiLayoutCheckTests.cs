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
    }
}
