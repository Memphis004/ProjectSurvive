using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The piece tooltip: ชื่อ, ระดับ, ราคา, ค่าที่ใช้จริง, รุ่นย่อยที่เลือก, คำอธิบาย, คอมโบ —
    /// one label per line, on its own canvas that draws above the whole camp screen. The same panel
    /// explains a <b>shelf offer</b> (Stage 09n): ราคา carries the price and the rent verdict, and
    /// คอมโบ carries the rules the piece could join rather than the ones acting on it. The lines a
    /// piece has and an offer does not (and the reverse) are hidden, never blank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own canvas, not a panel on the camp's.</b> The camp screen is one canvas whose draw
    /// order is the hierarchy order of its panels, so \"the tooltip is on top\" would hold only as long
    /// as nothing new was added after it — the board's cells, the piece rectangles and the combo
    /// lines are all created at runtime and are all appended to that hierarchy. The tooltip therefore
    /// lives on a second root canvas whose <c>sortingOrder</c> is higher than the camp's, which the
    /// installed engine documents as \"<i>Canvas' order within a sorting layer</i>\" for
    /// <c>Canvas.sortingOrder</c> and \"<i>Allows for nested canvases to override the
    /// Canvas.sortingOrder from parent canvases</i>\" for <c>Canvas.overrideSorting</c>
    /// (UnityEngine.UIModule.xml, Unity 6000.3.9f1). Nothing on that canvas can be clicked either:
    /// it carries no <c>GraphicRaycaster</c> and every graphic in it has <c>raycastTarget</c> off.
    /// </para>
    /// <para>
    /// <b>One field per line.</b> Each string is composed by <see cref="CampPresenter"/> from
    /// localization and from the run's resolved read model (Stage 09h); the view assigns them and
    /// hides the lines that are empty (a bag has no คำอธิบาย, a charm has no upgrade variant). A view
    /// that joined them or formatted them would be a second formatting path, which AGENTS rule 10
    /// forbids.
    /// </para>
    /// <para>
    /// The pointer is read only while the tooltip is on screen, and the placement is
    /// <see cref="CampTooltipPlacement"/>'s arithmetic — no allocation, no formatting, nothing built
    /// per frame.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CampTooltipView : MonoBehaviour
    {
        [SerializeField] private LifetimeScope _scope;
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _tierLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _upgradesLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private TMP_Text _combosLabel;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(8);

        private CampUiState _state;
        private RectTransform _panel;
        private bool _bound;

        /// <summary>True while the tooltip is on screen (a test reads this).</summary>
        public bool Visible { get; private set; }

        /// <summary>The canvas the tooltip draws on, so its draw order can be compared with the screen's.</summary>
        public Canvas RootCanvas { get; private set; }

        /// <summary>Resolves the screen's services and starts rendering. Public and idempotent.</summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            if (_scope == null || _scope.Container == null)
            {
                Debug.LogError("CampTooltipView needs a built scope to resolve the camp state.", this);
                return;
            }

            _bound = true;
            _state = _scope.Container.Resolve<CampUiState>();

            _panel = _root == null ? null : (RectTransform)_root.transform;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RootCanvas = canvas.rootCanvas;
            }

            if (_root != null)
            {
                // Belt and braces with the canvas having no GraphicRaycaster: a tooltip that follows
                // the pointer must never eat the pointer's own events.
                var background = _root.GetComponent<Image>();
                if (background != null)
                {
                    background.raycastTarget = false;
                }
            }

            _subscriptions.Add(_state.TooltipVisible.Subscribe(SetVisible));

            Subscribe(_state.TooltipTitle, _titleLabel);
            Subscribe(_state.TooltipTier, _tierLabel);
            Subscribe(_state.TooltipPrice, _priceLabel);
            Subscribe(_state.TooltipStatus, _statusLabel);
            Subscribe(_state.TooltipUpgrades, _upgradesLabel);
            Subscribe(_state.TooltipDescription, _descriptionLabel);
            Subscribe(_state.TooltipCombos, _combosLabel);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
            _state = null;
            _bound = false;
        }

        /// <summary>
        /// Keeps the panel beside the pointer while it is on screen.
        /// </summary>
        /// <remarks>
        /// Nothing runs while the tooltip is hidden, which is every frame of a camp the player is
        /// only walking through.
        /// </remarks>
        private void Update()
        {
            if (!Visible || _panel == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            PlaceAt(mouse.position.ReadValue());
        }

        /// <summary>
        /// Puts the panel beside a screen point, flipped and clamped so it stays on the canvas.
        /// </summary>
        /// <remarks>
        /// The conversion is the same one the rest of the screen uses:
        /// <c>RectTransformUtility.ScreenPointToLocalPointInRectangle</c> against the canvas. The
        /// canvas' own pivot is subtracted out (<c>rect.min</c>) rather than assumed, so the
        /// arithmetic holds whichever pivot the canvas happens to have.
        /// </remarks>
        public void PlaceAt(Vector2 screenPoint)
        {
            if (_panel == null)
            {
                return;
            }

            Canvas canvas = RootCanvas;
            if (canvas == null)
            {
                return;
            }

            var canvasRect = (RectTransform)canvas.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, null, out Vector2 local))
            {
                return;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            Vector2 panelSize = _panel.rect.size;

            if (panelSize.x <= 0f || panelSize.y <= 0f || canvasSize.x <= 0f || canvasSize.y <= 0f)
            {
                // The first frame after being shown can arrive before the layout has run.
                return;
            }

            _panel.anchoredPosition = CampTooltipPlacement.Place(
                new Vector2(local.x - canvasRect.rect.xMin, local.y - canvasRect.rect.yMin),
                panelSize,
                canvasSize);
        }

        /// <summary>The tooltip's own screen rectangle, or false while it is hidden or has no area.</summary>
        public bool TryGetScreenRect(out Rect rect) => CampScreenRects.TryOf(_panel, out rect);

        private void SetVisible(bool visible)
        {
            Visible = visible;

            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        private void Subscribe(ReactiveProperty<string> property, TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            // A line with nothing to say is hidden rather than drawn empty: the tooltip's shape then
            // says which of the five lines this piece actually has.
            _subscriptions.Add(property.Subscribe(value =>
            {
                bool hasText = !string.IsNullOrEmpty(value);
                label.text = value;
                label.gameObject.SetActive(hasText);
            }));
        }
    }
}
