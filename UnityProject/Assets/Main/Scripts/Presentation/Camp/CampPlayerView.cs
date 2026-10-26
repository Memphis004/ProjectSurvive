using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// Walks the player around ศาลาพัก and turns a key or a click into the run's own shop command.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 09c work items 1, 2, 3, 4 and 6 in one place: WASD moves the figure, the nearest shelf
    /// station or the เซียมซี cylinder within reach becomes the focus, <b>E</b> acts on the focus, and
    /// a <b>click</b> acts on whatever was clicked — without walking there first, which is what makes
    /// the screen usable on a phone and what a browser test can drive.
    /// </para>
    /// <para>
    /// It decides nothing about the game. ไปเช่า is <see cref="CampPresenter.Rent"/> — the same use
    /// case Stage 06 exposed, refusing for the same reasons — and a refusal is the presenter's
    /// status line, which the bottom bar already shows (AGENTS rule 10: a view computes no price and
    /// no rule).
    /// </para>
    /// <para>
    /// Both numbers this class moves with are the run's own tuning: the walking speed is
    /// <c>constant.move_speed</c> and how close counts as close enough is
    /// <c>constant.pickup_radius</c>, read from <see cref="IGameConfig"/> rather than written here
    /// (AGENTS rule 5).
    /// </para>
    /// <para>
    /// <b>While the จัดย่าม panel is open the figure stands still</b> (work item 5) and no key or
    /// click reaches the shop: the panel is the screen then, and it has its own rotate/undo/restore
    /// controls. Standing still is also why the doctor is drawn <b>idle</b> then.
    /// </para>
    /// <para>
    /// <b>The doctor is the Spine figure</b> (Stage 17c): every frame the walk this class already
    /// performs is handed to <see cref="IPlayerFigure.Present"/> — the pose from the distance the
    /// walk actually covered, the facing from the direction it was asked for — through the pure
    /// <see cref="PlayerPoseSelector"/>. No threshold, no speed and no animation name is written here:
    /// the profile owns them, and this view decides nothing (AGENTS rule 10). There is no hurt and no
    /// death in the sala.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CampPlayerView : MonoBehaviour
    {
        /// <summary>
        /// How far a frame's movement has to lean sideways before the figure turns. A twitch of
        /// float noise, not a threshold: whether the doctor is walking at all is the pose rule's
        /// call, and the facing follows a direction that was already asked for.
        /// </summary>
        private const float FacingDeadZone = 0.0001f;

        [SerializeField] private LifetimeScope _scope;
        [SerializeField] private CampWorldView _world;
        [SerializeField] private CampBagScreenView _bag;

        private IGameConfig _config;
        private CampPresenter _presenter;
        private Vector3 _position;
        private bool _bound;
        private bool _moving;

        /// <summary>Where the player is (world space).</summary>
        public Vector3 Position => _position;

        /// <summary>True while a walking key is held down.</summary>
        public bool Moving => _moving;

        /// <summary>The station the player is standing at, or -1.</summary>
        public int FocusedShelf => _world == null ? -1 : _world.FocusedShelf;

        /// <summary>True while the player stands at the เซียมซี cylinder.</summary>
        public bool ResetFocused => _world != null && _world.ResetFocused;

        /// <summary>True while the จัดย่าม panel is open, which stops the figure.</summary>
        public bool WalkingBlocked => _bag != null && _bag.PanelVisible;

        /// <summary>The last command this view asked for, as a short code a test reads.</summary>
        public string LastAction { get; private set; } = string.Empty;

        /// <summary>How many rents this view asked for (the presenter counts the accepted ones).</summary>
        public int RentRequests { get; private set; }

        /// <summary>How many lock toggles this view asked for.</summary>
        public int LockRequests { get; private set; }

        /// <summary>How many เซียมซี casts this view asked for.</summary>
        public int ResetRequests { get; private set; }

        private void Awake() => _position = transform.position;

        /// <summary>Resolves the run and the sala. Idempotent.</summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            if (_scope == null || _scope.Container == null)
            {
                Debug.LogError("CampPlayerView needs a built scope to resolve the run.", this);
                return;
            }

            // Stage 09j: resolved first, bound last. `_bound = true` used to be set before the two
            // resolves, so a container that had already been disposed threw out of this method and
            // left the view running with a null config — one NullReferenceException every frame from
            // `UpdateFocus` (measured: the reported stack, CampPlayerView.cs:202, thousands of them).
            // A failed bind now leaves the view unbound and quiet instead.
            IGameConfig config = _scope.Container.Resolve<IGameConfig>();
            CampPresenter presenter = _scope.Container.Resolve<CampPresenter>();

            if (config == null || presenter == null)
            {
                Debug.LogError("CampPlayerView could not resolve the run from its scope.", this);
                return;
            }

            _config = config;
            _presenter = presenter;
            _bound = true;

            if (_world != null && _world.PlayerTransform != null)
            {
                _position = _world.PlayerTransform.position;
            }
        }

        private void Update()
        {
            if (!_bound || _config == null)
            {
                return;
            }

            Walk();
            UpdateFocus();
            UpdateHover();

            if (WalkingBlocked)
            {
                // The panel is the screen while it is open: no E, no click into the shop behind it.
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ActivateNearest();
            }

            ReadClick();
        }

        // ------------------------------------------------------------------
        // walking
        // ------------------------------------------------------------------

        /// <summary>
        /// Puts the figure at <paramref name="position"/> without walking there, clamped into the
        /// sala exactly like a walk. Only the capture tool and a test use it: a player never
        /// teleports, but a screenshot of "the player standing at shelf 3" has to start from that
        /// spot rather than from a two-second key hold.
        /// </summary>
        public void Teleport(Vector3 position)
        {
            _position = _world == null ? position : _world.ClampToWalkArea(position);

            if (_world != null && _world.PlayerTransform != null)
            {
                _world.PlayerTransform.position = _position;
            }
        }

        /// <summary>
        /// WASD on the ground plane, camera-relative: the camera looks along +Z, so W walks away
        /// from the player and into the sala.
        /// </summary>
        private void Walk()
        {
            if (_world == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && !WalkingBlocked)
            {
                if (keyboard.wKey.isPressed)
                {
                    input.y += 1f;
                }

                if (keyboard.sKey.isPressed)
                {
                    input.y -= 1f;
                }

                if (keyboard.dKey.isPressed)
                {
                    input.x += 1f;
                }

                if (keyboard.aKey.isPressed)
                {
                    input.x -= 1f;
                }
            }

            _moving = input.sqrMagnitude > 0f;
            Vector3 from = _position;

            if (_moving)
            {
                input = input.normalized;
                float speed = _config.Constant.MoveSpeed;
                _position += new Vector3(input.x, 0f, input.y) * (speed * Time.deltaTime);
            }

            _position = _world.ClampToWalkArea(_position);

            if (_world.PlayerTransform != null)
            {
                _world.PlayerTransform.position = _position;
            }

            PresentFigure(from, input);
        }

        /// <summary>
        /// Draws the frame the walk just produced: run while the doctor covers ground, idle while
        /// they do not, facing the way the keys asked for.
        /// </summary>
        /// <remarks>
        /// The speed is the distance the walk <b>left</b> after the sala's own clamp, divided by the
        /// frame time — so a player pressing W into the far edge of the walk area stands rather than
        /// running on the spot, and no second copy of "how fast is walking" exists. The thresholds
        /// come from the figure's own profile through <see cref="PlayerPoseSelector"/>, and
        /// <see cref="IPlayerFigure.Present"/> writes nothing when the pose and the facing are the
        /// ones already drawn.
        /// </remarks>
        private void PresentFigure(Vector3 from, Vector2 direction)
        {
            IPlayerFigure figure = _world == null ? null : _world.PlayerFigure;
            if (figure == null || !figure.IsBegun)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float speed = deltaTime > 0f ? Vector3.Distance(from, _position) / deltaTime : 0f;

            float threshold = figure.RunSpeedThreshold;

            PlayerPose pose = PlayerPoseSelector.Select(speed, figure.Pose, false, threshold);
            int facing = PlayerPoseSelector.Facing(direction.x, figure.FaceDirection, FacingDeadZone);

            figure.Present(pose, facing);

            // The view owns no clock: this is the one thing that advances the animation state (its
            // component runs at a zero time scale, so Unity's frame time cannot animate it twice).
            figure.Tick(deltaTime);
        }

        // ------------------------------------------------------------------
        // focus
        // ------------------------------------------------------------------

        /// <summary>
        /// Highlights whatever is in reach, nearest first: two stations can both be within the
        /// radius, and \"the one you are standing at\" has to mean something a test can predict.
        /// </summary>
        private void UpdateFocus()
        {
            if (_world == null || _config == null)
            {
                return;
            }

            float radius = _config.Constant.PickupRadius;
            int nearest = -1;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < _world.ShelfCount; i++)
            {
                float distance = Planar(_position, _world.ShelfPosition(i));
                if (distance <= radius && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            float resetDistance = Planar(_position, _world.ResetPosition);
            bool atReset = resetDistance <= radius && resetDistance < nearestDistance;

            if (atReset)
            {
                nearest = -1;
            }

            _world.SetFocusedShelf(nearest);
            _world.SetResetFocused(atReset);

            // Stage 09n: the store the player is standing at explains itself on its own board. The
            // presenter early-returns on an unchanged station, so this costs one comparison a frame.
            _presenter.FocusOffer(nearest);
        }

        /// <summary>
        /// Tells the presenter which shelf the pointer is over, using the <b>same</b> hit test the
        /// click uses (Stage 09n: "ห้ามใช้ hit test ที่สอง").
        /// </summary>
        /// <remarks>
        /// The pointer is only <i>read</i> here; the presenter decides whether a tooltip is allowed
        /// (a drag, the chooser or the จัดย่าม panel all suppress it). A pointer over the pin control or
        /// over the screen's own UI states no shelf at all: hovering ล็อกไว้ must not describe the
        /// product, and a button's hover belongs to the button.
        /// </remarks>
        private void UpdateHover()
        {
            int station = -1;
            Mouse mouse = Mouse.current;

            if (mouse != null && _world != null && !WalkingBlocked
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Vector2 point = mouse.position.ReadValue();
                int hit = _world.ShelfAtScreenPoint(point, out bool onTheLock);

                if (hit >= 0 && !onTheLock)
                {
                    station = hit;
                }
            }

            _presenter.HoverOffer(station);
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt((dx * dx) + (dz * dz));
        }

        // ------------------------------------------------------------------
        // what E and a click do
        // ------------------------------------------------------------------

        private void ActivateNearest()
        {
            if (ResetFocused)
            {
                CastSticks();
                return;
            }

            if (FocusedShelf >= 0)
            {
                Rent(FocusedShelf);
            }
        }

        /// <summary>
        /// A left click acts on what it landed on, wherever the player is standing (work item 6).
        /// </summary>
        /// <remarks>
        /// A click that landed on the UI belongs to the UI: without this the จัดย่าม button's own
        /// click would also rent whatever shelf happens to be behind it.
        /// </remarks>
        private void ReadClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || _world == null)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 point = mouse.position.ReadValue();
            int station = _world.ShelfAtScreenPoint(point, out bool onTheLock);

            if (station >= 0)
            {
                if (onTheLock)
                {
                    ToggleLock(station);
                }
                else
                {
                    Rent(station);
                }

                return;
            }

            if (_world.IsResetAtScreenPoint(point))
            {
                CastSticks();
            }
        }

        private void Rent(int station)
        {
            RentRequests++;
            LastAction = "rent:" + station;
            _presenter.Rent(station);
        }

        private void ToggleLock(int station)
        {
            LockRequests++;
            LastAction = "lock:" + station;
            _presenter.ToggleSlotLock(station);
        }

        private void CastSticks()
        {
            ResetRequests++;
            LastAction = "reset";
            _presenter.RefreshShop();
        }
    }
}
