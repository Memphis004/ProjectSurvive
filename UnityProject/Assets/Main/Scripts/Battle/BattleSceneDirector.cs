using System;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Battle
{
    /// <summary>
    /// Starts the Battle scene's floor and tears it down: resolve the ports, build the setup,
    /// hand the runner its context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin. Everything it composes is either the scene's wiring (authored by
    /// <c>ProjectSetup</c> through the Editor API, AGENTS rule 11) or a service resolved from
    /// <see cref="BattleSceneScope"/>. The result is the one place that knows both halves: the
    /// presentation's <see cref="BattleSimulationRunner"/> and the composition root's real-vs-fake
    /// choice.
    /// </para>
    /// <para>
    /// It resolves services with explicit calls rather than field injection, because a
    /// <c>MonoBehaviour</c>'s injection needs a reflected constructor path and IL2CPP strips what
    /// no IL call site reaches (<c>llmwiki/gotchas/il2cpp-di-and-reflection.md</c>).
    /// </para>
    /// <para>
    /// A start that fails logs and leaves the scene inert instead of pretending to run. The
    /// realistic failures are data problems — a run that cannot start, a departure refused for
    /// want of an arcane weapon — and they should look like a data problem on the first frame.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleSceneDirector : MonoBehaviour
    {
        [SerializeField] private BattleSceneScope _scope;
        [SerializeField] private BattleSimulationRunner _runner;

        /// <summary>The runner this scene drives (null until the scene has started).</summary>
        public BattleSimulationRunner Runner => _runner;

        /// <summary>Why the last start refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        private void Start() => TryStart();

        private void OnDestroy()
        {
            if (_runner is not null)
            {
                _runner.Shutdown();
            }
        }

        /// <summary>
        /// Builds and begins the floor. Idempotent: a second call while running does nothing,
        /// so a test may drive it without waiting for <c>Start</c>.
        /// </summary>
        public bool TryStart()
        {
            if (_runner is not null && _runner.IsRunning)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                StartError = "BattleSceneDirector has no built BattleSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            if (_runner is null)
            {
                StartError = "BattleSceneDirector has no BattleSimulationRunner.";
                Debug.LogError(StartError, this);
                return false;
            }

            BattleRunSource source = _scope.Container.Resolve<BattleRunSource>();
            if (!source.TryBuild(out BattleSetup setup, out int startingBia, out string error))
            {
                StartError = error;
                Debug.LogError("Battle scene could not start a floor: " + error, this);
                return false;
            }

            StartError = string.Empty;

            // The scene marker, published by the scene that is on screen. Title and the Thai text
            // spike publish their own the same way; leaving the Battle scene out would make a
            // `?scene=Battle` handover invisible to a browser test, which is the only place the
            // handover can be observed (a WebGL build cannot run Unity's test framework).
            WebGlBridge.PublishScene(gameObject.scene.name);

            var context = new BattleRunnerContext
            {
                Factory = _scope.Container.Resolve<IBattleSessionFactory>(),
                Setup = setup,
                StartingBia = startingBia,
                VisualSeed = source.Seed,
                Text = _scope.Container.Resolve<LocalizationService>(),
                Config = _scope.Container.Resolve<IGameConfig>(),
                HudState = _scope.Container.Resolve<BattleHudState>(),
                Hud = _scope.Container.Resolve<BattleHudPresenter>(),
                Speed = _scope.Container.Resolve<BattleSpeed>(),
            };

            _runner.Begin(context);
            return true;
        }
    }
}
