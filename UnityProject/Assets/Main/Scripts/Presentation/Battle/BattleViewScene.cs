using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Audio;
using ProjectSurvive.Game.Presentation.Battle.Environment;
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
        private readonly TelegraphPool _telegraphs;
        private readonly BattleAudioListener _audio;
        private readonly ArenaEnvironmentView _environment;

        private PlayerFigurePresenter _figure;

        private IBattleSession _session;
        private GameObject _floor;
        private MeshRenderer _floorMesh;
        private Mesh _floorQuad;
        private bool _disposed;

        /// <summary>Builds the view: art, ground, pool, bridge, camera, HUD wiring, VFX.</summary>
        /// <param name="playerFigure">
        /// The presenter that drives the scene's Spine figure (Stage 17b), or null in a fixture. It
        /// is attached here and begun with the floor, so "the figure is built at the floor's start"
        /// is one call site rather than each scene's own habit.
        /// </param>
        /// <param name="audio">
        /// The app's audio director, or null in a scene that has none (an EditMode fixture) — in
        /// which case the battle is silent and every other part of the view is unchanged.
        /// </param>
        public BattleViewScene(
            BattleViewSettings settings,
            BattleHudState hudState,
            BattleHudPresenter hud,
            BattleSpeed speed,
            uint visualSeed,
            PlayerFigurePresenter playerFigure = null,
            AudioDirector audio = null)
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

            // Stage 11b: the floor warnings a telegraphed attack publishes, drawn from the
            // event's own shape and footprint rather than re-derived from a def.
            _telegraphs = _owned.Own(new TelegraphPool(root, _art));

            // Stage 13c-1: the third channel the pump reserved in Stage 08. Null in a fixture
            // with no App scope, and a null listener is simply not registered.
            _audio = audio is null ? null : new BattleAudioListener(audio);

            // Stage 08e: the card forest, the lighting profile and the shadow layer. The scene owns
            // them; the view only ticks what has to move (the lantern and the shadows).
            _environment = settings.Environment;

            // This is the whole event-side subscription list: three cursors, one per channel.
            _events.AddListener(BattleEventChannel.Hud, _hud);
            _events.AddListener(BattleEventChannel.Vfx, _damage);
            _events.AddListener(BattleEventChannel.Vfx, _telegraphs);

            if (_audio is not null)
            {
                _events.AddListener(BattleEventChannel.Audio, _audio);
            }

            CreateFloor(root);

            // Warm every view before the first ghost exists, so the first wave cannot spike a
            // frame and "no Instantiate after warm-up" is measurable.
            _pool.Warmup();

            // Stage 17b: the doctor's figure is built here, with the pool it stands beside and before
            // the first tick — never mid-floor. It is not a pooled view: its own build is counted by
            // the figure and never by `viewsCreated` / `viewsGrownDuringPlay`.
            if (settings.PlayerFigure is not null && playerFigure is not null)
            {
                _figure = playerFigure;
                _figure.Attach(settings.PlayerFigure, _camera.BillboardPitch);

                bool began = _figure.Begin();
                _bridge.PlayerDrawnElsewhere = began;

                if (began)
                {
                    // The hurt reaction is a picture, so it rides the VFX channel: a burst of hits the
                    // HUD is still drawing cannot cost the figure its event, and a dropped one costs
                    // a flash and nothing else.
                    _events.AddListener(BattleEventChannel.Vfx, _figure);
                }
            }
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
        public Renderer FloorRenderer { get; private set; }

        /// <summary>The arena environment, when the scene wired one (Stage 08e).</summary>
        public ArenaEnvironmentView Environment => _environment;

        /// <summary>
        /// The generated sprites the view draws with. Exposed so the readability capture can draw the
        /// <i>real</i> pickup and telegraph art for its probes instead of a copy of it (Stage 08f).
        /// </summary>
        public BattlePlaceholderArt Art => _art;

        /// <summary>How many views are bound to an entity right now.</summary>
        public int BoundViews => _bridge.BoundCount;

        /// <summary>How many damage numbers are alive right now.</summary>
        public int ActiveDamageNumbers => _damage.ActiveCount;

        /// <summary>
        /// The presenter that drives the doctor's Spine figure, or null in a fixture (Stage 17b).
        /// Its own counters are separate from the pool's: the figure is one built actor, not a view.
        /// </summary>
        public PlayerFigurePresenter PlayerFigure => _figure;

        /// <summary>How many floor warnings are drawn right now (Stage 11b).</summary>
        public int ActiveTelegraphs => _telegraphs.ActiveCount;

        /// <summary>Cues the battle's events have asked the director to play (Stage 13c-1).</summary>
        public long CuesRequested => _audio?.CuesRequested ?? 0;

        /// <summary>
        /// Starts a floor: creates the session through the port, snapshots its first frame and
        /// fills the HUD's static half. This is the only place a session is created, and the
        /// factory is the only thing that knows which implementation that is.
        /// </summary>
        public IBattleSession Begin(
            IBattleSessionFactory factory, BattleSetup setup, int startingBia)
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
            _telegraphs.Clear();

            _hud.Begin(setup, startingBia);
            if (_settings.Hud is not null)
            {
                _settings.Hud.Bind();
                _settings.Hud.ShowTimeline(_hudState.Timeline);
            }

            _snapshot.CopyFrom(_session.Frame);
            _bridge.ApplySnapshot(_snapshot);
            _figure?.AcceptSnapshot(_snapshot);
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
            _figure?.AcceptSnapshot(_snapshot);

        }

        /// <summary>Drains this tick's events into the channels' own cursors.</summary>
        public void DrainEvents(IBattleEventSource source) => _events.Drain(source);

        /// <summary>
        /// Holds the doctor's animation still, or lets it run again. Called by the runner when its
        /// own pause changes — once per change, never per frame — and never by touching
        /// <c>Time.timeScale</c>.
        /// </summary>
        public void SetPaused(bool paused) => _figure?.SetPaused(paused);

        /// <summary>
        /// Draws the frame at <paramref name="alpha"/> between the last two ticks, and
        /// advances the effect pool.
        /// </summary>
        public void Render(float alpha, float deltaSeconds)
        {
            _bridge.Render(alpha);
            _figure?.Render(alpha, deltaSeconds);

            Vector3 player = PlayerPosition();
            _camera.Follow(player);

            // The lantern follows the doctor and every actor gets its blob shadow from the same
            // frame the bridge just drew (Stage 08e).
            _environment?.Tick(player, _snapshot.Current);

            _damage.Tick(deltaSeconds);
            _telegraphs.Tick(deltaSeconds);
        }

        /// <summary>Writes the end banner, parks the camera and lets a lost floor's doctor fall.</summary>
        public void End(BattleResult result)
        {
            _figure?.End(result);
            _hud.PresentEnd(result);
        }

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

            // The figure is the scene's own object and the scope's service: this only stops it being
            // driven, so a disposed view cannot be ticked by a late frame.
            _figure?.Detach();
            _figure = null;

            _owned.Dispose();

            if (_floor is not null)
            {
                DestroyObject(_floor);
                _floor = null;
                _floorMesh = null;
                FloorRenderer = null;
            }

            if (_floorQuad is not null)
            {
                DestroyObject(_floorQuad);
                _floorQuad = null;
            }
        }

        /// <summary>
        /// The floor. With an arena theme this is a mesh quad with the theme's flat colour +
        /// macro-noise material, at the opaque queue (2000) so it is drawn before the decals,
        /// shadows and actors that stand on it — and, since Stage 08h, writing <b>no</b> depth
        /// (ADR 0038): who hides whom in this arena is <see cref="BattleWorldMap.SortingOrder"/>'s
        /// job, and a depth-writing floor silently cut the lower half off every billboard, whose
        /// centre pivot puts that half below the plane at the camera's 50° tilt. Without a theme
        /// (a fixture) the Stage 08 sprite floor is the fallback, unchanged.
        /// </summary>
        private void CreateFloor(Transform root)
        {
            _floor = new GameObject("arena-floor");
            _floor.transform.SetParent(root, worldPositionStays: false);
            _floor.transform.SetPositionAndRotation(
                Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

            Material ground = _environment?.Theme?.GroundMaterial;
            if (ground is not null)
            {
                _floorQuad = ArenaGeometry.CreateCenteredQuad();
                _floorQuad.hideFlags = HideFlags.DontSave;

                _floor.AddComponent<MeshFilter>().sharedMesh = _floorQuad;
                _floorMesh = _floor.AddComponent<MeshRenderer>();
                _floorMesh.sharedMaterial = ground;
                _floorMesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _floorMesh.receiveShadows = false;
                _floor.transform.localScale = new Vector3(
                    BattleViewCaps.ArenaRadius * 2f, BattleViewCaps.ArenaRadius * 2f, 1f);
                FloorRenderer = _floorMesh;
                return;
            }

            var renderer = _floor.AddComponent<SpriteRenderer>();
            renderer.sprite = _art.Ground;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(BattleViewCaps.ArenaRadius * 2f, BattleViewCaps.ArenaRadius * 2f);
            renderer.sortingOrder = BattleWorldMap.GroundSortingOrder;
            renderer.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            FloorRenderer = renderer;
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
