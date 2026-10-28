using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Camp;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

        /// <summary>
        /// Whether cheats are allowed at all — <see cref="DevMenuPresenter.Visible"/>, false in a
        /// release build.
        /// </summary>
        private bool _allowed;

        /// <summary>
        /// Whether the F1 key has been pressed since the floor started (Stage 08g-2). The panel starts
        /// shut even where cheats are allowed: the item asks for the dev menu to be "ซ่อนไว้หลังปุุม F1",
        /// and a menu that is on screen by default is a panel the player reads instead of the fight.
        /// </summary>
        private bool _toggledOpen;

        private IDisposable _visibleSubscription;
        private IDisposable _godModeSubscription;
        private IDisposable _statusSubscription;
        private int _publishedCanvasWidth = -1;
        private int _publishedCanvasHeight = -1;

        /// <summary>The panel's controls, named for the page's layout report.</summary>
        public enum DevMenuButton
        {
            /// <summary>อมตะ / god mode.</summary>
            GodMode = 0,

            /// <summary>เพิ่มเบี้ย / add bia.</summary>
            AddBia = 1,

            /// <summary>เพิ่มผี / spawn ghost.</summary>
            SpawnGhost = 2,

            /// <summary>ฆ่าผีทั้งหมด / kill all.</summary>
            KillAll = 3,

            /// <summary>ข้ามชั้น (ชนะ) / win now.</summary>
            WinNow = 4,

            /// <summary>แพ้ทันที / lose now.</summary>
            LoseNow = 5,
        }

        /// <summary>True once the view has a presenter (a test may wait for it).</summary>
        public bool Bound => _presenter is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public DevMenuPresenter Presenter => _presenter;

        /// <summary>Whether the panel is on screen right now.</summary>
        public bool PanelVisible => _panel is not null && _panel.activeSelf;

        private void Start() => Bind();

        /// <summary>
        /// Publishes where the panel's controls are, and whether they are on screen, once the floor
        /// binds and again whenever the panel opens, closes or the canvas changes size.
        /// </summary>
        /// <remarks>
        /// It is the view that knows this, and the page has no other way to learn it: a browser test
        /// can press a control it can name only if something tells it where that control is (the camp
        /// publishes the same thing for the same reason). Since Stage 08g-2 the menu starts hidden
        /// behind F1, so <b>the first document appears when the panel opens</b> — a browser presses the
        /// key and then reads it. Nothing is published before that, because a control on a shut panel
        /// has nowhere to click and no position worth reporting, and nothing at all is published where
        /// cheats are not allowed, which is every release build.
        /// </remarks>
        private void Update()
        {
            // Stage 08g-2: F1 is the only way in or out of the menu where cheats are allowed. The key
            // is read from the Input System the rest of the project uses, and the press is a UI toggle
            // rather than a command — nothing here decides whether a cheat may run.
            Keyboard keyboard = Keyboard.current;
            if (_allowed && keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                TogglePanel();
            }

            if (PanelVisible
                && (_publishedCanvasWidth != Screen.width || _publishedCanvasHeight != Screen.height))
            {
                PublishReport();
            }
        }

        /// <summary>
        /// Opens or closes the panel, exactly as F1 does, and publishes the report of where its
        /// controls now are. Public so a PlayMode test can reach the menu in a process with no
        /// keyboard device; nothing here decides whether a cheat may run.
        /// </summary>
        public void TogglePanel()
        {
            if (!_allowed)
            {
                return;
            }

            _toggledOpen = !_toggledOpen;
            ApplyPanel();
            PublishReport();
        }

        /// <summary>
        /// Writes the report the page reads, remembering the size it described. Silent where the menu
        /// is not allowed at all — a release build publishes no dev-menu global. A shut panel cannot
        /// report a control's position (`CampScreenRects` refuses an inactive transform, which is
        /// honest: nothing there can be clicked), so the document appears when the panel opens.
        /// </summary>
        private void PublishReport()
        {
            if (!_allowed)
            {
                return;
            }

            _publishedCanvasWidth = Screen.width;
            _publishedCanvasHeight = Screen.height;
            DevMenuReport.Publish(this);
        }

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

        /// <summary>
        /// Where one of the panel's controls is on screen right now, in screen pixels with the origin
        /// at the bottom-left — the convention the camp's layout report already uses.
        /// </summary>
        /// <remarks>
        /// False while the panel is hidden, which is the honest answer: a control that is not on screen
        /// has nowhere to click. The conversion is shared with the camp's views rather than copied,
        /// because the defect it exists for (a pivot on a corner reporting that corner as the position)
        /// is a property of the transform.
        /// </remarks>
        public bool TryGetButtonScreenPoint(DevMenuButton button, out Vector2 screen)
        {
            Button target;
            switch (button)
            {
                case DevMenuButton.GodMode:
                    target = _godModeButton;
                    break;
                case DevMenuButton.AddBia:
                    target = _addBiaButton;
                    break;
                case DevMenuButton.SpawnGhost:
                    target = _spawnGhostButton;
                    break;
                case DevMenuButton.KillAll:
                    target = _killAllButton;
                    break;
                case DevMenuButton.WinNow:
                    target = _winNowButton;
                    break;
                default:
                    target = _loseNowButton;
                    break;
            }

            return CampScreenRects.TryCentre(
                target is null ? null : target.transform as RectTransform, out screen);
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
            _allowed = visible;
            ApplyPanel();
        }

        /// <summary>
        /// The one place the panel's active state is set: cheats allowed <b>and</b> F1 pressed.
        /// </summary>
        private void ApplyPanel()
        {
            if (_panel is not null)
            {
                _panel.SetActive(_allowed && _toggledOpen);
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
