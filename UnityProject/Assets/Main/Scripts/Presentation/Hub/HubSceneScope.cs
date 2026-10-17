using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Game.Presentation.Flow;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Hub
{
    /// <summary>
    /// The Hub scene's composition root: its UI state and its presenter, and nothing else.
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="ProjectSurvive.Game.ResultScene.ResultSceneScope"/> — inherits
    /// everything the shell binds from <see cref="RootLifetimeScope"/>, and asks the
    /// <see cref="RunScopeOwner"/> for the run the loop is already carrying. Opened on its own (the
    /// Editor's scene list, a direct-load PlayMode test) the owner builds a run for it, so one scene is
    /// always playable without the boot flow.
    /// </remarks>
    public sealed class HubSceneScope : RootLifetimeScope
    {
        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register(_ => new HubUiState(), Lifetime.Singleton);

            builder.Register(
                resolver =>
                {
                    RunScopeOwner owner = resolver.Resolve<RunScopeOwner>();
                    return new HubPresenter(
                        owner.RequireRun(),
                        owner.Flow,
                        resolver.Resolve<LocalizationService>(),
                        resolver.Resolve<HubUiState>());
                },
                Lifetime.Singleton);
        }
    }
}
