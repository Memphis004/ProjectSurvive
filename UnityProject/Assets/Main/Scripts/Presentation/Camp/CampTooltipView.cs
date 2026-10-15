using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The piece tooltip: name, tier, the item's own table numbers, and the combos the layout
    /// has turned on ("tooltip: stat, tier, synergy ที่ทำงานอยู่").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three labels and one flag. Every line was composed by <see cref="CampPresenter"/> from
    /// localization and from numbers the run already resolved — the damage and cooldown are the
    /// item's own table values, and the multipliers are the ones the combo evaluation produced,
    /// because a View (or a presenter) that recomputed them would be a second rules engine
    /// (AGENTS rule 10).
    /// </para>
    /// <para>
    /// Show and hide are driven by one boolean, so a piece with no tooltip costs one comparison.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CampTooltipView : MonoBehaviour
    {
        [SerializeField] private LifetimeScope _scope;
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statsLabel;
        [SerializeField] private TMP_Text _combosLabel;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(4);

        private CampUiState _state;
        private bool _bound;

        /// <summary>True while the tooltip is on screen (a test reads this).</summary>
        public bool Visible { get; private set; }

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

            Subscribe(_state.TooltipTitle, _titleLabel);
            Subscribe(_state.TooltipStats, _statsLabel);
            Subscribe(_state.TooltipCombos, _combosLabel);

            _subscriptions.Add(_state.TooltipVisible.Subscribe(SetVisible));
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

            _subscriptions.Add(property.Subscribe(value => label.text = value));
        }
    }
}
