using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.Flow;
using VContainer;

namespace ProjectSurvive.Game.Camp
{
    /// <summary>
    /// The Camp scene's composition root: the screen's own state and its presenter, and nothing
    /// else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same shape as <c>BattleSceneScope</c> — the one place a port meets its adapter (AGENTS rule
    /// 3), inheriting everything the shell binds (config, clock, localization) from
    /// <see cref="RootLifetimeScope"/>, which since Stage 10a only registers the shell when it is
    /// the <b>App</b> scope. As a scene scope in the normal flow this class is a <i>child</i> of
    /// the App scope and registers nothing but a UI state and the presenter.
    /// </para>
    /// <para>
    /// <b>The run is no longer created here.</b> Stage 09 built a <c>RunSession</c> in this scope
    /// and its comment recorded the consequence as a known limit: "the run is created here, so
    /// leaving the Camp scene would take it with it". That limit is what Stage 10a closes — the run
    /// lives in the <see cref="RunScopeOwner"/>'s Run scope, which is a child of the App scope and
    /// therefore outlives this scene. The presenter asks the owner for it, so a camp that opens
    /// after a battle edits the <b>same</b> wallet, bag and shop the battle left behind.
    /// </para>
    /// <para>
    /// A camp opened with no App scope above it (the Editor's scene list, a direct-load PlayMode
    /// test) is a root, so the base class registers the shell and a local
    /// <see cref="RunScopeOwner"/> for it — and this scene is then its own whole game, which is how
    /// it is photographed and tested.
    /// </para>
    /// </remarks>
    public sealed class CampSceneScope : RootLifetimeScope
    {
        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // Every registration is an explicit factory, never a reflected constructor: the first
            // Web build failed on a constructor only reflection could see
            // (llmwiki/gotchas/il2cpp-di-and-reflection.md).
            builder.Register(_ => new CampUiState(), Lifetime.Singleton);

            builder.Register(
                resolver => new CampPresenter(
                    // The run comes from the Run scope. RequireRun is what creates one if the
                    // player arrived here without a run (a direct `?scene=Camp` handover): the camp
                    // is the one screen that may start one.
                    resolver.Resolve<RunScopeOwner>().RequireRun(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<LocalizationService>(),
                    resolver.Resolve<CampUiState>(),
                    // The first-run checklist is shown once per profile (Stage 13b). The flag lives
                    // with the other player settings, so it survives a browser reload and is the
                    // same file the settings screen writes.
                    resolver.Resolve<SettingsService>().Current.TutorialCompleted),
                Lifetime.Singleton);
        }
    }
}
