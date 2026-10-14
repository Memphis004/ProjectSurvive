using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Rebuilds the spatial grid from the ghosts' positions, once per tick, so collision
    /// and target picking never scan every entity against every entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A grid is built and thrown away every tick on purpose: a ghost moves a little, a
    /// grid that had to be kept in step with insertions and removals would be more state
    /// (and more places to be wrong) than one <see cref="BattleSpatialHash.Begin"/> plus N
    /// inserts. The rebuild is linear in the number of ghosts and allocates nothing.
    /// </para>
    /// <para>
    /// Ghosts that are already at zero HP are <b>not</b> inserted: they are removed by the
    /// death system later in the same tick, and leaving them out means a projectile cannot
    /// spend its pierce on a corpse.
    /// </para>
    /// </remarks>
    public sealed class SpatialHashSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Health> _healths = null!;
        private EcsFilter _filter = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _healths = world.GetPool<Health>();
            _filter = world.Filter<EnemyTag>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleSpatialHash hash = _context.Hash;
            hash.Begin();

            foreach (int entity in _filter)
            {
                if (_healths.Get(entity).Hp <= 0)
                {
                    continue;
                }

                ref Position position = ref _positions.Get(entity);
                hash.Insert(entity, position.X, position.Y);
            }
        }
    }
}
