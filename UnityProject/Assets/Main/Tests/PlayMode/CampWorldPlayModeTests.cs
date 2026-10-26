using System.Collections;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 09c: ศาลาพัก as a place the player <b>walks</b> — WASD movement under a follow camera,
    /// four shelf stations in the world, the เซียมซี cylinder, and renting by standing at a shelf and
    /// pressing E or by clicking it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every command in here goes through the real input devices into the scene's own views, exactly
    /// like <see cref="CampBagDragPlayModeTests"/>: a keyboard and a mouse are added to the test's
    /// input system and the walk is driven key by key. Nothing calls <c>CampPresenter</c> to rent —
    /// a test that did would pass on a camp where walking or clicking was broken, which is the
    /// failure this stage exists to catch.
    /// </para>
    /// <para>
    /// The scene is loaded from the test body rather than a <c>[UnitySetUp]</c>: that fixture's setup
    /// runs after a <c>[UnitySetUp]</c> coroutine on this runner and resets the input system under
    /// the scene's own module (see <see cref="CampBagDragPlayModeTests.LoadCamp"/>).
    /// </para>
    /// </remarks>
    public sealed class CampWorldPlayModeTests : InputTestFixture
    {
        private const string CampSceneName = "Camp";

        /// <summary>
        /// How long a walk across the sala may take before the test gives up. A duration rather than
        /// a frame count: movement is <c>speed × deltaTime</c>, so a fast editor (hundreds of frames
        /// a second) would otherwise cover a fraction of the distance a slow one does, and the test
        /// would be measuring the machine instead of the camp.
        /// </summary>
        private const float WalkBudgetSeconds = 20f;

        private Keyboard _keyboard;
        private Mouse _mouse;
        private CampSceneDirector _director;
        private CampWorldView _world;
        private CampPlayerView _player;
        private CampBagScreenView _bag;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        /// <summary>Loads the camp and hands back the sala, the walker and the bag screen.</summary>
        private IEnumerator LoadCamp()
        {
            // A shell this fixture owns: see SceneFlowHarness. The App scope persists once a
            // fixture loads Boot, so a test that did not reset would inherit a live run.
            SceneFlowHarness.ResetShell();
            yield return null;

            SceneManager.LoadScene(CampSceneName, LoadSceneMode.Single);
            yield return null;

            Assert.That(EventSystem.current, Is.Not.Null, "the Camp scene has no EventSystem");

            _director = Object.FindFirstObjectByType<CampSceneDirector>();
            Assert.That(_director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(_director.TryStart(), Is.True, "the camp did not start: " + _director.StartError);
            yield return null;

            _world = Object.FindFirstObjectByType<CampWorldView>();
            _player = Object.FindFirstObjectByType<CampPlayerView>();
            // FindObjectsInactive.Include: the จัดย่าม panel starts closed (work item 5), so the
            // view sits on an inactive GameObject and the default active-only search finds nothing.
            _bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);

            Assert.That(_world, Is.Not.Null, "the Camp scene has no CampWorldView");
            Assert.That(_player, Is.Not.Null, "the Camp scene has no CampPlayerView");
            Assert.That(_bag, Is.Not.Null, "the Camp scene has no CampBagScreenView");
            Assert.That(_world.ShelfCount, Is.EqualTo(_director.Run.Stock.SlotCount), "one station per shop slot");
        }

        /// <summary>
        /// Stage 09c work item 1: WASD walks the figure, and the camera follows it.
        /// </summary>
        [UnityTest]
        public IEnumerator The_player_walks_with_wasd_and_the_camera_follows()
        {
            yield return LoadCamp();

            Vector3 start = _player.Position;
            Vector3 cameraAtStart = _world.CameraPosition;

            // 0.6 s at `constant.move_speed` (4 units/s) is ~2.4 units — well past the 0.4 the
            // assertion asks for, and still inside the sala, so the clamp cannot be what moves it.
            yield return Hold(0.6f, up: true);

            Assert.That(_player.Position.z, Is.GreaterThan(start.z + 0.4f), "W did not walk the player away from the camera");
            Assert.That(_player.Moving, Is.False, "the player is still reported as walking after the key came up");
            Assert.That(
                _world.CameraPosition.z, Is.GreaterThan(cameraAtStart.z + 0.2f),
                "the camera did not follow the player");

            Vector3 afterForward = _player.Position;
            yield return Hold(0.4f, right: true);

            Assert.That(_player.Position.x, Is.GreaterThan(afterForward.x + 0.2f), "D did not walk the player to the right");
        }

        /// <summary>
        /// Stage 17c: the doctor in the sala is the <b>Spine figure</b>, and its pose is the walk the
        /// view already does — running while the keys move them, standing the moment they stop, facing
        /// the way they walked, and standing while the จัดย่าม panel is open (the panel stops the walk,
        /// so it stops the pose with it).
        /// </summary>
        /// <remarks>
        /// The keys are pressed and <b>left held</b> while the pose is read: <see cref="Hold"/> releases
        /// them at the end, and a released key is exactly the "stopped" state this test also has to see.
        /// </remarks>
        [UnityTest]
        public IEnumerator Walking_draws_the_figure_running_and_stopping_stands_it()
        {
            yield return LoadCamp();

            SpinePlayerView figure = Object.FindFirstObjectByType<SpinePlayerView>();
            Assert.That(figure, Is.Not.Null, "the sala draws no Spine figure for the player");
            Assert.That(
                figure.IsBegun, Is.True,
                "the figure did not build, so the placeholder silhouette draws the doctor: " + figure.ErrorCode);
            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Idle), "a player who has not walked yet must stand");

            string idle = figure.CurrentAnimation;
            Assert.That(idle, Is.Not.Null.And.Not.Empty, "the standing figure plays no animation");

            // Walking right: the walk pose, facing that way, and an animation that advanced.
            Press(_keyboard.dKey);
            yield return Wait(0.35f);

            Assert.That(_player.Moving, Is.True, "the walk key did not reach the walker");
            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Run), "walking must draw the run pose");
            Assert.That(figure.FaceDirection, Is.EqualTo(1), "walking right must face right");
            Assert.That(figure.AnimationTime, Is.GreaterThan(0f), "the walk's animation never advanced");

            string run = figure.CurrentAnimation;
            Assert.That(run, Is.Not.EqualTo(idle), "the run pose draws the idle animation");

            // Stopping: the same figure stands again.
            Release(_keyboard.dKey);
            yield return null;
            yield return null;

            Assert.That(_player.Moving, Is.False, "the walker is still walking after the key came up");
            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Idle), "a stopped player must stand");
            Assert.That(
                figure.CurrentAnimation, Is.EqualTo(idle),
                "standing after a walk did not restore the standing animation");

            // The other direction: the same figure turns rather than a second one being built.
            Press(_keyboard.aKey);
            yield return Wait(0.35f);
            Assert.That(figure.FaceDirection, Is.EqualTo(-1), "walking left must face left");
            Release(_keyboard.aKey);
            yield return null;

            // The จัดย่าม panel stops the walk, so the doctor stands while a walk key is still held.
            Press(_keyboard.wKey);
            yield return Wait(0.2f);
            Assert.That(figure.Pose, Is.EqualTo(PlayerPose.Run), "holding a walk key did not start the walk");

            Assert.That(
                _bag.TryGetButtonScreenPoint(CampBagButton.Open, out Vector2 toggle), Is.True,
                "the จัดย่าม button is not on screen");
            yield return Click(toggle);

            Assert.That(_bag.PanelVisible, Is.True, "clicking จัดย่าม did not open the panel");
            yield return null;
            Assert.That(
                figure.Pose, Is.EqualTo(PlayerPose.Idle),
                "the figure kept running while the จัดย่าม panel was open");

            Release(_keyboard.wKey);
            yield return null;
        }

        /// <summary>The player cannot walk out of the sala (the walk area clamps).</summary>
        [UnityTest]
        public IEnumerator The_player_cannot_walk_out_of_the_sala()
        {
            yield return LoadCamp();

            // Long enough to cross the whole sala in any direction from anywhere in it.
            yield return Hold(4f, down: true);

            Rect area = _world.WalkArea;
            Assert.That(
                area.Contains(new Vector2(_player.Position.x, _player.Position.z)), Is.True,
                "the player walked out of the sala: it is at " + _player.Position + ", the area is " + area);

            // And it really is the edge it stopped at, so a clamp that merely froze the player
            // (or a walk area reported the wrong way round) fails here rather than passing quietly.
            Assert.That(
                _player.Position.z, Is.EqualTo(area.yMin).Within(0.05f),
                "S did not reach the near edge of the walk area");
        }

        /// <summary>
        /// Stage 09c work item 3: walk up to a shelf, press E, the goods go into the chest and the
        /// purse is charged the price the shelf showed.
        /// </summary>
        [UnityTest]
        public IEnumerator Walking_to_a_shelf_and_pressing_E_rents_it_into_the_chest()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            int price = run.Stock[0].Price;
            int biaBefore = run.Wallet.Bia;
            int chestBefore = run.ChestUsed;

            yield return WalkToShelf(0);
            Assert.That(_player.FocusedShelf, Is.EqualTo(0), "the player is not standing at the first shelf");

            yield return PressKey(_keyboard.eKey);

            Assert.That(_player.RentRequests, Is.EqualTo(1), "E did not ask for a rent");
            Assert.That(_director.Presenter.Counters.Rented, Is.EqualTo(1), "the shelf was not rented");
            Assert.That(run.Wallet.Bia, Is.EqualTo(biaBefore - price), "the purse was not charged the shelf's price");
            Assert.That(run.ChestUsed, Is.EqualTo(chestBefore + 1), "the goods did not go into the chest");
            Assert.That(run.Stock[0].IsEmpty, Is.True, "the rented shelf is not empty");
        }

        /// <summary>
        /// Stage 09c work item 3, the refusal: a purse that cannot cover the shelf says why and is
        /// charged nothing ("หีบเต็มหรือเงินไม่พอให้แสดงเหตุผลและไม่หักเงิน").
        /// </summary>
        [UnityTest]
        public IEnumerator Renting_without_enough_bia_is_refused_and_charges_nothing()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;

            yield return WalkToShelf(0);
            yield return PressKey(_keyboard.eKey);

            int biaAfterFirst = run.Wallet.Bia;
            int rentPrice = run.Stock[1].Price;
            Assert.That(biaAfterFirst, Is.LessThan(rentPrice), "the fixture expected the first rent to leave too little bia");

            yield return WalkToShelf(1);
            yield return PressKey(_keyboard.eKey);

            Assert.That(_director.Presenter.Counters.Rented, Is.EqualTo(1), "a rent the purse could not cover was accepted");
            Assert.That(run.Wallet.Bia, Is.EqualTo(biaAfterFirst), "a refused rent charged the purse");
            Assert.That(_director.Presenter.StatusCode, Is.Not.Empty, "a refused rent said nothing to the player");
            Assert.That(run.Stock[1].IsEmpty, Is.False, "a refused rent emptied the shelf");
        }

        /// <summary>
        /// Stage 09c work item 6: a shelf can be rented by clicking it, without walking there.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_a_shelf_rents_it_without_walking()
        {
            yield return LoadCamp();

            Vector3 start = _player.Position;
            int biaBefore = _director.Run.Wallet.Bia;

            Assert.That(_world.TryGetShelfScreenRect(2, out Rect station), Is.True, "shelf 2 is not on screen");
            yield return Click(station.center);

            Assert.That(_player.RentRequests, Is.EqualTo(1), "the click did not reach the shelf");
            Assert.That(_director.Presenter.Counters.Rented, Is.EqualTo(1), "clicking a shelf did not rent it");
            Assert.That(_director.Run.Wallet.Bia, Is.LessThan(biaBefore), "the purse was not charged");
            Assert.That(_player.Position, Is.EqualTo(start), "the player walked to the shelf instead of clicking it");
        }

        /// <summary>
        /// Stage 09c work item 4: ล็อกสินค้าก่อนรีเซ็ต from the shelf's own sign, by clicking it.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_a_shelfs_lock_pins_it_against_the_reset()
        {
            yield return LoadCamp();

            Assert.That(_world.TryGetShelfLockScreenRect(1, out Rect lockRect), Is.True, "shelf 1 has no lock control on screen");

            // The control has to be a target a player can hit, and it must not have grown over the
            // shelf's own target: a click meant for the shelf would pin it instead of renting.
            Assert.That(lockRect.height, Is.GreaterThanOrEqualTo(12f),
                "the lock control is drawn caption-sized (" + lockRect.height.ToString("0") + " px tall)");

            Assert.That(_world.TryGetShelfScreenRect(1, out Rect station), Is.True, "shelf 1 is not on screen");
            Assert.That(lockRect.Contains(station.center), Is.False,
                "the lock control covers the shelf's own click target");

            yield return Click(lockRect.center);

            Assert.That(_player.LockRequests, Is.EqualTo(1), "the click did not reach the lock control");
            Assert.That(_director.Presenter.Counters.SlotLocks, Is.EqualTo(1), "the slot was not locked");
            Assert.That(_director.Run.Stock[1].IsLocked, Is.True, "the run does not hold the pinned slot");
        }

        /// <summary>
        /// Stage 09c work item 4: the เซียมซี cylinder is an object in the sala — walk to it, press E,
        /// the stock is re-rolled and the price is paid.
        /// </summary>
        [UnityTest]
        public IEnumerator Casting_the_sticks_at_the_drum_rerolls_the_stock()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            int price = run.RefreshPrice;
            int biaBefore = run.Wallet.Bia;
            string offeredBefore = run.Stock[0].OfferedId;

            yield return WalkTo(_world.ResetPosition.x, _world.ResetPosition.z);
            Assert.That(_player.ResetFocused, Is.True, "the player is not standing at the เซียมซี cylinder");

            yield return PressKey(_keyboard.eKey);

            Assert.That(_player.ResetRequests, Is.EqualTo(1), "E did not ask to cast the sticks");
            Assert.That(_director.Presenter.Counters.Refreshes, Is.EqualTo(1), "the stock was not re-rolled");
            Assert.That(run.Wallet.Bia, Is.EqualTo(biaBefore - price), "the cast was not paid for");
            Assert.That(
                run.Stock[0].OfferedId != offeredBefore || run.Wallet.Bia == biaBefore - price,
                Is.True,
                "the cast neither changed the offer nor charged for it");
        }

        /// <summary>
        /// Stage 06c: a shelf that was rented is refilled by the paid เซียมซี cast — the shelf the
        /// player emptied on screen shows a fresh offer the moment the drum is clicked.
        /// </summary>
        /// <remarks>
        /// The drum is clicked at a point that is inside its own rectangle and inside no shelf's, so
        /// the camp's click order (shelf first, drum second) cannot turn this into a rent.
        /// </remarks>
        [UnityTest]
        public IEnumerator Clicking_the_drum_refills_a_shelf_that_was_just_rented()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;

            // A purse that can pay for both the rent and the cast, whatever the shelf cost: the
            // rule under test is the refill, not what 15 bia affords.
            run.Wallet.Credit(100);

            Assert.That(_world.TryGetShelfScreenRect(0, out Rect shelf), Is.True, "shelf 0 is not on screen");
            yield return Click(shelf.center);

            Assert.That(run.Stock[0].IsPurchased, Is.True, "clicking the shelf did not rent it");
            Assert.That(run.Stock[0].IsEmpty, Is.True, "the rented shelf still holds an offer");

            Assert.That(TryFindClearDrumPoint(out Vector2 drumPoint), Is.True, "the drum has no point clear of the shelves");
            yield return Click(drumPoint);

            Assert.That(_player.ResetRequests, Is.EqualTo(1), "the click did not reach the เซียมซี cylinder");
            Assert.That(_director.Presenter.Counters.Refreshes, Is.EqualTo(1), "the cast did not re-roll the stock");

            // Stage 06c: the paid cast refilled the shelf that was rented a moment ago.
            Assert.That(run.Stock[0].IsPurchased, Is.False, "the cast left the shelf marked rented");
            Assert.That(run.Stock[0].IsEmpty, Is.False, "the cast left the rented shelf empty");
            Assert.That(run.Stock[0].Price, Is.GreaterThan(0), "the fresh offer has no price");
        }

        /// <summary>
        /// Stage 09c work item 5: the จัดย่าม button opens the panel, and while it is open the figure
        /// stands still.
        /// </summary>
        [UnityTest]
        public IEnumerator The_character_stops_walking_while_the_bag_panel_is_open()
        {
            yield return LoadCamp();

            Assert.That(_bag.PanelVisible, Is.False, "the panel should start closed: the camp is a place the player walks");

            Vector3 start = _player.Position;

            // The walking itself first: the panel being open must be the only reason it stops.
            yield return Hold(0.5f, up: true);
            Vector3 walked = _player.Position;
            Assert.That(walked.z, Is.GreaterThan(start.z + 0.2f), "the player did not walk");

            Assert.That(_bag.TryGetButtonScreenPoint(CampBagButton.Open, out Vector2 toggle), Is.True, "the จัดย่าม button is not on screen");
            yield return Click(toggle);
            Assert.That(_bag.PanelVisible, Is.True, "clicking จัดย่าม did not open the panel");
            Assert.That(_player.WalkingBlocked, Is.True, "the walker does not know the panel is open");

            yield return Hold(0.5f, up: true);

            Assert.That(_player.Position, Is.EqualTo(walked), "the player walked with the จัดย่าม panel open");

            Assert.That(_bag.TryGetButtonScreenPoint(CampBagButton.Close, out Vector2 close), Is.True, "the panel's กลับ button is not on screen");
            yield return Click(close);
            Assert.That(_bag.PanelVisible, Is.False, "clicking กลับ did not close the panel");

            yield return Hold(0.5f, up: true);
            Assert.That(_player.Position.z, Is.GreaterThan(walked.z + 0.2f), "the player did not walk again after closing the panel");
        }

        /// <summary>
        /// The price board is what the player reads to decide a purchase: its ink is near-black on a
        /// brightened board, and its type is sized past the 22 px ink floor this project states for a
        /// 1080p screen (both measured on the capture). The lock control shares that ink and is a
        /// control, so it is held to a size of its own rather than the price's. Asserted here because
        /// the labels only exist once the view has built them, which needs play mode.
        /// </summary>
        [UnityTest]
        public IEnumerator The_price_boards_use_the_readable_ink_and_type_size()
        {
            yield return LoadCamp();

            TMPro.TextMeshPro[] labels = Object.FindObjectsByType<TMPro.TextMeshPro>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int prices = 0;
            int locks = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                bool price = labels[i].name.StartsWith("ShelfPrice", System.StringComparison.Ordinal);
                bool control = labels[i].name.StartsWith("ShelfLock", System.StringComparison.Ordinal);

                if (!price && !control)
                {
                    continue;
                }

                float ink = ProjectSurvive.Game.Presentation.Battle.Environment.ArenaContrast
                    .RelativeLuminance(new Color32(
                        (byte)Mathf.RoundToInt(labels[i].color.r * 255f),
                        (byte)Mathf.RoundToInt(labels[i].color.g * 255f),
                        (byte)Mathf.RoundToInt(labels[i].color.b * 255f),
                        255));

                Assert.That(ink, Is.LessThan(0.02f),
                    labels[i].name + "'s ink is no longer near-black, so its contrast against the board fell");
                Assert.That(labels[i].raycastTarget, Is.False, labels[i].name + " would swallow a click");

                if (price)
                {
                    prices++;
                    Assert.That(labels[i].fontSize, Is.GreaterThanOrEqualTo(3.8f),
                        labels[i].name + " was shrunk below the size that drew 22 px of ink at 1080p");
                    continue;
                }

                locks++;
                Assert.That(labels[i].fontSize, Is.GreaterThanOrEqualTo(2.8f),
                    labels[i].name + " fell below the size that draws the project's 22 px floor at 1080p");
            }

            Assert.That(prices, Is.EqualTo(_world.ShelfCount), "not every station has a price board");
            Assert.That(locks, Is.EqualTo(_world.ShelfCount), "not every station has a lock control");
        }

        /// <summary>
        /// Stage 09o: the yard is scenery. Every station and the เซียมซี cylinder is still reachable
        /// by a click with the cards standing in front of it — nothing in the yard is a collider and
        /// nothing in the yard is a raycast target.
        /// </summary>
        /// <remarks>
        /// This tests the half the rectangle tests cannot: a card drawn between the camera and a
        /// shelf must not intercept the pointer. The camp resolves a click by a camera ray against
        /// named renderers, so a collider anywhere in the ray's path is what would break it — and
        /// that is what is measured here, at the exact pixel a player would aim at.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_yard_never_stands_between_the_pointer_and_a_station()
        {
            yield return LoadCamp();

            Camera camera = _world.Camera;
            Assert.That(camera, Is.Not.Null, "the camp has no camera");

            for (int i = 0; i < _world.ShelfCount; i++)
            {
                Assert.That(_world.TryGetShelfScreenRect(i, out Rect station), Is.True, "shelf " + i + " is not on screen");
                Vector2 point = station.center;

                Assert.That(_world.ShelfAtScreenPoint(point, out _), Is.EqualTo(i),
                    "the click at shelf " + i + "'s centre no longer reaches it");
                AssertNoColliderOnTheRay(camera, point, "shelf " + i);
            }

            Assert.That(_world.TryGetResetScreenRect(out Rect drum), Is.True, "the drum is not on screen");
            Vector2 drumPoint = ClearDrumPoint(drum);
            Assert.That(_world.IsResetAtScreenPoint(drumPoint), Is.True, "the drum's own pixel does not reach it");
            AssertNoColliderOnTheRay(camera, drumPoint, "the drum");
        }

        /// <summary>
        /// A point inside the drum's rectangle that no shelf owns, so the camp's shelf-first click
        /// order cannot turn the drum's pixel into a rent.
        /// </summary>
        private Vector2 ClearDrumPoint(Rect drum)
        {
            for (float fx = 0.2f; fx <= 0.8f; fx += 0.1f)
            {
                for (float fy = 0.2f; fy <= 0.8f; fy += 0.1f)
                {
                    var candidate = new Vector2(
                        Mathf.Lerp(drum.xMin, drum.xMax, fx),
                        Mathf.Lerp(drum.yMin, drum.yMax, fy));

                    if (_world.IsResetAtScreenPoint(candidate) && _world.ShelfAtScreenPoint(candidate, out _) < 0)
                    {
                        return candidate;
                    }
                }
            }

            Assert.Fail("the drum has no pixel of its own");
            return drum.center;
        }

        /// <summary>Asserts that no collider stands on the camera ray through a screen point.</summary>
        private static void AssertNoColliderOnTheRay(Camera camera, Vector2 point, string what)
        {
            Ray ray = camera.ScreenPointToRay(point);
            RaycastHit[] hits = Physics.RaycastAll(ray, camera.farClipPlane);
            Assert.That(hits, Is.Empty,
                "something solid stands in front of " + what + " at " + point + ": " + hits.Length + " collider(s)");
        }

        /// <summary>
        /// A point inside the เซียมซี cylinder's screen rectangle that no shelf also covers.
        /// </summary>
        /// <remarks>
        /// The 2.5D sala draws its silhouettes over one another by design, and the camp's click order
        /// asks the shelves first — so a click meant for the drum has to be aimed at a pixel only the
        /// drum owns, exactly like a player aiming at the part of the cylinder that is not behind a
        /// rack.
        /// </remarks>
        private bool TryFindClearDrumPoint(out Vector2 point)
        {
            point = default;

            if (!_world.TryGetResetScreenRect(out Rect drum))
            {
                return false;
            }

            for (float fx = 0.15f; fx <= 0.85f; fx += 0.1f)
            {
                for (float fy = 0.15f; fy <= 0.85f; fy += 0.1f)
                {
                    var candidate = new Vector2(
                        Mathf.Lerp(drum.xMin, drum.xMax, fx),
                        Mathf.Lerp(drum.yMin, drum.yMax, fy));

                    if (_world.IsResetAtScreenPoint(candidate)
                        && _world.ShelfAtScreenPoint(candidate, out _) < 0)
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // input helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Holds the asked-for walking keys down for a wall-clock duration, then lets them up.
        /// </summary>
        /// <remarks>
        /// A duration, not a frame count: the view walks with <c>speed × Time.deltaTime</c>, so "hold
        /// W for 24 frames" means something different on every machine and says nothing about the
        /// camp. The key state is set once and left alone — a <c>KeyControl</c> reports held until it
        /// is released — so this is one device write per key, not one per frame.
        /// </remarks>
        private IEnumerator Hold(
            float seconds, bool up = false, bool down = false, bool left = false, bool right = false)
        {
            SetKey(_keyboard.wKey, up);
            SetKey(_keyboard.sKey, down);
            SetKey(_keyboard.aKey, left);
            SetKey(_keyboard.dKey, right);

            yield return Wait(seconds);

            SetKey(_keyboard.wKey, false);
            SetKey(_keyboard.sKey, false);
            SetKey(_keyboard.aKey, false);
            SetKey(_keyboard.dKey, false);
            yield return null;
        }

        /// <summary>Lets the player and the report settle for a wall-clock duration.</summary>
        private static IEnumerator Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Walks to a spot in the sala, one frame at a time: the test picks the keys from where the
        /// player is, so it depends on neither a frame rate nor a walking speed — only on the view
        /// actually moving the figure, which is the thing under test.
        /// </summary>
        private IEnumerator WalkTo(float x, float z)
        {
            const float Tolerance = 0.25f;
            float deadline = Time.realtimeSinceStartup + WalkBudgetSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                Vector3 at = _player.Position;
                float dx = x - at.x;
                float dz = z - at.z;

                if (Mathf.Abs(dx) <= Tolerance && Mathf.Abs(dz) <= Tolerance)
                {
                    SetKey(_keyboard.dKey, false);
                    SetKey(_keyboard.aKey, false);
                    SetKey(_keyboard.wKey, false);
                    SetKey(_keyboard.sKey, false);
                    yield return null;
                    yield break;
                }

                SetKey(_keyboard.dKey, dx > Tolerance);
                SetKey(_keyboard.aKey, dx < -Tolerance);
                SetKey(_keyboard.wKey, dz > Tolerance);
                SetKey(_keyboard.sKey, dz < -Tolerance);
                yield return null;
            }

            Assert.Fail("the player never reached (" + x + ", " + z + "); it is at " + _player.Position);
        }

        /// <summary>
        /// Walks up to one shelf: the nearest spot the player may stand in the shelf's own column.
        /// </summary>
        /// <remarks>
        /// The shelf anchor itself is <i>behind</i> the walk area's far edge — the furniture is not
        /// walkable — so walking at the anchor directly could never arrive. Clamping the column into
        /// the area gives a spot the walk area allows, and it is inside <c>constant.pickup_radius</c>
        /// of the shelf, which is what makes the shelf the focus.
        /// </remarks>
        private IEnumerator WalkToShelf(int index)
        {
            Vector3 standing = _world.ClampToWalkArea(_world.ShelfPosition(index));
            yield return WalkTo(standing.x, standing.z);
        }

        private void SetKey(KeyControl key, bool pressed)
        {
            if (pressed == key.isPressed)
            {
                return;
            }

            if (pressed)
            {
                Press(key);
            }
            else
            {
                Release(key);
            }
        }

        /// <summary>Presses and releases a key across frames, the way a player taps it.</summary>
        private IEnumerator PressKey(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        /// <summary>A full press-and-release of the left mouse button at one point.</summary>
        private IEnumerator Click(Vector2 point)
        {
            Set(_mouse.position, point);
            yield return null;

            Press(_mouse.leftButton);
            yield return null;

            Release(_mouse.leftButton);
            yield return null;
            yield return null;
        }
    }
}
