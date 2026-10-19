using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Puts a pickup on the ground where a ghost fell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The queue comes from <see cref="DeathSystem"/>, so a drop exists exactly when a kill
    /// happened and never for a ghost that was merely hit.
    /// </para>
    /// <para>
    /// <b>What a drop is worth is <c>enemy.drop_value</c>.</b> The death system copies the
    /// row's value onto the drop it queues (Stage 07b), so this system never looks a def up
    /// on a hot path and a boss, an elite and a filler can pay differently. A value of 0 is
    /// legal — a ghost that pays nothing — but a negative one is rejected by the data
    /// validator.
    /// </para>
    /// <para>
    /// The pickup's <i>kind</i> is the def id of the ghost that dropped it, so presentation
    /// can pool the right sprite without a second lookup.
    /// </para>
    /// </remarks>
    public sealed class DropSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsFilter _pickupFilter = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _pickupFilter = systems.GetWorld()
                .Filter<Pickup>()
                .End(BattleArena.FilterDenseCapacity);
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;
            if (context.DropCount == 0)
            {
                return;
            }

            for (int i = 0; i < context.DropCount; i++)
            {
                if (_pickupFilter.GetEntitiesCount() >= BattleArena.MaxPickups)
                {
                    // A floor cannot carpet itself in drops. The cap is a memory bound: the
                    // oldest drops stay collectable and the newest simply are not spawned.
                    return;
                }

                float x = context.DropXAt(i);
                float y = context.DropYAt(i);
                int kind = context.DropDefIdAt(i);
                int value = context.DropValueAt(i);

                int entity = context.Spawner.SpawnPickup(x, y, kind, value);
                context.Emit(
                    BattleEventType.PickupSpawned,
                    context.Ref(entity),
                    kind,
                    kind,
                    BattleEventFlags.None,
                    x,
                    y);
            }
        }
    }
}
