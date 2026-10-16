using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// The Battle scene's HUD: it mirrors reactive values into labels, bars and the timeline,
    /// and does nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10: a View has no logic and computes nothing. Every string here was
    /// already localized or formatted by <see cref="BattleHudPresenter"/>; every bar is a
    /// percentage it was handed. The one interaction is the speed button, which calls the
    /// 1×/2× control directly because that control <i>is</i> UI state rather than a rule.
    /// </para>
    /// <para>
    /// Nothing here subscribes to an entity. The HUD reads a handful of scalars, so the
    /// number of subscriptions is a constant — "HUD ไม่ subscribe ตำแหน่งทุก entity" — and
    /// the 300-ghost case costs the same as the one-ghost case.
    /// </para>
    /// <para>
    /// Subscriptions are disposed when the scene is left. A battle is entered and left many
    /// times in a run, so a leaked subscription here would accumulate silently.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleHudView : MonoBehaviour
    {
        [SerializeField] private LifetimeScope _scope;

        [Header("Labels")]
        [SerializeField] private TMP_Text _floorLabel;

        [Tooltip("Which floor of the run this is, under the floor's name (Stage 08c).")]
        [SerializeField] private TMP_Text _floorIndexLabel;

        [SerializeField] private TMP_Text _hpCaption;
        [SerializeField] private TMP_Text _hpValue;
        [SerializeField] private TMP_Text _manaCaption;
        [SerializeField] private TMP_Text _manaValue;
        [SerializeField] private TMP_Text _biaCaption;
        [SerializeField] private TMP_Text _biaValue;
        [SerializeField] private TMP_Text _timeCaption;
        [SerializeField] private TMP_Text _timeValue;
        [SerializeField] private TMP_Text _waveCaption;
        [SerializeField] private TMP_Text _waveValue;
        [SerializeField] private TMP_Text _killsCaption;
        [SerializeField] private TMP_Text _killsValue;

        [Tooltip("The mini ย่าม's caption, above the bag (Stage 08c).")]
        [SerializeField] private TMP_Text _bagCaption;

        [Tooltip("The status panel's caption, over the figure and the two bars.")]
        [SerializeField] private TMP_Text _statusCaption;

        [SerializeField] private TMP_Text _timelineCaption;
        [SerializeField] private TMP_Text _bossCaption;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _speedLabel;

        [Header("Status panel")]
        [Tooltip("The figure at the bottom left: placeholder art, generated in code (Stage 08c).")]
        [SerializeField] private Image _portrait;

        [Header("Bars")]
        [Tooltip("The filled rectangle inside the HP track; its width is the health fraction.")]
        [SerializeField] private RectTransform _hpBar;

        [Tooltip("The filled rectangle inside the MP track.")]
        [SerializeField] private RectTransform _manaBar;

        [Tooltip("The filled rectangle inside the boss track.")]
        [SerializeField] private RectTransform _bossBar;

        [Tooltip("Shown only on a boss floor.")]
        [SerializeField] private GameObject _bossRoot;

        [Header("Controls")]
        [SerializeField] private Button _speedButton;
        [SerializeField] private BattleTimelineView _timeline;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(24);

        private BattleHudState _state;
        private BattleSpeed _speed;
        private BattleHudPresenter _presenter;
        private BattleHudArt _art;
        private bool _bound;

        /// <summary>The HUD state this view mirrors (null until <see cref="Bind"/>).</summary>
        public BattleHudState State => _state;

        /// <summary>How many reactive subscriptions the view holds.</summary>
        public int SubscriptionCount => _subscriptions.Count;

        /// <summary>How many markers the timeline is showing.</summary>
        public int TimelineMarkerCount => _timeline is null ? 0 : _timeline.MarkerCount;

        /// <summary>
        /// Resolves the HUD's services from the scene scope and starts mirroring. Public and
        /// idempotent so the scene's driver (and a test) can call it without waiting for a
        /// particular <c>Start</c> order.
        /// </summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("BattleHudView needs a built scope to resolve the HUD state.", this);
                return;
            }

            _bound = true;

            _state = _scope.Container.Resolve<BattleHudState>();
            _speed = _scope.Container.Resolve<BattleSpeed>();
            _presenter = _scope.Container.Resolve<BattleHudPresenter>();

            Subscribe(_state.FloorName, _floorLabel);
            Subscribe(_state.FloorIndex, _floorIndexLabel);
            Subscribe(_state.HpCaption, _hpCaption);
            Subscribe(_state.HpValue, _hpValue);
            Subscribe(_state.ManaCaption, _manaCaption);
            Subscribe(_state.ManaValue, _manaValue);
            Subscribe(_state.BiaCaption, _biaCaption);
            Subscribe(_state.BiaValue, _biaValue);
            Subscribe(_state.TimeCaption, _timeCaption);
            Subscribe(_state.TimeValue, _timeValue);
            Subscribe(_state.WaveCaption, _waveCaption);
            Subscribe(_state.WaveValue, _waveValue);
            Subscribe(_state.KillsCaption, _killsCaption);
            Subscribe(_state.KillsValue, _killsValue);
            Subscribe(_state.BagCaption, _bagCaption);
            Subscribe(_state.StatusCaption, _statusCaption);
            Subscribe(_state.TimelineCaption, _timelineCaption);
            Subscribe(_state.BossCaption, _bossCaption);
            Subscribe(_state.StatusLine, _statusLabel);
            Subscribe(_state.SpeedLabel, _speedLabel);

            _subscriptions.Add(_state.HealthPercent.Subscribe(value => SetFill(_hpBar, value)));
            _subscriptions.Add(_state.ManaPercent.Subscribe(value => SetFill(_manaBar, value)));
            _subscriptions.Add(_state.BossHealthPercent.Subscribe(value => SetFill(_bossBar, value)));
            _subscriptions.Add(_state.HasBoss.Subscribe(SetBossVisible));
            _subscriptions.Add(_state.FloorProgress.Subscribe(SetProgress));
            _subscriptions.Add(_state.TimeWarning.Subscribe(SetTimeWarning));
            _subscriptions.Add(_state.Ended.Subscribe(SetEnded));

            ApplyPortrait();

            if (_speedButton is not null)
            {
                _speedButton.onClick.AddListener(OnSpeedClicked);
            }
        }

        /// <summary>Pushes the floor's spawn windows into the timeline bar.</summary>
        public void ShowTimeline(IReadOnlyList<BattleTimelineMarker> markers)
        {
            if (_timeline is not null)
            {
                _timeline.SetMarkers(markers);
            }
        }

        private void OnDestroy()
        {
            if (_speedButton is not null)
            {
                _speedButton.onClick.RemoveListener(OnSpeedClicked);
            }

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
            _state = null;
            _speed = null;
            _presenter = null;
            _bound = false;

            _art?.Dispose();
            _art = null;
        }

        /// <summary>
        /// Puts the generated placeholder figure in the status panel.
        /// </summary>
        /// <remarks>
        /// The HUD's own art rather than the battle's: <c>BattlePlaceholderArt</c> belongs to the
        /// world (it is built and disposed by the view scene), and the HUD outlives neither more nor
        /// less. It is drawn from the same cloth-and-shawl language as the world's figures (AGENTS
        /// rule 13: generated, never copied), so the panel reads as this game's own placeholder.
        /// </remarks>
        private void ApplyPortrait()
        {
            if (_portrait is null)
            {
                return;
            }

            _art = new BattleHudArt();
            _portrait.sprite = _art.Bust;
            _portrait.color = Color.white;
        }

        private void OnSpeedClicked()
        {
            // A UI control, not a rule: the presenter observes the speed control and writes
            // the label, so the view never formats anything itself.
            if (_speed is not null)
            {
                _speed.Toggle();
            }
            else if (_presenter is not null)
            {
                _presenter.ToggleSpeed();
            }
        }

        private void Subscribe(ReactiveProperty<string> property, TMP_Text label)
        {
            if (label is null)
            {
                return;
            }

            _subscriptions.Add(property.Subscribe(value => label.text = value));
        }

        /// <summary>
        /// Sets a bar's fill by anchoring its fill rectangle. Deliberately not
        /// <c>Image.fillAmount</c>: an anchored rectangle needs no sprite asset, so the scene
        /// carries no generated UI texture and the fill is one layout write.
        /// </summary>
        private static void SetFill(RectTransform bar, float value)
        {
            if (bar is not null)
            {
                bar.anchorMax = new Vector2(value < 0f ? 0f : (value > 1f ? 1f : value), 1f);
            }
        }

        private void SetBossVisible(bool visible)
        {
            if (_bossRoot is not null)
            {
                _bossRoot.SetActive(visible);
            }
        }

        private void SetProgress(float value)
        {
            if (_timeline is not null)
            {
                _timeline.SetProgress(value);
            }
        }

        private void SetTimeWarning(bool warning)
        {
            if (_timeValue is not null)
            {
                _timeValue.color = warning ? new Color(1f, 0.45f, 0.42f, 1f) : Color.white;
            }
        }

        private void SetEnded(bool ended)
        {
            if (ended && _statusLabel is not null)
            {
                _statusLabel.gameObject.SetActive(true);
            }
        }
    }
}
