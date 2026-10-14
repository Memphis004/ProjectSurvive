using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The composition root: the single place where the game shell decides which
    /// concrete implementation fills each port.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only Presentation may reference Infrastructure (AGENTS rule 3 — the layers
    /// point Shared &lt;- Domain &lt;- Application &lt;- Presentation, and
    /// Infrastructure implements Application's ports). Everything upstream sees the
    /// port, never the adapter.
    /// </para>
    /// <para>
    /// Registrations are lazy on purpose: <see cref="IGameConfig"/> is built on the
    /// first resolve, so a broken or missing table fails where it is used and can be
    /// reported by the smoke probe instead of tearing down container construction.
    /// </para>
    /// <para>
    /// Each scene carries its own scope for Stage 03. A scope that survives scene
    /// loading is a run-level concern (Stage 06); inventing one now would be a
    /// lifetime decision made without the run flow it has to serve.
    /// </para>
    /// <para>
    /// Not sealed since Stage 08: <c>BattleSceneScope</c> derives from it, so the Battle
    /// scene inherits everything the shell binds (config, clock, localization, UI state)
    /// and adds only what a battle needs. A scene that starts from nothing but this class
    /// would otherwise keep a parent/child pair of scopes in step by hand.
    /// </para>
    /// </remarks>
    public class RootLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// Table bytes come from <c>Resources/Config</c>. Kept behind the
        /// <c>ResourcesGameConfigLoader</c> seam so a future
        /// streaming/Addressables path does not change this class.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            // Every registration is an explicit factory, never a reflected
            // constructor. The first Web build failed with
            // "VContainerException: Type does not found injectable constructor, type:
            // SystemClock" because VContainer's type analysis needs a
            // ConstructorInfo and the IL2CPP build strips a constructor that no IL
            // call site reaches — the constructor is only ever seen by reflection.
            // A factory keeps a real call site, so the composition root cannot be
            // stripped out from under itself. Mono (Editor/tests) does not strip, so
            // only a real player build can catch this; see the browser test.
            builder.Register<IGameConfig>(_ => ResourcesGameConfigLoader.Load(), Lifetime.Singleton);
            builder.Register<IClock>(_ => new SystemClock(), Lifetime.Singleton);

            // LocalizationService takes an optional Language, which the container
            // cannot resolve on its own — so it is constructed explicitly.
            builder.Register(
                resolver => new LocalizationService(resolver.Resolve<IGameConfig>()),
                Lifetime.Singleton);

            builder.Register(_ => new UiState(), Lifetime.Singleton);

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
    }
}
