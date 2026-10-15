using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The camp's bottom bar (น้ำหนัก, สมาธิ, หีบ x/10, เบี้ย, ราคา refresh), the depart button
    /// with its reason, and the status line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every string is assigned from a value the presenter formatted or looked up
    /// (AGENTS rules 5 and 10); the bar adds nothing up. In particular the chest counter and the
    /// weight penalty are the run's own numbers, not the sum of what happens to be drawn.
    /// </para>
    /// <para>
    /// The depart button is disabled from the model rather than from a click: the reason it is
    /// disabled is on screen before the player tries, which is what the GDD asks for
    /// ("กดไม่ได้ถ้าไม่มีอาวุธอาคม ... และต้องแสดงเหตุผล"). Pressing it calls the presenter, which
    /// re-asks the run; a second press cannot depart twice because the run has already left Camp.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CampStatusBarView : MonoBehaviour
    {
        [SerializeField] private LifetimeScope _scope;

        [Header("Labels")]
        [SerializeField] private TMP_Text _floorLabel;
        [SerializeField] private TMP_Text _weightCaptionLabel;
        [SerializeField] private TMP_Text _weightValueLabel;
        [SerializeField] private RectTransform _weightBar;
        [SerializeField] private TMP_Text _focusCaptionLabel;
        [SerializeField] private TMP_Text _focusValueLabel;
        [SerializeField] private TMP_Text _chestCaptionLabel;
        [SerializeField] private TMP_Text _chestValueLabel;
        [SerializeField] private TMP_Text _biaCaptionLabel;
        [SerializeField] private TMP_Text _biaValueLabel;
        [SerializeField] private TMP_Text _refreshCostCaptionLabel;
        [SerializeField] private TMP_Text _refreshCostValueLabel;
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private TMP_Text _hpMpLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _departReasonLabel;

        [Header("Controls")]
        [SerializeField] private Button _departButton;
        [SerializeField] private TMP_Text _departButtonLabel;

        [Tooltip("The bar's own background, so the layout report can measure the closed-camp layout.")]
        [SerializeField] private RectTransform _panelRect;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(20);

        private CampUiState _state;
        private CampPresenter _presenter;
        private bool _bound;

        /// <summary>True when the depart button is live (a test reads this).</summary>
        public bool CanDepart { get; private set; }

        /// <summary>Resolves the screen's services and starts rendering. Public and idempotent.</summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            if (_scope == null || _scope.Container == null)
            {
                Debug.LogError("CampStatusBarView needs a built scope to resolve the camp state.", this);
                return;
            }

            _bound = true;

            _state = _scope.Container.Resolve<CampUiState>();
            _presenter = _scope.Container.Resolve<CampPresenter>();

            Subscribe(_state.FloorValue, _floorLabel);
            Subscribe(_state.WeightCaption, _weightCaptionLabel);
            Subscribe(_state.WeightValue, _weightValueLabel);
            Subscribe(_state.FocusCaption, _focusCaptionLabel);
            Subscribe(_state.FocusValue, _focusValueLabel);
            Subscribe(_state.ChestStatCaption, _chestCaptionLabel);
            Subscribe(_state.ChestValue, _chestValueLabel);
            Subscribe(_state.BiaCaption, _biaCaptionLabel);
            Subscribe(_state.BiaValue, _biaValueLabel);
            Subscribe(_state.RefreshCostCaption, _refreshCostCaptionLabel);
            Subscribe(_state.RefreshCostValue, _refreshCostValueLabel);
            Subscribe(_state.HintCaption, _hintLabel);
            Subscribe(_state.HpMpValue, _hpMpLabel);
            Subscribe(_state.StatusLine, _statusLabel);
            Subscribe(_state.DepartReason, _departReasonLabel);
            Subscribe(_state.DepartLabel, _departButtonLabel);

            _subscriptions.Add(_state.CanDepart.Subscribe(SetDepartInteractable));
            _subscriptions.Add(_state.WeightBar.Subscribe(SetWeightBar));

            if (_departButton != null)
            {
                _departButton.onClick.AddListener(OnDepartClicked);
            }
        }

        private void OnDestroy()
        {
            if (_departButton != null)
            {
                _departButton.onClick.RemoveListener(OnDepartClicked);
            }

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
            _state = null;
            _presenter = null;
            _bound = false;
        }

        private void OnDepartClicked()
        {
            if (_presenter != null)
            {
                _presenter.Depart();
            }
        }

        /// <summary>The centre of the ออกเดินทาง button in screen pixels, for a test that clicks it.</summary>
        /// <remarks>
        /// The centre, not <c>transform.position</c>: the button's pivot is its top-left corner, and a
        /// corner lies on the boundary a hit test excludes.
        /// </remarks>
        public bool TryGetDepartButtonScreenPoint(out Vector2 screen) =>
            CampScreenRects.TryCentre(_departButton == null ? null : (RectTransform)_departButton.transform, out screen);

        /// <summary>The bar's own screen rectangle.</summary>
        public bool TryGetPanelScreenRect(out Rect screen) => CampScreenRects.TryOf(_panelRect, out screen);

        private void SetDepartInteractable(bool interactable)
        {
            CanDepart = interactable;

            if (_departButton != null)
            {
                _departButton.interactable = interactable;
            }
        }

        /// <summary>
        /// The weight bar shows the movement penalty, not the weight: the penalty is what the
        /// player feels, and its cap is a data number the presenter already normalized against.
        /// </summary>
        private void SetWeightBar(float value)
        {
            if (_weightBar != null)
            {
                _weightBar.anchorMax = new Vector2(value < 0f ? 0f : (value > 1f ? 1f : value), 1f);
            }
        }

        private void Subscribe(ReactiveProperty<string> property, TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            _subscriptions.Add(property.Subscribe(value => label.text = value));
        }
    }
}
