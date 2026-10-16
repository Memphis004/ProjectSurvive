using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// The <b>Run</b> scope: everything whose lifetime is one run (Stage 10a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three lifetimes the architecture doc names are three scopes now. The <b>App</b> scope
    /// (<see cref="RootLifetimeScope"/>, built by Boot) holds what lives until the game closes —
    /// config, clock, localization, the UI state and the scene loader. This scope holds the run:
    /// created when a run begins, disposed when the player goes back to the title. A scene scope
    /// (Camp, Battle, Title) is a child of the App scope and reaches the run through
    /// <see cref="RunScopeOwner"/>, so leaving the camp does not take the wallet with it — the bug
    /// the Stage 09 comment in <c>CampSceneScope</c> named as "a known Stage 09 limit".
    /// </para>
    /// <para>
    /// It is created by <see cref="RunScopeOwner"/> rather than by a component in a scene, because
    /// nothing about it belongs to a scene: the GameObject it lives on is parented to the App
    /// scope's (which is <c>DontDestroyOnLoad</c>) and is destroyed when the run ends. Every
    /// registration is an explicit factory, never a reflected constructor (the first Web build
    /// failed on a constructor only reflection could see —
    /// <c>llmwiki/gotchas/il2cpp-di-and-reflection.md</c>).
    /// </para>
    /// </remarks>
    public sealed class RunLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// The run seed, set by <see cref="RunScopeOwner"/> before the scope is activated so the
        /// run is drawn the moment the child builds.
        /// </summary>
        public uint Seed { get; set; } = RunScopeOwner.DefaultSeed;

        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            // The bag snapshot codec is Application-facing and serialization-backed, so it lives
            // here rather than in a scene: the run owns the bag it snapshots for undo and for
            // "คืนสภาพตอนเข้าศาลา".
            builder.Register<IBackpackSnapshotCodec>(
                _ => new MessagePackBackpackSnapshotCodec(), Lifetime.Singleton);

            builder.Register(
                resolver => new RunSession(
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<IBackpackSnapshotCodec>(),
                    Seed),
                Lifetime.Singleton);
        }
    }
}
