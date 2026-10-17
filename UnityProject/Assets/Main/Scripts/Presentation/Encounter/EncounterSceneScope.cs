using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Game.Presentation.Flow;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Encounter
{
    /// <summary>
    /// The Encounter scene's composition root: its UI state and its presenter, and nothing else.
    /// </summary>
    /// <remarks>
    /// The run comes from the <see cref="RunScopeOwner"/> in the App scope — the same run the Hub just
    /// prepared — so the encounter is one unit of that run rather than a throwaway one built for the
    /// scene. Opened on its own, the owner builds a run and the scene is playable by itself.
    /// </remarks>
    public sealed class EncounterSceneScope : RootLifetimeScope
    {
        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register(_ => new EncounterUiState(), Lifetime.Singleton);

            builder.Register(
                resolver =>
                {
                    RunScopeOwner owner = resolver.Resolve<RunScopeOwner>();
                    return new EncounterPresenter(
                        owner.RequireRun(),
                        owner.Flow,
                        resolver.Resolve<LocalizationService>(),
                        resolver.Resolve<EncounterUiState>());
                },
                Lifetime.Singleton);
        }
    }
}
