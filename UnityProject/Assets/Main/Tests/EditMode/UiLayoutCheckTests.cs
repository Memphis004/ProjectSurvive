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

        /// <summary>
        /// The Result screen's summary column is held to the same floor as the camp's panels: at the
        /// camp panel's 0.92 it is not reported, and a column translucent enough to read the scene
        /// through is.
        /// </summary>
        [Test]
        public void The_summary_column_is_held_to_the_same_floor_as_the_camp_panels()
        {
            Assert.That(
                UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
                {
                    Surface(UiLayoutCheck.Summary, 240f, 26f, 800f, 666f, 0.92f),
                }),
                Is.Empty,
                "the summary column is authored at the camp panel's own alpha");

            List<string> violations = UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
            {
                Surface(UiLayoutCheck.Summary, 240f, 26f, 800f, 666f, 0.62f),
            });

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain(UiLayoutCheck.Summary));
        }

        [Test]
        public void The_panels_that_are_meant_to_be_translucent_are_not_held_to_the_floor()
        {
            // The จัดย่าม screen is an overlay on ศาลาพัก and the จัดย่าม panel behind it is the way
            // into the run: the instruction scroll and the altar are deliberately see-through, and
            // the rule names only the panels the two stages name.
            Assert.That(
                UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
                {
                    Surface("Instructions", 1104f, 884f, 776f, 176f, 0.85f),
                    Surface("Altar", 822f, 214f, 210f, 106f, 0.5f),
                    Surface("StatusBar", 24f, 16f, 1400f, 190f, 0.4f),
                }),
                Is.Empty);
        }

        // ------------------------------------------------------------------
        // Stage 09g: the merge chooser is a modal
        // ------------------------------------------------------------------

        [Test]
        public void The_merge_chooser_may_cover_the_panel_it_is_asking_about()
        {
            // "วางทับไอเทมแล้วเลือกอัปเกรด": the window lands on the board and the chest panel by
            // construction, and the dim layer lands on everything. Reporting either would report the
            // screen working as designed, which is why both are measured as overlays.
            var layout = new List<UiRegion>
            {
                Surface("Board", 40f, 140f, 740f, 580f),
                Surface(UiLayoutCheck.ChestPanel, 1104f, 200f, 776f, 400f),
                Overlay(UiLayoutCheck.MergeChooserDim, 0f, 0f, 1920f, 1080f),
                Overlay(UiLayoutCheck.MergeChooser, 420f, 200f, 1080f, 620f),
                Modal(UiLayoutCheck.MergeChooserCard(0), 460f, 320f, 320f, 380f),
                Modal(UiLayoutCheck.MergeChooserCard(1), 800f, 320f, 320f, 380f),
                Modal(UiLayoutCheck.MergeChooserCard(2), 1140f, 320f, 320f, 380f),
            };

            Assert.That(
                UiLayoutCheck.FindViolations(layout), Is.Empty,
                "a modal covering the panels behind it is what a modal is for");
            Assert.That(
                UiLayoutCheck.FindModalEscapees(layout, UiLayoutCheck.MergeChooser), Is.Empty,
                "every card is drawn inside the window's own frame");
        }

        [Test]
        public void The_merge_chooser_does_not_excuse_the_panels_own_collisions()
        {
            // The overlay is exempt, the pair behind it is not — the same rule the tooltip is held to.
            var layout = new List<UiRegion>
            {
                Surface("Altar", 822f, 16f, 210f, 106f),
                Surface("StatusBar", 24f, 16f, 1400f, 190f),
                Overlay(UiLayoutCheck.MergeChooserDim, 0f, 0f, 1920f, 1080f),
                Overlay(UiLayoutCheck.MergeChooser, 420f, 200f, 1080f, 620f),
            };

            List<string> violations = UiLayoutCheck.FindViolations(layout);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("Altar"));
            Assert.That(violations[0], Does.Contain("StatusBar"));
        }

        [Test]
        public void Two_cards_drawn_on_each_other_are_reported()
        {
            // The one collision inside a modal a player cannot read past: two choices sharing pixels.
            var layout = new List<UiRegion>
            {
                Overlay(UiLayoutCheck.MergeChooser, 420f, 200f, 1080f, 620f),
                Modal(UiLayoutCheck.MergeChooserCard(0), 460f, 320f, 320f, 380f),
                Modal(UiLayoutCheck.MergeChooserCard(1), 700f, 320f, 320f, 380f),
            };

            List<string> violations = UiLayoutCheck.FindViolations(layout);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain(UiLayoutCheck.MergeChooserCard(0)));
            Assert.That(violations[0], Does.Contain(UiLayoutCheck.MergeChooserCard(1)));
        }

        [Test]
        public void Cards_that_touch_and_the_back_button_are_not_collisions()
        {
            // Three cards in a row share their edges with the gaps between them, and the back button
            // stands under the row: none of these pairs shares a pixel.
            var layout = new List<UiRegion>
            {
                Modal(UiLayoutCheck.MergeChooserCard(0), 460f, 320f, 320f, 380f),
                Modal(UiLayoutCheck.MergeChooserCard(1), 780f, 320f, 320f, 380f),
                Modal(UiLayoutCheck.MergeChooserCard(2), 1100f, 320f, 320f, 380f),
                Modal(UiLayoutCheck.MergeChooserBack, 480f, 220f, 240f, 60f),
            };

            Assert.That(UiLayoutCheck.FindViolations(layout), Is.Empty);
        }

        [Test]
        public void A_card_that_escapes_the_window_is_reported()
        {
            var inside = new List<UiRegion>
            {
                Overlay(UiLayoutCheck.MergeChooser, 420f, 200f, 1080f, 620f),
                Modal(UiLayoutCheck.MergeChooserCard(0), 460f, 320f, 320f, 380f),
            };

            var outside = new List<UiRegion>
            {
                Overlay(UiLayoutCheck.MergeChooser, 420f, 200f, 1080f, 620f),
                Modal(UiLayoutCheck.MergeChooserCard(0), 60f, 320f, 320f, 380f),
            };

            Assert.That(
                UiLayoutCheck.FindModalEscapees(inside, UiLayoutCheck.MergeChooser), Is.Empty,
                "a card inside the window is the normal case");

            List<string> violations = UiLayoutCheck.FindModalEscapees(outside, UiLayoutCheck.MergeChooser);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain(UiLayoutCheck.MergeChooserCard(0)));
            Assert.That(violations[0], Does.Contain(UiLayoutCheck.MergeChooser));
        }

        [Test]
        public void A_run_that_never_measured_a_window_does_not_invent_an_escapee()
        {
            // The missing window is the caller's failure to report, not a layout one: with no region
            // carrying the window's name there is nothing to be outside of.
            var layout = new List<UiRegion>
            {
                Modal(UiLayoutCheck.MergeChooserCard(0), 60f, 320f, 320f, 380f),
            };

            Assert.That(
                UiLayoutCheck.FindModalEscapees(layout, UiLayoutCheck.MergeChooser), Is.Empty);
        }

        [Test]
        public void The_dim_layer_is_not_held_to_the_opacity_floor()
        {
            // A modal that dims the camp is translucent by design: the floor names the board and the
            // chest panel, and adding the dim layer to that rule would fail every correct screen.
            Assert.That(
                UiLayoutCheck.FindOpacityViolations(new List<UiRegion>
                {
                    new UiRegion(
                        UiLayoutCheck.MergeChooserDim, new Rect(0f, 0f, 1920f, 1080f), UiRegionKind.Overlay, 0.55f),
                    new UiRegion(
                        UiLayoutCheck.MergeChooser, new Rect(420f, 200f, 1080f, 620f), UiRegionKind.Overlay, 0.95f),
                }),
                Is.Empty);
        }

        private static UiRegion Overlay(string name, float x, float y, float w, float h) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.Overlay, 0.95f);

        private static UiRegion Modal(string name, float x, float y, float w, float h) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.ModalControl, 1f);

        private static UiRegion Surface(string name, float x, float y, float w, float h, float alpha) =>
            new UiRegion(name, new Rect(x, y, w, h), UiRegionKind.Surface, alpha);
    }
}
