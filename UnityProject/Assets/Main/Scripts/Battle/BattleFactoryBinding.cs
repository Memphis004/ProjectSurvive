using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Game.Battle
{
    /// <summary>
    /// The factory the game actually plays with, reachable from code that may not name the
    /// simulation assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ProjectSurvive.Game.Battle</c> already references <c>Core.Simulation</c> (the Battle
    /// scene's scope registers the real session), while the Editor assembly deliberately does
    /// not: an Editor tool that could reach into the ECS world would make "Presentation reads the
    /// contract" a claim rather than a boundary. This is the one door — Editor captures that need
    /// a real floor (Stage 11b's telegraph shot) ask for a factory here and still only see
    /// <see cref="IBattleSessionFactory"/>.
    /// </para>
    /// <para>
    /// It returns the shipped factory, never the Stage 02b dev session, so a capture is of the
    /// battle the player gets.
    /// </para>
    /// </remarks>
    public static class BattleFactoryBinding
    {
        /// <summary>Creates the shipped battle session factory.</summary>
        /// <param name="config">The loaded tables.</param>
        /// <returns>The factory the game uses.</returns>
        public static IBattleSessionFactory Create(IGameConfig config) =>
            new Core.Simulation.Battle.EcsBattleSessionFactory(config);
    }
}
