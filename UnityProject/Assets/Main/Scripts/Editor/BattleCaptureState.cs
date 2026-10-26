using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>Which moment of a floor the capture command is photographing.</summary>
    public enum BattleShot
    {
        /// <summary>The first frame of the floor: nothing has happened yet.</summary>
        FloorStart = 0,

        /// <summary>Six seconds in: HP spent, ghosts slain, the casters' cooldowns at several levels.</summary>
        MidFight = 1,

        /// <summary>
        /// A caster that is ready and cannot pay for its cast — the frame's own
        /// <c>LoadoutCooldownFlags.ManaShort</c>, which is what the faint blue plate means.
        /// </summary>
        ManaShort = 2,

        /// <summary>
        /// The last frame before Pob possesses the player: phase 2, the boss in reach, the controls
        /// still normal (Stage 11c).
        /// </summary>
        BossBeforePossession = 3,

        /// <summary>
        /// The frame a possession landed on: the status panel says the controls are reversed
        /// (Stage 11c).
        /// </summary>
        BossPossessed = 4,
    }

    /// <summary>
    /// Drives a Battle scene's HUD from the <b>dev battle</b> so the capture command can photograph
    /// a floor that is really running, with the numbers the fight really published.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why this exists. <c>UiCaptureTool</c> photographs a scene's <i>authored</i> geometry, and that
    /// is enough for the camp, the title and the summary — but the Battle HUD is nearly all runtime
    /// state: the mini ย่าม is the run's own bag, the cooldown plates come from a frame, and the
    /// labels are written by the presenter. A shot of the authored scene would show empty panels and
    /// prove nothing about Stage 08c. So this helper plays the screen the way the runner does:
    /// depart a real run on the real tables, create the dev session through
    /// <c>DevBattleSessionBinding</c> (the port, exactly as <c>BattleSceneScope</c> does — no Unity
    /// file may name the fake's type), step it to each state's tick, and hand the frame it published
    /// to the same objects the scene uses.
    /// </para>
    /// <para>
    /// <b>No string is invented here.</b> Every label on the screen arrives over
    /// <see cref="BattleHudState"/>, written by the real <see cref="BattleHudPresenter"/> from the
    /// localization table — the same values <c>BattleHudView.Bind</c> would have mirrored. The
    /// view's own <c>Bind</c> cannot run here because the scene scope has no built container outside
    /// play mode, so this class copies each property into the label the view serializes, which is the
    /// whole of what <c>Bind</c> does for a string. Nothing else in the game is changed, and the
    /// scene is never saved.
    /// </para>
    /// <para>
    /// <b>The MP-short state is authored, and says so.</b> Neither session reaches
    /// <c>ManaShort</c> on floor 1's data: the dev session's pool never falls below a single cast's
    /// cost (its MP is static), and the Editor assembly is not allowed to name the EcsLite session to
    /// reach the one that spends MP. So for that one shot the tool hands the mini bag a real
    /// <see cref="LoadoutCooldown"/> carrying the flag the simulation sets when a ready cast cannot
    /// be paid for (<c>FramePublishSystem</c>). What the picture proves is that the tone is
    /// <i>visible and distinguishable</i> at both sizes; that the flag reaches the plate is proved
    /// by <c>BattleMiniBagTests</c> and by the PlayMode suite, and the report repeats this sentence.
    /// </para>
    /// </remarks>
    public sealed class BattleCaptureState : IDisposable
    {
        /// <summary>The seed the run is departed on. The scene's own default, so the bag is a real one.</summary>
        public const uint Seed = 20261013;

        /// <summary>Six seconds at 30 Hz: far enough into the floor to have taken and dealt damage.</summary>
        private const int MidFightTicks = 180;

        /// <summary>
        /// How long to keep looking for a mid-fight frame whose casters are at several different
        /// cooldowns — the picture the stage asks for. The search is bounded so a floor that never
        /// produces one still gets photographed rather than hanging.
        /// </summary>
        private const int MidFightCeiling = 1500;

        /// <summary>
        /// How many distinct cooldown levels the mid-fight search aims for, never more than the floor
        /// has casters: two weapons can show two levels and no more, and a search that kept asking for
        /// a third would walk a floor to its end chasing something that does not exist.
        /// </summary>
        private const int LevelsWanted = 3;

        /// <summary>
        /// Ticks the boss capture may play before it gives up: the boss floor is 90 s, and the boss has
        /// to walk in and be hurt down to its phase-2 threshold inside that.
        /// </summary>
        private const int BossCeiling = 2700;

        /// <summary>
        /// Where each string the HUD shows lands in the scene: the field <c>BattleHudView</c>
        /// serializes, and the state property the presenter wrote it into.
        /// </summary>
        private static readonly (string Field, Func<BattleHudState, string> Value)[] Labels =
        {
            ("_floorLabel", state => state.FloorName.Value),
            ("_floorIndexLabel", state => state.FloorIndex.Value),
            ("_hpCaption", state => state.HpCaption.Value),
            ("_hpValue", state => state.HpValue.Value),
            ("_manaCaption", state => state.ManaCaption.Value),
            ("_manaValue", state => state.ManaValue.Value),
            ("_biaCaption", state => state.BiaCaption.Value),
            ("_biaValue", state => state.BiaValue.Value),
            ("_timeCaption", state => state.TimeCaption.Value),
            ("_timeValue", state => state.TimeValue.Value),
            ("_waveCaption", state => state.WaveCaption.Value),
            ("_waveValue", state => state.WaveValue.Value),
            ("_killsCaption", state => state.KillsCaption.Value),
            ("_killsValue", state => state.KillsValue.Value),
            ("_bagCaption", state => state.BagCaption.Value),
            ("_statusCaption", state => state.StatusCaption.Value),
            ("_timelineCaption", state => state.TimelineCaption.Value),
            ("_bossCaption", state => state.BossCaption.Value),
            ("_possessionLabel", state => state.PossessionText.Value),
            ("_speedLabel", state => state.SpeedLabel.Value),
        };

        private readonly BattleMiniBagView _miniBag;
        private readonly BattleTimelineView _timeline;
        private readonly RectTransform _hpBar;
        private readonly RectTransform _manaBar;
        private readonly GameObject _bossRoot;
        private readonly RectTransform _possessionLabel;
        private readonly SerializedObject _view;

        private readonly IGameConfig _config;
        private readonly RunSession _run;
        private readonly BattleSetup _setup;
        private readonly IBattleSession _session;
        private readonly BattleFrameSnapshot _beforePossession = new BattleFrameSnapshot();
        private readonly LocalizationService _text;
        private readonly BattleSpeed _speed = new BattleSpeed();
        private readonly BattleHudState _state = new BattleHudState();
        private readonly BattleHudPresenter _presenter;
        private readonly BattleFrameSnapshot _first = new BattleFrameSnapshot();
        private readonly BattleFrameSnapshot _current = new BattleFrameSnapshot();
        private readonly LoadoutCooldown[] _manaShort = new LoadoutCooldown[BattleConstants.MaxLoadoutEntries];

        private PlayerFigurePresenter _figure;

        private GameObject _floor;
        private Mesh _floorQuad;

        private int _steppedTo;
        private int _manaShortIndex = -1;
        private bool _disposed;

        private BattleCaptureState(
            BattleHudView hud,
            BattleMiniBagView miniBag,
            IGameConfig config,
            RunSession run,
            BattleSetup setup,
            int startingBia,
            IBattleSessionFactory factory)
        {
            _view = new SerializedObject(hud);
            _config = config;
            _run = run;
            _setup = setup;
            _miniBag = miniBag;
            _text = new LocalizationService(config);

            _timeline = _view.FindProperty("_timeline")?.objectReferenceValue as BattleTimelineView;
            _hpBar = _view.FindProperty("_hpBar")?.objectReferenceValue as RectTransform;
            _manaBar = _view.FindProperty("_manaBar")?.objectReferenceValue as RectTransform;
            _bossRoot = _view.FindProperty("_bossRoot")?.objectReferenceValue as GameObject;
            var possessionText = _view.FindProperty("_possessionLabel")?.objectReferenceValue as Component;
            _possessionLabel = possessionText == null ? null : possessionText.transform as RectTransform;

            _presenter = new BattleHudPresenter(_state, _text, config, _speed);
            _session = factory.Create(setup);

            // The arena floor, drawn the way BattleViewScene draws it. It is built at runtime in the
            // game, so a photograph taken in Edit Mode has no ground at all and the doctor is read
            // against the camera's clear colour instead of the olive tile the floor really is —
            // which is the difference between "he stands out" and "he does not".
            BuildFloor();

            // The floor's static half first (floor name, timeline, captions), then its first frame —
            // the order BattleViewScene.Begin uses. That frame is kept aside: it is the floor-start
            // shot, and it cannot be stepped back to once the other shots have run.
            _presenter.Begin(setup, startingBia);
            _first.CopyFrom(_session.Frame);
            _current.CopyFrom(_session.Frame);

            _miniBag.Build(run.Backpack, setup.Loadout, config);
        }

        /// <summary>Builds a capture state over a Battle scene, or explains why it cannot.</summary>
        /// <param name="hud">The scene's HUD view (its serialized fields are the labels).</param>
        /// <param name="miniBag">The scene's mini ย่าม.</param>
        /// <param name="state">The prepared state, or null on failure.</param>
        /// <param name="error">Why it refused, or an empty string.</param>
        public static bool TryBuild(
            BattleHudView hud, BattleMiniBagView miniBag, out BattleCaptureState state, out string error)
        {
            state = null;
            error = string.Empty;

            if (hud is null || miniBag is null)
            {
                error = "the Battle scene is missing its HUD or its mini bag";
                return false;
            }

            IGameConfig config = ResourcesGameConfigLoader.Load();
            var run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), Seed);

            if (!BattleRunSource.TryStartFrom(
                    run, config, equipSecondWeapon: true,
                    out BattleSetup setup, out int startingBia, out string departure))
            {
                error = "the run refused to depart: " + departure;
                return false;
            }

            state = new BattleCaptureState(
                hud, miniBag, config, run, setup, startingBia, DevBattleSessionBinding.CreateFactory(config));
            return true;
        }

        /// <summary>
        /// Builds a capture state over <b>floor 1 played by the shipped simulation</b> (Stage 17b).
        /// </summary>
        /// <remarks>
        /// The dev session ignores the player's input — it publishes one still doctor — so a picture of
        /// the figure <i>walking</i> cannot come from it. This is the same floor the HUD shots show
        /// (same seed, same departure, same bag), stepped by the battle the game really runs: the
        /// figure's pose, facing and position on the picture are therefore the floor's own, not a
        /// scenario's.
        /// </remarks>
        public static bool TryBuildRealFloorOne(
            BattleHudView hud, BattleMiniBagView miniBag, out BattleCaptureState state, out string error)
        {
            state = null;
            error = string.Empty;

            if (hud is null || miniBag is null)
            {
                error = "the Battle scene is missing its HUD or its mini bag";
                return false;
            }

            IGameConfig config = ResourcesGameConfigLoader.Load();
            var run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), Seed);

            if (!BattleRunSource.TryStartFrom(
                    run, config, equipSecondWeapon: true,
                    out BattleSetup setup, out int startingBia, out string departure))
            {
                error = "the run refused to depart: " + departure;
                return false;
            }

            state = new BattleCaptureState(
                hud, miniBag, config, run, setup, startingBia, BattleFactoryBinding.Create(config));
            return true;
        }

        /// <summary>
        /// Builds the same capture state over <b>floor 5</b>, played by the shipped simulation
        /// (Stage 11c).
        /// </summary>
        /// <remarks>
        /// The dev session cannot stand in for this one: a boss's phases, its corpse eating and its
        /// possession are rules of the real EcsLite battle, and a picture of them has to come from
        /// it. The run reaches floor 5 the way a player does — four cleared floors, each settled
        /// through <see cref="RunSession.ApplyBattleResult"/> with a win — so the bag, the loadout and
        /// the floor are the run's own rather than a scenario's.
        /// </remarks>
        public static bool TryBuildBoss(
            BattleHudView hud, BattleMiniBagView miniBag, out BattleCaptureState state, out string error)
        {
            state = null;
            error = string.Empty;

            if (hud is null || miniBag is null)
            {
                error = "the Battle scene is missing its HUD or its mini bag";
                return false;
            }

            IGameConfig config = ResourcesGameConfigLoader.Load();

            // The Editor is the Editor: this is exactly the decision the App scope makes at startup
            // ("dev cheats belong to a development build and to the Editor"), made here because a
            // capture runs in Edit Mode, where nothing has configured it yet. Without it the boss
            // floor kills the idle player before the fight reaches phase 2, and no possession can be
            // photographed.
            DevCheats.Configure(isDevelopmentBuild: false, isEditor: true);

            var run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), Seed);

            Result started = run.StartRun();
            if (started.IsFailure)
            {
                error = "the run refused to start: " + started.Error;
                return false;
            }

            BattleSetup setup = null;

            for (int floor = 0; floor < 5; floor++)
            {
                Result<BattleSetup> departed = run.StartFloor();
                if (departed.IsFailure)
                {
                    error = "floor " + (floor + 1) + " refused to depart: " + departed.Error;
                    return false;
                }

                setup = departed.Value;
                if (floor == 4)
                {
                    break;
                }

                // A win is the only way past a floor, and the settlement is what moves the run on.
                Result<FloorSettlement> settled = run.ApplyBattleResult(Win(setup));
                if (settled.IsFailure)
                {
                    error = "floor " + (floor + 1) + " refused its settlement: " + settled.Error;
                    return false;
                }
            }

            if (setup is null || !setup.IsBossFloor)
            {
                error = "the run did not reach a boss floor";
                return false;
            }

            state = new BattleCaptureState(
                hud, miniBag, config, run, setup, run.Wallet.Bia, BattleFactoryBinding.Create(config));
            return true;
        }

        /// <summary>An untouched-floor win, shaped so the settlement can validate it.</summary>
        private static BattleResult Win(BattleSetup setup)
        {
            return new BattleResult
            {
                Outcome = BattlePhase.Won,
                EndReason = setup.IsBossFloor
                    ? BattleEndReason.BossDefeated
                    : BattleEndReason.SurvivedAllWaves,
                SurvivedTicks = 1,
                FinalHp = setup.CurrentHp,
                CurrencyEarned = 0,
                Kills = 0,
                RemainingConsumables = new int[setup.Loadout.Consumables.Count],
            };
        }

        /// <summary>The tick the mid-fight shot was taken at.</summary>
        public int MidFightTick { get; private set; }

        /// <summary>The floor this state is playing.</summary>
        public string StageId => _setup.StageId;

        /// <summary>The tick a possession landed on, or -1 when the floor never reached one (Stage 11c).</summary>
        public int BossPossessionTick { get; private set; } = -1;

        /// <summary>Ticks the last frame says the controls are still reversed (Stage 11c).</summary>
        public int ControlFlipTicks => _current.HasFrame ? _current.Hud.ControlFlipTicks : 0;

        /// <summary>The status panel's possession line, for a capture that measures what it drew.</summary>
        public RectTransform PossessionLabel => _possessionLabel;

        /// <summary>The boss's HP on the frame the possession landed on (Stage 11c).</summary>
        public int BossHp => _current.HasFrame ? _current.Hud.BossHp : 0;

        /// <summary>The boss's max HP, for the report's "x / y" line.</summary>
        public int BossMaxHp => _current.HasFrame ? _current.Hud.BossMaxHp : 0;

        /// <summary>How many casters the loadout resolved (the pieces with a plate).</summary>
        public int CasterCount => _current.CooldownCount;

        /// <summary>How many distinct cooldown levels the mid-fight frame showed (Stage 08c's "หลายระดับ").</summary>
        public int DistinctLevels { get; private set; }

        /// <summary>The instance id whose plate the MP-short shot flags, or 0 when there was none to flag.</summary>
        public int ManaShortInstanceId { get; private set; }

        /// <summary>
        /// Whether the floor shows the boss band. False for every floor the run departs on today, so the
        /// capture command does not measure a rectangle floor 1 does not draw.
        /// </summary>
        /// <remarks>
        /// Read from the frame the floor published — the same value the presenter writes into the HUD —
        /// rather than from the HUD, which is only written once a state has been applied.
        /// </remarks>
        public bool HasVisibleBoss => _current.HasFrame && _current.Hud.HasBoss;

        /// <summary>Steps the dev session forward to <paramref name="ticks"/> and copies the frame.</summary>
        public void StepTo(int ticks)
        {
            while (_session.Tick < ticks && _session.Phase == BattlePhase.Running)
            {
                _session.Step(PlayerInputFrame.Idle);
                _steppedTo = (int)_session.Tick;
            }

            _current.CopyFrom(_session.Frame);
        }

        /// <summary>
        /// Steps the floor until its casters sit at <see cref="LevelsWanted"/> different cooldown
        /// levels (or the search's ceiling), and records where that landed.
        /// </summary>
        public void StepToMidFight()
        {
            int casters = _current.CooldownCount;
            int wanted = casters < LevelsWanted ? casters : LevelsWanted;

            StepTo(MidFightTicks);

            while (_steppedTo < MidFightCeiling && _session.Phase == BattlePhase.Running)
            {
                if (DistinctCooldownLevels() >= wanted)
                {
                    break;
                }

                StepTo(_steppedTo + 1);
            }

            MidFightTick = _steppedTo;
            DistinctLevels = DistinctCooldownLevels();

            PickManaShortInstance();
        }

        /// <summary>
        /// Plays the boss floor until Pob's possession lands (Stage 11c).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Nothing is staged: the floor is floor 5 as the run departs it, the boss walks in on its own,
        /// the player's weapon is the one the run's bag resolved, and the possession is the rule's. The
        /// tool only does two things a player would: it walks toward the boss (read from the frame's own
        /// <c>RenderEntityFlags.Boss</c>), and it turns <c>DevGodMode</c> on so the photograph is not a
        /// race between the shutter and the player's death — a dev command the Editor build allows, and
        /// it changes nothing about the boss's states.
        /// </para>
        /// <para>
        /// The frame before the reversal is kept, so the pair of pictures differs by the possession
        /// itself rather than by a second run.
        /// </para>
        /// </remarks>
        public void StepToBossPossession()
        {
            BossPossessionTick = -1;
            GodModeResult = _session.Submit(new BattleCommand(BattleCommandKind.DevGodMode, 1));
            GodModeAccepted = GodModeResult == CommandResult.Ok;
            _current.CopyFrom(_session.Frame);
            _beforePossession.CopyFrom(_session.Frame);

            while (_steppedTo < BossCeiling && _session.Phase == BattlePhase.Running)
            {
                _beforePossession.CopyFrom(_session.Frame);
                _session.Step(SteerToBoss());
                _steppedTo = (int)_session.Tick;
                _current.CopyFrom(_session.Frame);

                if (_current.Hud.ControlFlipTicks > 0)
                {
                    BossPossessionTick = _steppedTo;
                    BossDistance = DistanceToBoss();
                    return;
                }
            }

            Debug.Log(
                "[Stage 11c] boss capture stopped at tick " + _steppedTo + " (phase " + _session.Phase
                + ", floor " + _setup.FloorIndex + "): boss " + _current.Hud.BossHp + "/"
                + _current.Hud.BossMaxHp + ", player " + _current.Hud.Hp + "/" + _current.Hud.MaxHp
                + ", god mode " + GodModeResult + ", kills " + _current.Hud.Kills);
        }

        /// <summary>
        /// How far the boss stood from the player on the frame the possession landed, in world
        /// units — the proof that both are in the same camera shot.
        /// </summary>
        public float BossDistance { get; private set; } = -1f;

        /// <summary>Distance between the player and the boss in the last frame, or -1.</summary>
        private float DistanceToBoss()
        {
            if (!FindPlayerAndBoss(out float playerX, out float playerY, out float bossX, out float bossY))
            {
                return -1f;
            }

            float dx = bossX - playerX;
            float dy = bossY - playerY;
            return (float)Math.Sqrt((dx * dx) + (dy * dy));
        }

        /// <summary>The last frame's player and boss positions, when it showed both.</summary>
        private bool FindPlayerAndBoss(out float playerX, out float playerY, out float bossX, out float bossY)
        {
            playerX = 0f;
            playerY = 0f;
            bossX = 0f;
            bossY = 0f;

            if (!_current.HasFrame)
            {
                return false;
            }

            bool sawPlayer = false;
            bool sawBoss = false;

            ReadOnlySpan<RenderEntity> entities = _current.Current;
            for (int i = 0; i < entities.Length; i++)
            {
                RenderEntity entity = entities[i];
                if (entity.Kind == RenderEntityKind.Player)
                {
                    playerX = entity.X;
                    playerY = entity.Y;
                    sawPlayer = true;
                }
                else if ((entity.Flags & RenderEntityFlags.Boss) != 0)
                {
                    bossX = entity.X;
                    bossY = entity.Y;
                    sawBoss = true;
                }
            }

            return sawPlayer && sawBoss;
        }

        /// <summary>What the floor said to <c>DevGodMode</c>, for the capture's own report.</summary>
        public CommandResult GodModeResult { get; private set; }

        /// <summary>True when the floor accepted <c>DevGodMode</c>.</summary>
        public bool GodModeAccepted { get; private set; }

        /// <summary>
        /// The input for one tick: straight at the boss when the frame shows one, still otherwise.
        /// </summary>
        private PlayerInputFrame SteerToBoss()
        {
            if (!FindPlayerAndBoss(out float playerX, out float playerY, out float bossX, out float bossY))
            {
                return PlayerInputFrame.Idle;
            }

            return PlayerInputFrame.FromAxis(bossX - playerX, bossY - playerY);
        }

        /// <summary>
        /// Builds the scene's own Spine figure and drives it from the frame the shots are taken on
        /// (Stage 17b).
        /// </summary>
        /// <remarks>
        /// The figure is begun through <see cref="PlayerFigurePresenter"/> — the object the Battle
        /// scene really runs — rather than by calling <c>SpinePlayerView.Begin</c> here, so the
        /// picture is of the code path the player gets: the billboard pitch, the pose rule, the row
        /// sorting and the animation tick are the runtime's, not a copy of them. The camera pitch is
        /// the rig's own constant, because a capture has no built <see cref="BattleViewScene"/> to ask.
        /// </remarks>
        public bool TryBeginPlayerFigure(MonoBehaviour view, out string error)
        {
            error = string.Empty;

            if (view is null)
            {
                error = "the Battle scene has no player figure to photograph";
                return false;
            }

            _figure = new PlayerFigurePresenter(_speed);
            _figure.Attach(view, BattleCameraRig.PitchDegrees);

            if (!_figure.Begin())
            {
                error = _figure.ErrorCode ?? "the figure refused to build";
                return false;
            }

            PlaceFigure(_current);
            return true;
        }

        /// <summary>True when the figure was built for the shots.</summary>
        public bool HasPlayerFigure => _figure is not null && _figure.IsReady;

        /// <summary>The pose the figure was last placed at, for the capture report.</summary>
        public PlayerPose PlayerFigurePose => _figure is null ? PlayerPose.Idle : _figure.Pose;

        /// <summary>The animation the figure is on, for the capture report.</summary>
        public string PlayerFigureAnimation => _figure is null ? string.Empty : _figure.Animation;

        /// <summary>How much the player moved in the last frame, in simulation units a second.</summary>
        public float PlayerFigureSpeed => _figure is null ? 0f : _figure.SpeedUnitsPerSecond;

        /// <summary>
        /// Steps the floor a few ticks with the player walking right, so the photograph is of a
        /// walking figure rather than of one standing still.
        /// </summary>
        /// <remarks>
        /// Taken <b>last</b>, after every HUD shot: it moves the player, and the earlier shots are of
        /// the frame they were composed from.
        /// </remarks>
        public void Walk(int ticks)
        {
            for (int i = 0; i < ticks && _session.Phase == BattlePhase.Running; i++)
            {
                _session.Step(PlayerInputFrame.FromAxis(1f, 0f));
                _steppedTo = (int)_session.Tick;
            }

            _current.CopyFrom(_session.Frame);
            PlaceFigure(_current);
        }

        /// <summary>
        /// Removes what the figure built from the open scene. The view is the scene's own component
        /// and stays; its built actor is a child created by <c>Begin</c>, and a capture must not leave
        /// a scene dirty with a second one.
        /// </summary>
        public void DetachPlayerFigure()
        {
            MonoBehaviour view = _figure is null ? null : _figure.View;
            _figure?.Dispose();
            _figure = null;

            if (view is null)
            {
                return;
            }

            for (int i = view.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(view.transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Places and poses the figure from <paramref name="frame"/>.</summary>
        private void PlaceFigure(BattleFrameSnapshot frame)
        {
            if (_figure is null || !_figure.IsReady)
            {
                return;
            }

            _figure.AcceptSnapshot(frame);
            _figure.Render(1f, 1f / BattleConstants.TickRate);
        }

        /// <summary>
        /// Puts the chosen state on the screen: the frame's cooldowns on the mini bag, its numbers in
        /// the labels and the bars, and the floor's own progress on the timeline.
        /// </summary>
        /// <param name="shot">Which moment to show.</param>
        /// <remarks>
        /// Idempotent, so the same state is applied again for the second size: the mini bag rewrites
        /// a plate only when the frame moved it, and the presenter only when a value changed.
        /// </remarks>
        public void Apply(BattleShot shot)
        {
            // The floor-start shot is the frame the runner renders before it steps anything; the other
            // two are the frame the mid-fight search landed on. Both sizes of one shot read the same
            // snapshot, so the two pictures differ only by what the state itself changes.
            BattleFrameSnapshot frame = FrameOf(shot);

            if (shot == BattleShot.ManaShort)
            {
                FillManaShortCooldowns();
                _miniBag.ApplyCooldowns(_manaShort.AsSpan(0, frame.CooldownCount));
            }
            else
            {
                _miniBag.ApplyCooldowns(frame.Cooldowns);
            }

            _presenter.Present(frame);

            if (_timeline is not null)
            {
                _timeline.SetProgress(_state.FloorProgress.Value);
            }

            SetFill(_hpBar, _state.HealthPercent.Value);
            SetFill(_manaBar, _state.ManaPercent.Value);

            if (_bossRoot is not null)
            {
                // The view's own subscription call, made here because Bind() cannot run outside play
                // mode. Floor 1 has no boss, so the band is hidden and is not one of the regions the
                // capture command measures.
                _bossRoot.SetActive(_state.HasBoss.Value);
            }

            // The possession line (Stage 11c) is one of the labels WriteLabels writes: the presenter
            // makes it the localized sentence while the frame says the controls are reversed and empty
            // otherwise, exactly as BattleHudView would mirror it.

            WriteLabels();

            // The figure is placed from the same snapshot the HUD was written from, so the two
            // halves of the picture are one frame.
            PlaceFigure(frame);
        }

        /// <inheritdoc />
        /// <summary>
        /// Lays the arena floor under the picture, exactly as <see cref="BattleViewScene"/> does.
        /// </summary>
        /// <remarks>
        /// The floor is one opaque quad with the theme's ground material, 80 × 80 world units, flat
        /// on the ground plane (<c>ArenaGeometry.CreateCenteredQuad</c> scaled by the arena's radius).
        /// It exists only at runtime, which is why every earlier battle photograph showed the doctor
        /// on the camera's near-black clear colour: a white robe on black looks far better than the
        /// game's, and an olive tile under a pale lantern halo looks far worse. Neither is the game.
        /// Without the arena's environment there is nothing to copy and no floor is made — the
        /// failure would be a fixture, not a look.
        /// </remarks>
        private void BuildFloor()
        {
            var environment = UnityEngine.Object.FindFirstObjectByType<ArenaEnvironmentView>(
                UnityEngine.FindObjectsInactive.Include);

            Material ground = environment?.Theme?.GroundMaterial;
            if (ground is null)
            {
                return;
            }

            _floorQuad = ArenaGeometry.CreateCenteredQuad();
            _floorQuad.hideFlags = HideFlags.DontSave;

            _floor = new GameObject("capture-floor");
            _floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            _floor.AddComponent<MeshFilter>().sharedMesh = _floorQuad;

            var renderer = _floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ground;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _floor.transform.localScale = new Vector3(
                BattleViewCaps.ArenaRadius * 2f, BattleViewCaps.ArenaRadius * 2f, 1f);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_floor is not null)
            {
                UnityEngine.Object.DestroyImmediate(_floor);
                _floor = null;
            }

            if (_floorQuad is not null)
            {
                UnityEngine.Object.DestroyImmediate(_floorQuad);
                _floorQuad = null;
            }

            _figure?.Dispose();
            _figure = null;

            _session?.Dispose();

            // The run's own subscription to the speed control, then the state it writes into.
            _presenter?.Dispose();
            _state?.Dispose();
            _speed?.Dispose();
        }

        /// <summary>
        /// What the frame behind one state says, as a line for the report: the tick, the numbers a
        /// player would read off the screen, and each caster's cooldown fraction.
        /// </summary>
        /// <remarks>
        /// Written into <c>artifacts/ui/ui-report.md</c> beside the picture, because "this shot is
        /// mid-fight" is a claim about numbers and a claim about numbers should be checkable in the
        /// artifact rather than inferred from a title.
        /// </remarks>
        public string Describe(BattleShot shot)
        {
            BattleFrameSnapshot frame = FrameOf(shot);
            HudState hud = frame.Hud;

            var text = new System.Text.StringBuilder(96);
            text.Append("tick ").Append(frame.Tick)
                .Append(", hp ").Append(hud.Hp).Append('/').Append(hud.MaxHp)
                .Append(", mp ").Append(hud.Mp).Append('/').Append(hud.MaxMp)
                .Append(", kills ").Append(hud.Kills)
                .Append(", wave ").Append(hud.WaveIndex + 1)
                .Append(", ").Append(hud.RemainingSeconds(BattleConstants.TickRate).ToString("0.#"))
                .Append("s left");

            // The plates, as the fraction they are drawn at (remaining / period).
            ReadOnlySpan<LoadoutCooldown> cooldowns = frame.Cooldowns;
            text.Append(", cooldowns [");
            for (int i = 0; i < cooldowns.Length; i++)
            {
                LoadoutCooldown entry = cooldowns[i];
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append("#").Append(entry.InstanceId).Append(' ')
                    .Append((entry.RemainingTicks / (double)entry.PeriodTicks).ToString("0.00"));

                if (shot == BattleShot.ManaShort && i == _manaShortIndex)
                {
                    text.Append(" (mana-short)");
                }
            }

            text.Append(']');

            if (hud.HasBoss)
            {
                text.Append(", boss ").Append(hud.BossHp).Append('/').Append(hud.BossMaxHp)
                    .Append(", controls ").Append(hud.ControlFlipTicks > 0 ? "reversed" : "normal")
                    .Append(" (").Append(hud.ControlFlipTicks).Append(" tick(s) left)");
            }

            return text.ToString();
        }

        /// <summary>
        /// The mini bag's own objects, removed from the scene: the view builds its cells and boxes as
        /// children of the content area when a floor starts, and this tool must not leave a scene
        /// dirty with a duplicate pool behind it.
        /// </summary>
        public void ClearBagObjects()
        {
            RectTransform content = _miniBag is null ? null : _miniBag.Content;
            if (content is null)
            {
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
            }
        }

        private void WriteLabels()
        {
            for (int i = 0; i < Labels.Length; i++)
            {
                SerializedProperty property = _view.FindProperty(Labels[i].Field);
                if (property is null)
                {
                    continue;
                }

                var label = property.objectReferenceValue as TMPro.TMP_Text;
                if (label is null)
                {
                    continue;
                }

                label.text = Labels[i].Value(_state);
            }
        }

        /// <summary>The snapshot a shot shows.</summary>
        private BattleFrameSnapshot FrameOf(BattleShot shot)
        {
            switch (shot)
            {
                case BattleShot.FloorStart:
                    return _first;
                case BattleShot.BossBeforePossession:
                    return _beforePossession;
                default:
                    return _current;
            }
        }

        private static void SetFill(RectTransform bar, float value)
        {
            if (bar is not null)
            {
                bar.anchorMax = new Vector2(value < 0f ? 0f : (value > 1f ? 1f : value), 1f);
            }
        }

        /// <summary>
        /// How many different cooldown levels the last frame's casters are at, counted in tenths of
        /// their own period so two weapons of different lengths compare by how far through they are.
        /// </summary>
        private int DistinctCooldownLevels()
        {
            int count = 0;
            Span<int> levels = stackalloc int[BattleConstants.MaxLoadoutEntries];

            ReadOnlySpan<LoadoutCooldown> cooldowns = _current.Cooldowns;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                LoadoutCooldown entry = cooldowns[i];
                int level = entry.PeriodTicks <= 0
                    ? 0
                    : (int)Math.Round(10.0 * entry.RemainingTicks / entry.PeriodTicks);

                bool seen = false;
                for (int s = 0; s < count; s++)
                {
                    if (levels[s] == level)
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    levels[count++] = level;
                }
            }

            return count;
        }

        /// <summary>
        /// The caster whose plate the MP-short shot flags: the last entry of the loadout, so the
        /// picture shows a bag where one piece is blocked and the others are not.
        /// </summary>
        private void PickManaShortInstance()
        {
            ReadOnlySpan<LoadoutCooldown> cooldowns = _current.Cooldowns;
            ManaShortInstanceId = cooldowns.Length == 0 ? 0 : cooldowns[cooldowns.Length - 1].InstanceId;
            _manaShortIndex = cooldowns.Length == 0 ? -1 : cooldowns.Length - 1;
        }

        /// <summary>
        /// The frame's own cooldown list with the chosen entry carrying the flag the simulation sets
        /// when a ready cast cannot pay for itself. Every other entry is the frame's, untouched.
        /// </summary>
        private void FillManaShortCooldowns()
        {
            ReadOnlySpan<LoadoutCooldown> cooldowns = _current.Cooldowns;
            for (int i = 0; i < cooldowns.Length && i < _manaShort.Length; i++)
            {
                LoadoutCooldown entry = cooldowns[i];

                _manaShort[i] = i == _manaShortIndex
                    ? new LoadoutCooldown(
                        entry.InstanceId,
                        entry.RemainingTicks < 1 ? 0 : entry.RemainingTicks,
                        entry.PeriodTicks,
                        LoadoutCooldownFlags.ManaShort)
                    : entry;
            }
        }
    }
}
