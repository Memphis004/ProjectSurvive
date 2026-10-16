using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
using VContainer;

namespace ProjectSurvive.Game.ResultScene
{
    /// <summary>
    /// The Result scene's composition root: the screen's own state and its presenter, and nothing
    /// else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same shape as <c>CampSceneScope</c> and <c>BattleSceneScope</c> — the one place a port meets
    /// its adapter (AGENTS rule 3), inheriting everything the shell binds from
    /// <see cref="RootLifetimeScope"/>. In the normal flow this scene is a child of the App scope,
    /// so it registers nothing but a UI state and the presenter; opened on its own (the Editor's
    /// scene list, a direct-load PlayMode test) the base class registers the shell for it and this
    /// scene is then its own whole game.
    /// </para>
    /// <para>
    /// <b>The run is not created here either.</b> It comes from the <see cref="RunScopeOwner"/> in
    /// the App scope — the same run the battle just settled — so the screen is summarising the run
    /// that was actually played rather than one built for the occasion. <c>RequireRun</c> is what
    /// creates one if this scene is opened with no run at all, and
    /// <see cref="ResultSceneDirector"/> is what notices that such a run has nothing to summarise.
    /// </para>
    /// </remarks>
    public sealed class ResultSceneScope : RootLifetimeScope
    {
        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // Every registration is an explicit factory, never a reflected constructor: the first
            // Web build failed on a constructor only reflection could see
            // (llmwiki/gotchas/il2cpp-di-and-reflection.md).
            builder.Register(_ => new ResultUiState(), Lifetime.Singleton);

            builder.Register(
                resolver => new ResultPresenter(
                    resolver.Resolve<RunScopeOwner>().RequireRun(),
                    resolver.Resolve<LocalizationService>(),
                    resolver.Resolve<ResultUiState>()),
                Lifetime.Singleton);
        }
    }
}
