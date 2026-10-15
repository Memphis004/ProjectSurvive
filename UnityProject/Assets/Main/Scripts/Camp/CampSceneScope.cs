using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Camp
{
    /// <summary>
    /// The Camp scene's composition root: it binds the run the camp screen edits and the screen's
    /// own state, and adds nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same shape as <c>BattleSceneScope</c> — the one place a port meets its adapter (AGENTS rule
    /// 3), inheriting everything the shell already binds (config, clock, localization) from
    /// <see cref="RootLifetimeScope"/>. It is deliberately smaller than the battle's: a camp has no
    /// simulation, no session factory and no view settings, so the run plus one UI state is the
    /// whole graph.
    /// </para>
    /// <para>
    /// <b>The run lives in this scene's scope, and that is a known Stage 09 limit.</b> The run is
    /// created here, so leaving the Camp scene would take it with it. Stage 10 owns the scene that
    /// consumes a departure, so it owns the decision about where a run outlives a scene; guessing
    /// it here would be a lifetime decision made without the flow it has to serve — the same
    /// reasoning the shell's original scope recorded (see
    /// <see cref="RootLifetimeScope"/>'s remarks). Depart still performs the real
    /// <c>StartFloor</c> command, so the state a handover would carry is real; only the handover
    /// itself is missing.
    /// </para>
    /// </remarks>
    public sealed class CampSceneScope : RootLifetimeScope
    {
        [Tooltip("Run seed: every stream the run draws from is derived from it.")]
        [SerializeField] private uint _runSeed = 20261015u;

        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // Every registration is an explicit factory, never a reflected constructor: the first
            // Web build failed on a constructor only reflection could see
            // (llmwiki/gotchas/il2cpp-di-and-reflection.md).
            builder.Register<IBackpackSnapshotCodec>(
                _ => new MessagePackBackpackSnapshotCodec(), Lifetime.Singleton);

            builder.Register(
                resolver => new RunSession(
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<IBackpackSnapshotCodec>(),
                    _runSeed),
                Lifetime.Singleton);

            builder.Register(_ => new CampUiState(), Lifetime.Singleton);

            builder.Register(
                resolver => new CampPresenter(
                    resolver.Resolve<RunSession>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<LocalizationService>(),
                    resolver.Resolve<CampUiState>()),
                Lifetime.Singleton);
        }
    }
}
