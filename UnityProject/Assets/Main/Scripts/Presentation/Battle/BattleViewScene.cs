using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Battle.Vfx;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Everything a Battle scene shows: the generated art, the sprite pool, the render
    /// bridge, the floor, the camera, the HUD and the damage numbers — built once and fed one
    /// frame at a time by <see cref="BattleSimulationRunner"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The runner owns the clock and the session; this owns the pixels. Splitting them keeps
    /// the driver short and makes the view testable without a simulation: an EditMode test
    /// can construct one, hand it a snapshot and assert that a view was reused rather than
    /// created.
    /// </para>
    /// <para>
    /// It reads nothing but <see cref="IBattleFrame"/> and <see cref="IBattleEventSource"/>
    /// (acceptance: "Presentation อ่านเฉพาะ IBattleFrame / IBattleEventSource"). It never
    /// names a simulation type, which is what lets the scene run against the Stage 02b fake
    /// by changing one binding.
    /// </para>
    /// <para>
    /// One floor per scene visit in Stage 08, so <see cref="Begin"/> is called once; a later
    /// stage that reuses the scene for a second floor calls it again, and everything it
    /// builds is either idempotent (the floor, the pool) or replaced (the session).
    /// </para>
    /// </remarks>
    public sealed class BattleViewScene : IDisposable
    {
        private readonly BattleViewSettings _settings;
        private readonly BattleHudState _hudState;
        private readonly BattleHudPresenter _hud;
        private readonly BattleSpeed _speed;
        private readonly uint _visualSeed;
        private readonly BattleViewScope _owned = new BattleViewScope();
        private readonly BattleFrameSnapshot _snapshot = new BattleFrameSnapshot();
        private readonly BattleInputAdapter _input = new BattleInputAdapter();
        private readonly BattleEventPump _events = new BattleEventPump();
        private readonly BattlePlaceholderArt _art;
        private readonly EntityViewPool _pool;
        private readonly BattleRenderBridge _bridge;
        private readonly BattleCameraRig _camera;
        private readonly FloatingDamagePool _damage;

        private IBattleSession _session;
        private GameObject _floor;
        private bool _disposed;

        /// <summary>Builds the view: art, ground, pool, bridge, camera, HUD wiring, VFX.</summary>
        public BattleViewScene(
            BattleViewSettings settings,
            BattleHudState hudState,
            BattleHudPresenter hud,
            BattleSpeed speed,
            uint visualSeed)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _hudState = hudState ?? throw new ArgumentNullException(nameof(hudState));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _speed = speed ?? throw new ArgumentNullException(nameof(speed));
            _visualSeed = visualSeed;

            Transform root = settings.ViewRoot;
            if (root is null)
            {
                throw new ArgumentException("BattleViewSettings.ViewRoot is not wired.", nameof(settings));
            }

            if (settings.Camera is null)
            {
                throw new ArgumentException("BattleViewSettings.Camera is not wired.", nameof(settings));
            }

            _art = _owned.Own(new BattlePlaceholderArt());
            _pool = _owned.Own(new EntityViewPool(root, _art));
            _bridge = _owned.Own(new BattleRenderBridge(_pool, _art));
            _camera = new BattleCameraRig(settings.Camera, settings.Projection);
            _bridge.BillboardPitch = _camera.BillboardPitch;

            // The visual stream is derived from the run seed exactly like the shop and battle
            // streams, so a jitter cannot reach a rule and a replay cannot depend on how many
            // damage numbers were on screen (AGENTS rule 6).
            IRandomSource visual = new RandomStreams(_visualSeed).Visual;
            _damage = _owned.Own(new FloatingDamagePool(root, settings.Font, visual, _camera.BillboardPitch));

            // This is the whole event-side subscription list: three cursors, one per channel.
            _events.AddListener(BattleEventChannel.Hud, _hud);
            _events.AddListener(BattleEventChannel.Vfx, _damage);

            CreateFloor(root);

            // Warm every view before the first ghost exists, so the first wave cannot spike a
            // frame and "no Instantiate after warm-up" is measurable.
            _pool.Warmup();
        }

        /// <summary>The latest copied frame. Never the session's own spans.</summary>
        public BattleFrameSnapshot Snapshot => _snapshot;

        /// <summary>The scene's input source.</summary>
        public IBattleInputSource Input => _input;

        /// <summary>The 1×/2× control.</summary>
        public BattleSpeed Speed => _speed;

        /// <summary>The event pump, one cursor per consumer.</summary>
        public BattleEventPump Events => _events;

        /// <summary>The sprite pool's counters.</summary>
        public EntityViewPool Pool => _pool;

        /// <summary>The camera rig.</summary>
        public BattleCameraRig Camera => _camera;

        /// <summary>The layout of the floor the current session is running.</summary>
        public SpriteRenderer FloorRenderer { get; private set; }

        /// <summary>The mini bag, when the scene wired one.</summary>
        public BattleMiniBagView MiniBag => _settings.MiniBag;

        /// <summary>How many views are bound to an entity right now.</summary>
        public int BoundViews => _bridge.BoundCount;

        /// <summary>How many damage numbers are alive right now.</summary>
        public int ActiveDamageNumbers => _damage.ActiveCount;

        /// <summary>
        /// Starts a floor: creates the session through the port, snapshots its first frame and
        /// fills the HUD's static half. This is the only place a session is created, and the
        /// factory is the only thing that knows which implementation that is.
        /// </summary>
        public IBattleSession Begin(
            IBattleSessionFactory factory, BattleSetup setup, int startingBia,
            Backpack bag = null, IGameConfig config = null)
        {
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            _session?.Dispose();
            _session = factory.Create(setup);

            // The mini bag is the run's own bag, drawn once: the loadout the battle was handed is
            // what says which pieces can cast, so the plates and the cooldown list cannot disagree
            // (ADR 0024). A scene without a bag (a test's view, or ?scene=Battle before a run
            // exists) simply has nothing to draw.
            if (_settings.MiniBag is not null && bag is not null && config is not null)
            {
                _settings.MiniBag.Build(bag, setup.Loadout, config);
            }

            _hud.Begin(setup, startingBia);
            if (_settings.Hud is not null)
            {
                _settings.Hud.Bind();
                _settings.Hud.ShowTimeline(_hudState.Timeline);
            }

            _snapshot.CopyFrom(_session.Frame);
            _bridge.ApplySnapshot(_snapshot);
            _camera.SnapTo(PlayerPosition());
            _hud.Present(_snapshot);
            _events.Drain(_session.Events);

            return _session;
        }

        /// <summary>
        /// Copies the frame the last tick published. Must be called before the next
        /// <c>Step</c>/<c>Submit</c>: the spans are valid only until then.
        /// </summary>
        public void AcceptTick(IBattleFrame frame)
        {
            _snapshot.CopyFrom(frame);
            _bridge.ApplySnapshot(_snapshot);
            _hud.Present(_snapshot);

            // The plates come from the frame that was just copied, on this frame — the same span a
            // HUD label is read from, and never a clock of the mini bag's own (Stage 08b/08c).
            if (_settings.MiniBag is not null)
            {
                _settings.MiniBag.ApplyCooldowns(_snapshot.Cooldowns);
            }
        }

        /// <summary>Drains this tick's events into the channels' own cursors.</summary>
        public void DrainEvents(IBattleEventSource source) => _events.Drain(source);

        /// <summary>
        /// Draws the frame at <paramref name="alpha"/> between the last two ticks, and
        /// advances the effect pool.
        /// </summary>
        public void Render(float alpha, float deltaSeconds)
        {
            _bridge.Render(alpha);
            _camera.Follow(PlayerPosition());
            _damage.Tick(deltaSeconds);
            _settings.MiniBag?.RefreshIfResized();
        }

        /// <summary>Writes the end banner and parks the camera.</summary>
        public void End(BattleResult result) => _hud.PresentEnd(result);

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _session?.Dispose();
            _session = null;

            _owned.Dispose();

            if (_floor is not null)
            {
                DestroyObject(_floor);
                _floor = null;
                FloorRenderer = null;
            }
        }

        private void CreateFloor(Transform root)
        {
            _floor = new GameObject("arena-floor");
            _floor.transform.SetParent(root, worldPositionStays: false);
            _floor.transform.SetPositionAndRotation(
                Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

            FloorRenderer = _floor.AddComponent<SpriteRenderer>();
            FloorRenderer.sprite = _art.Ground;
            FloorRenderer.drawMode = SpriteDrawMode.Tiled;
            FloorRenderer.size = new Vector2(BattleViewCaps.ArenaRadius * 2f, BattleViewCaps.ArenaRadius * 2f);
            FloorRenderer.sortingOrder = BattleWorldMap.GroundSortingOrder;
            FloorRenderer.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        }

        /// <summary>
        /// Where the camera should look: the ghost-doctor's interpolated position, or the
        /// arena centre before the first frame of a session.
        /// </summary>
        private Vector3 PlayerPosition()
        {
            ReadOnlySpan<RenderEntity> current = _snapshot.Current;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].Kind == RenderEntityKind.Player)
                {
                    return BattleWorldMap.ToWorld(current[i]);
                }
            }

            return Vector3.zero;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
