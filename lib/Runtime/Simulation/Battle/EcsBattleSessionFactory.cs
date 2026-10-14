using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// Creates <see cref="EcsBattleSimulation"/> sessions — the real battle, replacing
    /// <c>FakeBattleSessionFactory</c> at one binding in the composition root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shaped exactly like the fake's factory on purpose (same constructor, same
    /// validation, same seed derivation), so swapping one for the other changes which
    /// session runs and nothing else. That parity is what <c>BattleSessionContractTests</c>
    /// holds both implementations to.
    /// </para>
    /// <para>
    /// The factory owns what is expensive and shared: the config snapshot, the def-id map
    /// built once from it, and the knobs. A session is created per floor and thrown away.
    /// </para>
    /// </remarks>
    public sealed class EcsBattleSessionFactory : IBattleSessionFactory
    {
        private readonly IGameConfig _config;
        private readonly EcsBattleOptions _options;
        private readonly BattleDefIds _defIds;

        /// <summary>Creates a factory.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public EcsBattleSessionFactory(IGameConfig config, EcsBattleOptions? options = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _options = options ?? new EcsBattleOptions();
            _defIds = new BattleDefIds(config);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="setup"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <see cref="BattleSetup.StageId"/> is empty or not in the floor table.
        /// </exception>
        public IBattleSession Create(BattleSetup setup)
        {
            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (string.IsNullOrEmpty(setup.StageId))
            {
                throw new ArgumentException("BattleSetup.StageId is required.", nameof(setup));
            }

            if (!_config.TryGetFloor(setup.StageId, out FloorConfig floor))
            {
                throw new ArgumentException("Unknown floor id '" + setup.StageId + "'.", nameof(setup));
            }

            var battle = new Xoshiro128(RandomStreams.DeriveStreamSeed(setup.Seed, RandomStreamKind.Battle));
            return new EcsBattleSimulation(setup, floor, _config, _options, battle, _defIds);
        }
    }
}
