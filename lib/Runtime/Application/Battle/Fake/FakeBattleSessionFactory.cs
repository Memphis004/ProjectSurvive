using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Battle.Fake
{
    /// <summary>
    /// Creates <see cref="FakeBattleSession"/> instances. Config tables arrive
    /// through the constructor, so the factory (not each session) owns the
    /// balance data — the same shape the real EcsLite factory will have.
    /// </summary>
    /// <remarks>
    /// The battle RNG stream is derived from <see cref="BattleSetup.Seed"/> exactly
    /// the way the real simulation must (<c>RandomStreams.DeriveStreamSeed</c>,
    /// <see cref="RandomStreamKind.Battle"/>), so a replay through this factory and
    /// a replay through the real one start from the same stream state.
    /// </remarks>
    public sealed class FakeBattleSessionFactory : IBattleSessionFactory
    {
        private readonly IGameConfig _config;
        private readonly FakeBattleOptions _options;

        /// <summary>Creates a factory.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public FakeBattleSessionFactory(IGameConfig config, FakeBattleOptions? options = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _options = options ?? new FakeBattleOptions();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="setup"/> is null.</exception>
        /// <exception cref="ArgumentException"><see cref="BattleSetup.StageId"/> is empty or not in the floor table.</exception>
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

            int bossMaxHp = 0;
            if (floor.IsBoss
                && !string.IsNullOrEmpty(floor.BossEnemyId)
                && _config.TryGetEnemy(floor.BossEnemyId, out EnemyConfig boss))
            {
                bossMaxHp = boss.MaxHp;
            }

            var battle = new Xoshiro128(RandomStreams.DeriveStreamSeed(setup.Seed, RandomStreamKind.Battle));
            return new FakeBattleSession(setup, floor, _options, battle, bossMaxHp);
        }
    }
}
