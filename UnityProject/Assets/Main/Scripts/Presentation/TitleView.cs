using System;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Title screen view. It assigns text and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10: a View has no logic and computes no numbers. Everything this
    /// class does is mirror one reactive value into one label, which is exactly the
    /// "thin MVP" boundary — the strings were already decided by
    /// <see cref="TitlePresenter"/>.
    /// </para>
    /// <para>
    /// Subscriptions are disposed in <c>OnDestroy</c>. A title screen is reloaded
    /// every time the player returns to it, so a leaked subscription here would
    /// accumulate silently — that is what the R3 smoke probe guards against.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TitleView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _diagnosticsLabel;

        private UiState _state;
        private IDisposable _titleSubscription;
        private IDisposable _diagnosticsSubscription;

        private void Start() => Bind();

        private void OnDestroy()
        {
            _titleSubscription?.Dispose();
            _diagnosticsSubscription?.Dispose();
            _titleSubscription = null;
            _diagnosticsSubscription = null;
            _state = null;
        }

        /// <summary>
        /// Connects to the scope and pushes the first values. Public so a test can
        /// drive it without waiting for <c>Start</c>.
        /// </summary>
        public void Bind()
        {
            if (_state is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("TitleView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<UiState>();
            _titleSubscription = _state.Title.Subscribe(ApplyTitle);
            _diagnosticsSubscription = _state.Diagnostics.Subscribe(ApplyDiagnostics);

            _scope.Container.Resolve<TitlePresenter>().Refresh();

            Web.WebGlBridge.PublishScene(gameObject.scene.name);
        }

        private void ApplyTitle(string value)
        {
            if (_titleLabel is not null)
            {
                _titleLabel.text = value;
            }
        }

        private void ApplyDiagnostics(string value)
        {
            if (_diagnosticsLabel is not null)
            {
                _diagnosticsLabel.text = value;
            }
        }
    }
}
