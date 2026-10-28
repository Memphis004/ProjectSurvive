using System;
using System.Collections.Generic;
using System.Globalization;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using R3;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// Turns a battle frame into the HUD's reactive values, and reacts to the HUD's own
    /// event channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The presenter is the only writer of <see cref="BattleHudState"/>; the View only
    /// mirrors it. It reads the frame it is handed (never the session), so it cannot pull
    /// simulation state the contract does not publish, and it subscribes to <b>no</b>
    /// entity — a frame is a by-value snapshot, not a stream.
    /// </para>
    /// <para>
    /// Text comes from localization only (AGENTS rule 5): captions, the floor's name and its
    /// number, and the end banner are looked up by key. The numeric values are formatted here, and
    /// each one is guarded on its <i>inputs</i> rather than on the string, so a HUD sitting at a
    /// constant HP builds no string at all — R3 would otherwise publish, and TMP re-layout, 60
    /// times a second for nothing.
    /// </para>
    /// <para>
    /// Stage 08c removed the equipped-spell line: the mini ย่าม draws the bag itself, piece by
    /// piece, so a comma-separated list of skill names was both a second source of truth and a
    /// string rebuilt whenever the loadout changed. What the HUD still formats is what a
    /// <i>frame</i> can say.
    /// </para>
    /// <para>
    /// The HUD is a listener on <see cref="BattleEventChannel.Hud"/>, so it owns its own
    /// cursor and cannot be starved by the VFX channel's work. It uses events only for
    /// things a frame cannot say (a floor that ended, a timer warning) and never to decide
    /// anything, and never to reconstruct state the frame already carries.
    /// </para>
    /// </remarks>
    public sealed class BattleHudPresenter : IBattleEventListener, IDisposable
    {
        private readonly BattleHudState _state;
        private readonly LocalizationService _text;
        private readonly IGameConfig _config;
        private readonly BattleSpeed _speed;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(4);

        private int _startingBia;
        private string _possessionLine = string.Empty;
        private int _lastHp = int.MinValue;
        private int _lastMaxHp = int.MinValue;
        private int _lastMp = int.MinValue;
        private int _lastMaxMp = int.MinValue;
        private int _lastBia = int.MinValue;
        private int _lastKills = int.MinValue;
        private int _lastSecond = int.MinValue;
        private int _lastWave = int.MinValue;
        private int _waveCount;
        private bool _disposed;

        /// <summary>Creates a presenter over the HUD state.</summary>
        public BattleHudPresenter(
            BattleHudState state, LocalizationService text, IGameConfig config, BattleSpeed speed)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _speed = speed ?? throw new ArgumentNullException(nameof(speed));

            _subscriptions.Add(_speed.Multiplier.Subscribe(OnSpeedChanged));
            OnSpeedChanged(_speed.Multiplier.Value);
        }

        /// <summary>The HUD values this presenter writes.</summary>
        public BattleHudState State => _state;

        /// <summary>
        /// Fills what is known before the first tick: the floor's name and timeline, the
        /// equipped spells, every caption, and the purse the player walked in with.
        /// </summary>
        /// <param name="setup">The floor about to run (its loadout is the real resolved one).</param>
        /// <param name="startingBia">
        /// The run's purse entering the floor. The battle reports what its pickups
        /// <i>paid this floor</i> (<see cref="HudState.Currency"/>, the sum of
        /// <c>enemy.drop_value</c> over collected drops), so the HUD shows the two added.
        /// The clear reward is not part of it: that belongs to the run's settlement, not
        /// to the battle, and a loss pays none.
        /// </param>
        public void Begin(BattleSetup setup, int startingBia)
        {
            if (setup is null)
            {
                return;
            }

            _startingBia = startingBia;

            FloorConfig floor = FindFloor(setup.StageId);
            BattleHudState.Set(_state.FloorName, floor is null ? string.Empty : Lookup(floor.NameKey));

            // The floor's number, from the setup the run handed the battle — never counted here.
            BattleHudState.Set(
                _state.FloorIndex,
                setup.FloorIndex <= 0
                    ? string.Empty
                    : Format("hud.floor.format", setup.FloorIndex));

            IReadOnlyList<BattleTimelineMarker> markers = floor is null
                ? Array.Empty<BattleTimelineMarker>()
                : BattleTimelineModel.Build(_config, floor);

            _waveCount = markers.Count;
            _state.SetTimeline(markers);

            BattleHudState.Set(_state.HpCaption, Lookup("hud.hp"));
            BattleHudState.Set(_state.ManaCaption, Lookup("hud.mp"));
            BattleHudState.Set(_state.BiaCaption, Lookup("hud.bia"));
            BattleHudState.Set(_state.TimeCaption, Lookup("hud.time"));
            BattleHudState.Set(_state.WaveCaption, Lookup("hud.wave"));
            BattleHudState.Set(_state.BossCaption, Lookup("hud.boss"));
            BattleHudState.Set(_state.EliteCaption, Lookup("hud.elite"));
            BattleHudState.Set(_state.KillsCaption, Lookup("hud.kills"));
            BattleHudState.Set(_state.StatusCaption, Lookup("hud.status"));
            _possessionLine = Lookup("hud.possession");
            BattleHudState.Set(_state.PossessionText, string.Empty);
            BattleHudState.Set(_state.TimelineCaption, Lookup("hud.timeline"));
            BattleHudState.Set(_state.BiaValue, startingBia.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Mirrors one frame's HUD values into the reactive state.</summary>
        public void Present(BattleFrameSnapshot snapshot)
        {
            if (snapshot is null || !snapshot.HasFrame)
            {
                return;
            }

            HudState hud = snapshot.Hud;

            if (hud.Hp != _lastHp || hud.MaxHp != _lastMaxHp)
            {
                _lastHp = hud.Hp;
                _lastMaxHp = hud.MaxHp;
                BattleHudState.Set(_state.HpValue, Pair(hud.Hp, hud.MaxHp));
                BattleHudState.Set(_state.HealthPercent, Percent(hud.Hp, hud.MaxHp));
            }

            if (hud.Mp != _lastMp || hud.MaxMp != _lastMaxMp)
            {
                _lastMp = hud.Mp;
                _lastMaxMp = hud.MaxMp;
                BattleHudState.Set(_state.ManaValue, Pair(hud.Mp, hud.MaxMp));
                BattleHudState.Set(_state.ManaPercent, Percent(hud.Mp, hud.MaxMp));
            }

            int bia = _startingBia + hud.Currency;
            if (bia != _lastBia)
            {
                _lastBia = bia;
                BattleHudState.Set(_state.BiaValue, bia.ToString(CultureInfo.InvariantCulture));
            }

            if (hud.Kills != _lastKills)
            {
                _lastKills = hud.Kills;
                BattleHudState.Set(_state.KillsValue, hud.Kills.ToString(CultureInfo.InvariantCulture));
            }

            float remaining = hud.RemainingSeconds(BattleConstants.TickRate);
            int second = remaining <= 0f ? 0 : (int)Math.Ceiling(remaining);
            if (second != _lastSecond)
            {
                _lastSecond = second;
                BattleHudState.Set(_state.TimeValue, Clock(second));
            }

            if (hud.WaveIndex != _lastWave)
            {
                _lastWave = hud.WaveIndex;
                BattleHudState.Set(_state.WaveValue, Pair(hud.WaveIndex + 1, _waveCount));
            }

            BattleHudState.Set(_state.FloorProgress, Progress(hud));
            BattleHudState.Set(_state.HasBoss, hud.HasBoss);
            BattleHudState.Set(_state.BossHealthPercent, Percent(hud.BossHp, hud.BossMaxHp));

            // Stage 11c: the frame carries the reversal itself, so the HUD never has to remember
            // that a possession started or guess when it ended. One comparison, one line.
            BattleHudState.Set(
                _state.PossessionText, hud.ControlFlipTicks > 0 ? _possessionLine : string.Empty);
        }

        /// <summary>Writes the end banner once the session has a result.</summary>
        public void PresentEnd(BattleResult result)
        {
            if (result is null || _state.Ended.Value)
            {
                return;
            }

            string key;
            switch (result.Outcome)
            {
                case BattlePhase.Won:
                    key = "hud.end.win";
                    break;
                case BattlePhase.Lost:
                    key = "hud.end.lose";
                    break;
                default:
                    key = "hud.end.aborted";
                    break;
            }

            BattleHudState.Set(_state.StatusLine, Lookup(key));
            BattleHudState.Set(_state.Ended, true);
        }

        /// <summary>Advances the 1×/2× control, from the HUD's speed button.</summary>
        public void ToggleSpeed() => _speed.Toggle();

        /// <inheritdoc />
        public void OnBattleEvent(in BattleEvent battleEvent)
        {
            if (battleEvent.Type == BattleEventType.TimeWarning)
            {
                BattleHudState.Set(_state.TimeWarning, true);
            }

            // Everything else the HUD needs is in the frame. Reading state beats
            // reconstructing it from the event stream, which is presentation-only
            // ([battle-contract.md §5]).
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
        }

        private void OnSpeedChanged(float multiplier)
        {
            BattleHudState.Set(_state.Speed, multiplier);
            BattleHudState.Set(_state.SpeedLabel, BattleSpeed.Label(multiplier));
        }

        private FloorConfig FindFloor(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                return null;
            }

            return _config.TryGetFloor(stageId, out FloorConfig floor) ? floor : null;
        }

        /// <summary>
        /// A key the HUD tolerates missing: the captions shipped with Stage 08, so a table
        /// that lost one degrades to the key instead of throwing inside the frame loop. The
        /// smoke probe and the data validator are what catch a genuinely missing row.
        /// </summary>
        private string Lookup(string key)
        {
            return _text.TryGet(key, out string value) ? value : key;
        }

        /// <summary>A localized format string with one integer in it (AGENTS rule 5: strings live in the tables).</summary>
        private string Format(string key, int value) =>
            string.Format(CultureInfo.InvariantCulture, Lookup(key), value);

        private static string Pair(int value, int max) =>
            value.ToString(CultureInfo.InvariantCulture) + "/" + max.ToString(CultureInfo.InvariantCulture);

        private static string Clock(int seconds)
        {
            int minutes = seconds / 60;
            int rest = seconds % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":"
                + rest.ToString("00", CultureInfo.InvariantCulture);
        }

        private static float Percent(int value, int max) =>
            max <= 0 ? 0f : Mathf.Clamp01(value / (float)max);

        private static float Progress(in HudState hud) =>
            hud.FloorDurationTicks <= 0
                ? 0f
                : Mathf.Clamp01(hud.ElapsedTicks / (float)hud.FloorDurationTicks);
    }
}
