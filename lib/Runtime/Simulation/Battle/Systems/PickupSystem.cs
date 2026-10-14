using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// The magnet: a drop that comes within the player's pickup radius is collected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Collection is <b>instantaneous inside <c>constant.pickup_radius</c></b> rather than a
    /// drift towards the player. A moving magnet needs a speed, and there is no column for
    /// one — the radius is the only pickup number the data has, and it is the one the bag
    /// resolver already folds into the loadout (so a piece that widens the radius works by
    /// walking near a drop). A magnet speed is named in ADR 0017 as the thing that changes
    /// this from a test to an animation.
    /// </para>
    /// <para>
    /// Collected drops are released after the enumeration, so no pool access ever follows a
    /// deletion.
    /// </para>
    /// </remarks>
    public sealed class PickupSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsWorld _world = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Pickup> _pickups = null!;
        private EcsFilter _filter = null!;
        private int[] _collected = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _world = systems.GetWorld();
            _positions = _world.GetPool<Position>();
            _pickups = _world.GetPool<Pickup>();
            _filter = _world.Filter<Pickup>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
            _collected = new int[BattleArena.MaxPickups];
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;
            int player = context.PlayerEntity;

            if (!context.IsAlive(player))
            {
                return;
            }

            ref Position playerPosition = ref _positions.Get(player);
            float playerX = playerPosition.X;
            float playerY = playerPosition.Y;
            float radius = context.PickupRadius;
            float reach = radius * radius;

            int collectedCount = 0;

            foreach (int entity in _filter)
            {
                ref Position position = ref _positions.Get(entity);
                float dx = position.X - playerX;
                float dy = position.Y - playerY;

                if (((dx * dx) + (dy * dy)) > reach)
                {
                    continue;
                }

                ref Pickup pickup = ref _pickups.Get(entity);
                context.CurrencyEarned += pickup.Value;
                context.PickupsCollected++;
                context.Emit(
                    BattleEventType.PickupCollected,
                    context.Ref(entity),
                    pickup.Kind,
                    pickup.Kind,
                    BattleEventFlags.None,
                    position.X,
                    position.Y);

                if (collectedCount < _collected.Length)
                {
                    _collected[collectedCount++] = entity;
                }
            }

            for (int i = 0; i < collectedCount; i++)
            {
                _world.DelEntity(_collected[i]);
            }
        }
    }
}
