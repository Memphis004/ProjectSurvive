using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The dev menu's view: six controls, a status line, and the same rule every other view in this
    /// project follows — it mirrors strings and forwards presses, and decides nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two gates keep it out of a release build.</b> The panel is activated only when
    /// <see cref="DevMenuPresenter.Visible"/> is true, which is <c>DevCheats.Allowed</c> — false in a
    /// release build by <c>DevCheats.AllowedInRelease</c> — and every command it sends is refused by
    /// the running floor anyway, because a floor started while cheats were off carries
    /// <c>BattleSetup.DevCheatsAllowed == false</c>. Neither gate is a build-time <c>#if</c>: both are
    /// values a test can read and the second is a value the session enforces.
    /// </para>
    /// <para>
    /// It submits through <see cref="BattleSimulationRunner.Submit"/>, which is the port the battle
    /// already exposes for out-of-band commands (surrender today, these cheats now). The
    /// <i>meaning</i> of each control — which command it is, and whether the floor accepted it —
    /// lives in <see cref="DevMenuPresenter"/>; the view only carries the press across.
    /// </para>
    /// <para>
    /// The captions are authored in the scene from <see cref="DevMenuText"/> by <c>ProjectSetup</c>,
    /// so the words exist in one place. They are deliberately not localization rows — see
    /// <see cref="DevMenuPresenter"/>'s remarks.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DevMenuView : MonoBehaviour
    {
        [Tooltip("The scene's own scope, which owns the dev menu's presenter.")]
        [SerializeField] private LifetimeScope _scope;

        [Tooltip("The runner a command is submitted through. Authored by ProjectSetup.")]
        [SerializeField] private BattleSimulationRunner _runner;

        [Tooltip("The panel the controls live in. Authored inactive; Bind shows it only where cheats are allowed.")]
        [SerializeField] private GameObject _panel;

        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _godModeLabel;

        [SerializeField] private Button _godModeButton;
        [SerializeField] private Button _addBiaButton;
        [SerializeField] private Button _spawnGhostButton;
        [SerializeField] private Button _killAllButton;
        [SerializeField] private Button _winNowButton;
        [SerializeField] private Button _loseNowButton;

        private DevMenuPresenter _presenter;
        private IDisposable _visibleSubscription;
        private IDisposable _godModeSubscription;
        private IDisposable _statusSubscription;

        /// <summary>True once the view has a presenter (a test may wait for it).</summary>
        public bool Bound => _presenter is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public DevMenuPresenter Presenter => _presenter;

        /// <summary>Whether the panel is on screen right now.</summary>
        public bool PanelVisible => _panel is not null && _panel.activeSelf;

        private void Start() => Bind();

        private void OnDestroy()
        {
            _visibleSubscription?.Dispose();
            _godModeSubscription?.Dispose();
            _statusSubscription?.Dispose();
            _visibleSubscription = null;
            _godModeSubscription = null;
            _statusSubscription = null;

            RemoveListener(_godModeButton, PressGodMode);
            RemoveListener(_addBiaButton, PressAddBia);
            RemoveListener(_spawnGhostButton, PressSpawnGhost);
            RemoveListener(_killAllButton, PressKillAll);
            RemoveListener(_winNowButton, PressWinNow);
            RemoveListener(_loseNowButton, PressLoseNow);

            _presenter = null;
        }

        /// <summary>
        /// Connects to the scene's scope and to the runner's command port. Public and idempotent so a
        /// test can drive it without waiting for <c>Start</c>.
        /// </summary>
        public void Bind()
        {
            if (_presenter is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("DevMenuView needs a built scene scope.", this);
                return;
            }

            _presenter = _scope.Container.Resolve<DevMenuPresenter>();
            _presenter.Begin();

            _visibleSubscription = _presenter.Visible.Subscribe(ApplyVisible);
            _godModeSubscription = _presenter.GodMode.Subscribe(ApplyGodMode);
            _statusSubscription = _presenter.Status.Subscribe(ApplyStatus);

            AddListener(_godModeButton, PressGodMode);
            AddListener(_addBiaButton, PressAddBia);
            AddListener(_spawnGhostButton, PressSpawnGhost);
            AddListener(_killAllButton, PressKillAll);
            AddListener(_winNowButton, PressWinNow);
            AddListener(_loseNowButton, PressLoseNow);
        }

        /// <summary>What the god-mode control does. Public so a test can press it without a pointer.</summary>
        public void PressGodMode() => Submit(_presenter?.NextGodMode());

        /// <summary>What the add-bia control does.</summary>
        public void PressAddBia() => Submit(_presenter?.AddBia());

        /// <summary>What the spawn-a-ghost control does.</summary>
        public void PressSpawnGhost() => Submit(_presenter?.SpawnGhost());

        /// <summary>What the kill-all control does.</summary>
        public void PressKillAll() => Submit(_presenter?.KillAll());

        /// <summary>What the skip-floor control does.</summary>
        public void PressWinNow() => Submit(_presenter?.WinNow());

        /// <summary>What the lose-now control does.</summary>
        public void PressLoseNow() => Submit(_presenter?.LoseNow());

        /// <summary>
        /// Sends one command to the floor and hands the session's own answer back to the presenter.
        /// </summary>
        /// <remarks>
        /// The result is never interpreted here: a refusal is exactly as informative as an
        /// acceptance — "a release build has the menu hidden <i>and</i> the floor refuses the
        /// command" — so it travels to the status line unchanged.
        /// </remarks>
        private void Submit(BattleCommand? command)
        {
            if (command is not BattleCommand request || _presenter is null)
            {
                return;
            }

            if (_runner is null)
            {
                // A wiring fault, not a runtime state: the scene must carry the runner reference.
                Debug.LogError("DevMenuView has no BattleSimulationRunner to submit through.", this);
                return;
            }

            _presenter.NoteResult(request.Kind, _runner.Submit(request));
        }

        private void ApplyVisible(bool visible)
        {
            if (_panel is not null)
            {
                _panel.SetActive(visible);
            }
        }

        private void ApplyGodMode(bool on)
        {
            if (_godModeLabel is not null)
            {
                _godModeLabel.text = on ? DevMenuText.GodModeOn : DevMenuText.GodModeOff;
            }
        }

        private void ApplyStatus(string status)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.text = status;
            }
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button is not null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button is not null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
