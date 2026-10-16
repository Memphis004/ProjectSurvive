using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
            ("_speedLabel", state => state.SpeedLabel.Value),
        };

        private readonly BattleMiniBagView _miniBag;
        private readonly BattleTimelineView _timeline;
        private readonly RectTransform _hpBar;
        private readonly RectTransform _manaBar;
        private readonly GameObject _bossRoot;
        private readonly Image _portrait;
        private readonly SerializedObject _view;

        private readonly IGameConfig _config;
        private readonly RunSession _run;
        private readonly BattleSetup _setup;
        private readonly IBattleSession _session;
        private readonly LocalizationService _text;
        private readonly BattleSpeed _speed = new BattleSpeed();
        private readonly BattleHudState _state = new BattleHudState();
        private readonly BattleHudPresenter _presenter;
        private readonly BattleHudArt _art = new BattleHudArt();
        private readonly BattleFrameSnapshot _first = new BattleFrameSnapshot();
        private readonly BattleFrameSnapshot _current = new BattleFrameSnapshot();
        private readonly LoadoutCooldown[] _manaShort = new LoadoutCooldown[BattleConstants.MaxLoadoutEntries];

        private int _steppedTo;
        private int _manaShortIndex = -1;
        private bool _disposed;

        private BattleCaptureState(
            BattleHudView hud,
            BattleMiniBagView miniBag,
            IGameConfig config,
            RunSession run,
            BattleSetup setup,
            int startingBia)
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
            _portrait = _view.FindProperty("_portrait")?.objectReferenceValue as Image;
            _bossRoot = _view.FindProperty("_bossRoot")?.objectReferenceValue as GameObject;

            _presenter = new BattleHudPresenter(_state, _text, config, _speed);
            _session = DevBattleSessionBinding.CreateFactory(config).Create(setup);

            // The floor's static half first (floor name, timeline, captions), then its first frame —
            // the order BattleViewScene.Begin uses. That frame is kept aside: it is the floor-start
            // shot, and it cannot be stepped back to once the other shots have run.
            _presenter.Begin(setup, startingBia);
            _first.CopyFrom(_session.Frame);
            _current.CopyFrom(_session.Frame);

            _miniBag.Build(run.Backpack, setup.Loadout, config);

            if (_portrait is not null)
            {
                _portrait.sprite = _art.Bust;
                _portrait.color = Color.white;
            }
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

            state = new BattleCaptureState(hud, miniBag, config, run, setup, startingBia);
            return true;
        }

        /// <summary>The tick the mid-fight shot was taken at.</summary>
        public int MidFightTick { get; private set; }

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
            BattleFrameSnapshot frame = shot == BattleShot.FloorStart ? _first : _current;

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

            WriteLabels();
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

            // The run's own subscription to the speed control, then the state it writes into.
            _presenter?.Dispose();
            _state?.Dispose();
            _speed?.Dispose();
            _art?.Dispose();

            if (_portrait is not null)
            {
                _portrait.sprite = null;
            }
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
            BattleFrameSnapshot frame = shot == BattleShot.FloorStart ? _first : _current;
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
