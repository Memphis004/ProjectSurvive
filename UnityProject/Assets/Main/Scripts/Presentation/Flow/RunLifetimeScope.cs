using ProjectSurvive.Core.Application.Run;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// The <b>Run</b> scope: everything whose lifetime is one run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>App</b> scope (<see cref="RootLifetimeScope"/>, built by Boot) holds what lives until the
    /// game closes. This scope holds the run: created when the title starts one, alive across every
    /// Hub ⇄ Encounter crossing, disposed when the player arrives back at the title. A scene scope
    /// reaches it through <see cref="RunScopeOwner"/>, so leaving the Hub does not take the run with
    /// it.
    /// </para>
    /// <para>
    /// It is created by <see cref="RunScopeOwner"/> rather than by a component in a scene, and every
    /// registration is an explicit factory, never a reflected constructor (the first Web build failed
    /// on a constructor only reflection could see — <c>llmwiki/gotchas/il2cpp-di-and-reflection.md</c>).
    /// </para>
    /// </remarks>
    public sealed class RunLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// The run seed, set by <see cref="RunScopeOwner"/> before the scope is activated so the run is
        /// drawn the moment the child builds.
        /// </summary>
        public uint Seed { get; set; } = RunScopeOwner.DefaultSeed;

        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register(_ => new RunSession(Seed), Lifetime.Singleton);

            // The loop's state machine, built around the run it drives. It answers "may this scene
            // change from here" without touching Unity, so the answer is testable headlessly.
            builder.Register(
                resolver => new SceneFlow(resolver.Resolve<RunSession>()),
                Lifetime.Singleton);
        }
    }
}
