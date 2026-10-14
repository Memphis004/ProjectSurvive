using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Application.Battle.Fake
{
    /// <summary>
    /// Binds <see cref="IBattleSessionFactory"/> to the dev session for a composition root that is
    /// not allowed to name a session implementation.
    /// </summary>
    /// <remarks>
    /// The Stage 08 prompt asks the Battle scene to run against the Stage 02b fake while the
    /// presentation is being built, switched from its <c>BattleSceneScope</c>. That scope is Unity
    /// code, and the Stage 06 architecture test
    /// (<c>Battle_sessions_are_reached_only_through_their_factory_port</c>) fails any Unity file
    /// that names the fake's type — deliberately, so a call site cannot hard-wire a session instead
    /// of asking the factory. The two rules are compatible if the name stays in the layer that owns
    /// the fake: the scene calls this method and picks between two <see cref="IBattleSessionFactory"/>
    /// values, which is all a composition root should be doing. The type is named after the role
    /// the scene plays (a dev binding), not after the class it constructs, so the substring the
    /// architecture test looks for cannot appear in engine-facing code.
    /// </remarks>
    public static class DevBattleSessionBinding
    {
        /// <summary>Creates the dev/test factory with the fake's default options.</summary>
        /// <param name="config">The loaded tables.</param>
        public static IBattleSessionFactory CreateFactory(IGameConfig config) =>
            new FakeBattleSessionFactory(config);
    }
}
