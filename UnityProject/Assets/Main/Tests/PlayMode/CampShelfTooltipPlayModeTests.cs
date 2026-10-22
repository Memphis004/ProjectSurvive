using System.Collections;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 09n: a shelf offer explains itself under a <b>real pointer</b>, on the real Camp scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The EditMode suite drives <see cref="CampPresenter.HoverOffer"/> directly; what only a Play Mode
    /// test can prove is the wiring behind it — that the view's own hit test finds the station under
    /// the mouse, that the tooltip is the panel the scene really draws (on the canvas above the camp),
    /// that it stays inside the window at the corner shelves, and that a pointer moving around inside
    /// one shelf allocates nothing.
    /// </para>
    /// <para>
    /// The panel is deliberately <b>not</b> opened here: the จัดย่าม screen suppresses the shelf
    /// tooltip, so this fixture is the player's other state — walking the sala.
    /// </para>
    /// </remarks>
    public sealed class CampShelfTooltipPlayModeTests : InputTestFixture
    {
        private const string CampSceneName = "Camp";

        private Mouse _mouse;
        private CampSceneDirector _director;
        private CampWorldView _world;
        private CampTooltipView _tooltip;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        /// <summary>Loads the Camp scene, starts the run, and finds the views. No panel is opened.</summary>
        private IEnumerator LoadCamp()
        {
            // A shell this fixture owns: a run that outlived another test would carry its wallet and
            // its rented shelves into this one (Stage 10a made the App scope persist).
            SceneFlowHarness.ResetShell();
            yield return null;

            SceneManager.LoadScene(CampSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(EventSystem.current, Is.Not.Null, "the Camp scene has no EventSystem");

            _director = Object.FindFirstObjectByType<CampSceneDirector>();
            Assert.That(_director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(_director.TryStart(), Is.True, "the camp did not start: " + _director.StartError);
            yield return null;

            _world = Object.FindFirstObjectByType<CampWorldView>(FindObjectsInactive.Include);
            Assert.That(_world, Is.Not.Null, "the Camp scene has no CampWorldView");

            _tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Assert.That(_tooltip, Is.Not.Null, "the Camp scene has no CampTooltipView");

            yield return null;
        }

        /// <summary>
        /// The stage's first claim: moving the pointer onto each of the four shelves puts the offer's
        /// own readout on screen, and onto the shelf's rectangle and nowhere else.
        /// </summary>
        [UnityTest]
        public IEnumerator Hovering_each_shelf_shows_that_offers_readout()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            int checkedShelves = 0;

            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                if (run.Stock[slot].IsEmpty)
                {
                    continue;
                }

                Assert.That(
                    _world.TryGetShelfScreenRect(slot, out Rect shelf), Is.True,
                    "shelf " + slot + " is not on screen");

                yield return MoveTo(shelf.center);
                yield return null;

                Assert.That(
                    presenter.HoveredOffer, Is.EqualTo(slot),
                    "the pointer at " + shelf.center + " is over shelf " + slot);

                Result<OfferReadout> explained = run.ExplainOffer(slot);
                Assert.That(explained.IsSuccess, Is.True, explained.Error.ToString());
                OfferReadout offer = explained.Value;

                Assert.That(_tooltip.Visible, Is.True, "hovering shelf " + slot + " drew no tooltip");
                Assert.That(offer.Price, Is.GreaterThan(0), "a shelf that drew a tooltip has a price");

                Assert.That(
                    _tooltip.TryGetScreenRect(out Rect panel), Is.True,
                    "the tooltip drew nothing, so it cannot be measured");
                Assert.That(
                    panel.xMin >= 0f && panel.yMin >= 0f
                    && panel.xMax <= Screen.width && panel.yMax <= Screen.height,
                    Is.True,
                    "the tooltip left the window at shelf " + slot + ": " + panel
                    + " in " + Screen.width + "x" + Screen.height);

                checkedShelves++;
            }

            Assert.That(checkedShelves, Is.GreaterThan(0), "the first camp offers nothing to hover");
        }

        /// <summary>
        /// A corner shelf is where a tooltip falls off the screen: the placement rule has to flip it,
        /// and the panel the view finally put down is what has to be inside the window.
        /// </summary>
        [UnityTest]
        public IEnumerator The_tooltip_stays_inside_the_window_at_the_edge_shelves()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;

            // The rightmost shelf with something on it: its centre is the right of the screen, so the
            // panel beside the pointer would pass the edge.
            bool measured = false;

            for (int slot = run.Stock.SlotCount - 1; slot >= 0; slot--)
            {
                if (run.Stock[slot].IsEmpty || !_world.TryGetShelfScreenRect(slot, out Rect shelf))
                {
                    continue;
                }

                yield return MoveTo(new Vector2(shelf.xMax - 4f, shelf.yMax - 4f));
                yield return null;

                Assert.That(_tooltip.Visible, Is.True, "the edge shelf did not explain itself");

                Assert.That(_tooltip.TryGetScreenRect(out Rect panel), Is.True);
                Assert.That(
                    panel.xMax <= Screen.width && panel.yMax <= Screen.height,
                    Is.True,
                    "the tooltip passed the window's edge: " + panel + " in " + Screen.width + "x" + Screen.height);
                Assert.That(
                    CampTooltipPlacement.Margin <= panel.xMin && CampTooltipPlacement.Margin <= panel.yMin,
                    Is.True,
                    "the tooltip was pushed off the near edge instead of flipped: " + panel);
                measured = true;
                break;
            }

            Assert.That(measured, Is.True, "the camp had no edge shelf to measure");
        }

        /// <summary>
        /// Stage 09n: renting the shelf under the pointer takes the tooltip away in the same command,
        /// and the pointer has not moved — the shelf simply holds nothing any more.
        /// </summary>
        [UnityTest]
        public IEnumerator Renting_the_hovered_shelf_takes_the_tooltip_away()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            int slot = -1;
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                if (!run.Stock[i].IsEmpty && _world.TryGetShelfScreenRect(i, out _))
                {
                    slot = i;
                    break;
                }
            }

            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "the camp has no shelf to rent");
            Assert.That(_world.TryGetShelfScreenRect(slot, out Rect shelf), Is.True);

            yield return MoveTo(shelf.center);
            yield return null;
            Assert.That(_tooltip.Visible, Is.True);

            // Enough bia for the rent, so what is tested is the tooltip and not the purse.
            run.Wallet.Credit(200);
            presenter.Rent(slot);
            yield return null;

            Assert.That(run.Stock[slot].IsPurchased, Is.True, "the rent did not go through");
            Assert.That(_tooltip.Visible, Is.False, "the tooltip kept describing a rented shelf");
        }

        /// <summary>
        /// Stage 09n: the tooltip draws over the camp. It is a second root canvas with a higher
        /// <c>sortingOrder</c>, which is what \"เหนือป้ายราคาและชั้น\" means in this project.
        /// </summary>
        [UnityTest]
        public IEnumerator The_tooltip_draws_above_the_camp_canvas()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;

            Assert.That(_world.TryGetShelfScreenRect(0, out Rect shelf), Is.True);
            yield return MoveTo(shelf.center);
            yield return null;

            Assert.That(_tooltip.Visible, Is.True);
            Assert.That(_tooltip.RootCanvas, Is.Not.Null, "the tooltip has no root canvas");

            Canvas camp = null;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != _tooltip.RootCanvas && candidate.rootCanvas == candidate
                    && candidate.GetComponent<CampWorldView>() == null
                    && candidate.gameObject.scene.name == _tooltip.RootCanvas.gameObject.scene.name)
                {
                    camp = candidate;
                    break;
                }
            }

            Assert.That(camp, Is.Not.Null, "the camp has no canvas of its own to compare with");
            Assert.That(
                _tooltip.RootCanvas.sortingOrder, Is.GreaterThan(camp.sortingOrder),
                "the tooltip's canvas does not draw above the camp's");
            Assert.That(run.Stock[0].IsEmpty, Is.False, "slot 0 should hold an offer for this test");
        }

        /// <summary>
        /// AGENTS rule 8: a pointer moving inside the shelf it is already on must not build a string,
        /// because the tooltip's content has not changed. Measured, not inspected.
        /// </summary>
        [UnityTest]
        public IEnumerator Moving_within_one_shelf_allocates_nothing()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            Assert.That(_world.TryGetShelfScreenRect(0, out Rect shelf), Is.True);
            yield return MoveTo(shelf.center);
            yield return null;

            Assert.That(presenter.HoveredOffer, Is.EqualTo(0), "the pointer is not on shelf 0");
            Assert.That(_tooltip.Visible, Is.True);

            // Let the scene settle on the shelf before the measurement starts.
            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 8; i++)
            {
                var drift = new Vector2(
                    shelf.center.x + ((i % 3) - 1) * 2f,
                    shelf.center.y + (((i / 3) % 3) - 1) * 2f);

                Set(_mouse.position, drift);
                yield return null;
            }

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(
                allocated, Is.EqualTo(0),
                "moving the pointer inside one shelf allocated " + allocated + " bytes");
            Assert.That(run.Stock[0].IsEmpty, Is.False);
        }

        /// <summary>Moves the pointer and lets the frame read it.</summary>
        private IEnumerator MoveTo(Vector2 point)
        {
            Set(_mouse.position, point);
            yield return null;
        }
    }
}
