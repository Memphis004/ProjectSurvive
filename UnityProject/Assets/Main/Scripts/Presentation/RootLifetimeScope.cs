using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The composition root: the single place where the game shell decides which concrete
    /// implementation fills each port — and, since Stage 10a, the <b>App scope</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only Presentation may reference Infrastructure (AGENTS rule 3 — the layers point
    /// Shared &lt;- Domain &lt;- Application &lt;- Presentation, and Infrastructure implements
    /// Application's ports). Everything upstream sees the port, never the adapter.
    /// </para>
    /// <para>
    /// Registrations are lazy on purpose: <see cref="IGameConfig"/> is built on the first resolve,
    /// so a broken or missing table fails where it is used and can be reported by the smoke probe
    /// instead of tearing down container construction.
    /// </para>
    /// <para>
    /// <b>Three lifetimes, three scopes (Stage 10a).</b> This scope is what the architecture doc
    /// calls the <b>App</b> scope — config, clock, localization, the UI state, the scene loader and
    /// the owner of the <b>Run</b> scope — and it is meant to outlive every scene:
    /// <see cref="PersistAcrossScenes"/>, called once by Boot, moves it into
    /// <c>DontDestroyOnLoad</c> and makes it the parent of every scene scope that loads afterwards.
    /// A scene scope (Camp, Battle, Title) is therefore a <i>child</i>, and inherits all of this
    /// rather than rebuilding it per scene.
    /// </para>
    /// <para>
    /// <b>The root registers; the children inherit.</b> <see cref="Configure"/> installs the shell
    /// only when it is the root (<c>Parent is null</c>). A scene scope that loads with no parent —
    /// the Editor's scene list, a direct-load PlayMode test, anything that never went through Boot
    /// — then registers the shell for itself and is still playable on its own, which is how Camp
    /// and Battle are photographed and tested today. The guard is what keeps a scene scope from
    /// creating a second copy of every singleton on the normal path.
    /// </para>
    /// <para>
    /// Not sealed since Stage 08: <c>BattleSceneScope</c> and <c>CampSceneScope</c> derive from it,
    /// so a scene inherits this wiring and adds only what the scene itself needs.
    /// </para>
    /// </remarks>
    public class RootLifetimeScope : LifetimeScope
    {
        [Tooltip("Run seed: every stream a run draws from is derived from it.")]
        [SerializeField] private uint _runSeed = RunScopeOwner.DefaultSeed;

        // Nullable because ParentOverrideScope is a struct: disposing it is what pops the global
        // parent override, so "none pushed" has to be distinguishable from "one pushed".
        private LifetimeScope.ParentOverrideScope? _parentOverride;
        private bool _persisted;

        /// <summary>The seed a run created in this scope's run scope is drawn from.</summary>
        public uint RunSeed => _runSeed;

        /// <summary>
        /// The <b>App</b> scope: this object is not unloaded with its scene, and every scene scope
        /// loaded afterwards becomes its child.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Called by <see cref="SceneBoot"/> — the Boot scene is the only scope that should do this,
        /// and the caller is the composition root's own scene rather than a name check inside the
        /// scope, so the intent is visible where the boot flow is written.
        /// </para>
        /// <para>
        /// <see cref="LifetimeScope.EnqueueParent"/> is VContainer's own mechanism for exactly this:
        /// the override is peeked by each new scene scope's <c>GetRuntimeParent()</c>, so a scene
        /// scope finds this one instead of being a root. It is held (not disposed immediately)
        /// because it has to apply to every later scene, and released in <c>OnDestroy</c>.
        /// </para>
        /// </remarks>
        public void PersistAcrossScenes()
        {
            if (_persisted)
            {
                return;
            }

            if (Parent is not null)
            {
                Debug.LogWarning(
                    "RootLifetimeScope.PersistAcrossScenes was called on a child scope; the App scope " +
                    "is the one that should persist.",
                    this);
                return;
            }

            _persisted = true;
            Object.DontDestroyOnLoad(gameObject);
            _parentOverride = LifetimeScope.EnqueueParent(this);
        }

        /// <summary>
        /// Undoes <see cref="PersistAcrossScenes"/>: the global parent override is released, so
        /// scene scopes loaded afterwards are roots again.
        /// </summary>
        /// <remarks>
        /// The counterpart exists because persistence is a <b>global</b> setting: whoever turned it
        /// on owns putting it back, which in practice is a PlayMode fixture that loads Boot and
        /// must not leave the next fixture parented to its App scope.
        /// </remarks>
        public void StopPersisting()
        {
            _parentOverride?.Dispose();
            _parentOverride = null;
            _persisted = false;
        }

        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent is not null)
            {
                // A child scope: the shell — and the run — come from the parent chain. Registering
                // them again would give the scene its own config, its own clock and its own run.
                return;
            }

            // The one place that knows whether this build may cheat (Stage 10c). It is decided here
            // — at the root, before anything can start a run — rather than in an `#if` scattered
            // through the screens, and it is a plain value in Domain so that "a release build is
            // off" is one expression (DevCheats.Allows) plus the shipped floor's own refusal (see
            // DevCheatTests). Every floor copies the answer into `BattleSetup.DevCheatsAllowed` when
            // it departs, so a run that left while cheats were off cannot be cheated afterwards.
            DevCheats.Configure(isDevelopmentBuild: Debug.isDebugBuild, isEditor: Application.isEditor);

            // Every registration is an explicit factory, never a reflected constructor. The first
            // Web build failed with "VContainerException: Type does not found injectable
            // constructor, type: SystemClock" because VContainer's type analysis needs a
            // ConstructorInfo and the IL2CPP build strips a constructor that no IL call site
            // reaches — the constructor is only ever seen by reflection. A factory keeps a real
            // call site, so the composition root cannot be stripped out from under itself. Mono
            // (Editor/tests) does not strip, so only a real player build can catch this; see the
            // browser test.
            builder.Register<IGameConfig>(_ => ResourcesGameConfigLoader.Load(), Lifetime.Singleton);
            builder.Register<IClock>(_ => new SystemClock(), Lifetime.Singleton);

            // LocalizationService takes an optional Language, which the container cannot resolve on
            // its own — so it is constructed explicitly.
            builder.Register(
                resolver => new LocalizationService(resolver.Resolve<IGameConfig>()),
                Lifetime.Singleton);

            builder.Register(_ => new UiState(), Lifetime.Singleton);

            // The run is not a singleton of this scope: it has a lifetime of its own (one run,
            // disposed on the way back to the title), and RunScopeOwner is the only thing that
            // creates or destroys it.
            builder.Register(
                resolver => new RunScopeOwner(this, _runSeed),
                Lifetime.Singleton);

            // The one way to change scene, for the whole game shell.
            builder.Register(
                resolver => new SceneLoader(resolver.Resolve<RunScopeOwner>()),
                Lifetime.Singleton);

            builder.Register(
                resolver => new TitlePresenter(
                    resolver.Resolve<LocalizationService>(),
                    resolver.Resolve<UiState>(),
                    resolver.Resolve<IGameConfig>()),
                Lifetime.Singleton);

            builder.Register(
                resolver => new Smoke.SmokeRunner(
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<LocalizationService>(),
                    resolver.Resolve<IClock>(),
                    resolver.Resolve<UiState>()),
                Lifetime.Singleton);

            builder.Register(
                resolver => new Thai.ThaiTextSpike(resolver.Resolve<LocalizationService>()),
                Lifetime.Singleton);
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            // Release the global parent override before the container goes away: a stale entry
            // would point later scene scopes at a disposed scope.
            StopPersisting();

            base.OnDestroy();
        }
    }
}
