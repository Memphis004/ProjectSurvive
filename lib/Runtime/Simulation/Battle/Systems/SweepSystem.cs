using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// The end-of-floor sweep: a win pulls every drop still on the ground into the player
    /// before the floor is reported as finished (Stage 07c).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this lives in the simulation.</b> What a floor pays is part of its result, so a
    /// drop that is already on the ground when the timer runs out must be collected by the
    /// rule engine, not by a view. A view-driven magnet would make <c>CurrencyEarned</c>
    /// depend on how many frames were drawn, and the same replay would pay differently.
    /// </para>
    /// <para>
    /// <b>What runs during a sweep.</b> Nothing else does. The session steps
    /// <see cref="EcsBattleSimulation.Step"/> into this system and the frame publisher only, so
    /// no wave spawns, no ghost moves, no weapon casts, nothing new dies and no contact damage
    /// lands. The ghosts that were on the field stand still and are simply left standing when
    /// the result is produced — there is no second payout for them, because a kill is only
    /// ever paid by <see cref="DeathSystem"/>. That is the simplest reading of "the ghosts that
    /// are left are removed without dropping anything else".
    /// </para>
    /// <para>
    /// <b>It always ends.</b> The sweep closes when the last pickup is collected or when
    /// <c>constant.sweep_timeout_seconds</c> runs out; at the cap whatever is left is collected
    /// at once, so a slow ghost or an unreachable corner cannot hold the floor open. A pickup
    /// is credited exactly once either way: collecting it deletes the entity in the same tick.
    /// </para>
    /// <para>
    /// Collection is released after the enumeration, so no pool access ever follows a deletion
    /// (the same rule <see cref="PickupSystem"/> follows).
    /// </para>
    /// </remarks>
    public sealed class SweepSystem : IEcsInitSystem, IEcsRunSystem
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

            if (!context.SweepActive)
            {
                return;
            }

            int remaining = _filter.GetEntitiesCount();
            if (context.SweepTicksRemaining > 0)
            {
                context.SweepTicksRemaining--;
            }

            // At the cap every pickup is collected at once, wherever it is. That is what makes
            // the sweep bounded: the floor cannot wait for a drop the player cannot reach.
            bool atCap = context.SweepTicksRemaining <= 0;

            int player = context.PlayerEntity;
            bool hasPlayer = context.IsAlive(player);
            float playerX = 0f;
            float playerY = 0f;

            if (hasPlayer)
            {
                ref Position playerPosition = ref _positions.Get(player);
                playerX = playerPosition.X;
                playerY = playerPosition.Y;
            }

            float step = context.MagnetSpeed * BattleArena.TickDeltaSeconds;
            float reach = step * step;

            int collectedCount = 0;

            foreach (int entity in _filter)
            {
                ref Position position = ref _positions.Get(entity);
                float dx = playerX - position.X;
                float dy = playerY - position.Y;
                float squared = (dx * dx) + (dy * dy);

                if (!atCap && hasPlayer)
                {
                    if (squared > reach)
                    {
                        float distance = (float)Math.Sqrt(squared);
                        if (distance > 0f)
                        {
                            float scale = step / distance;
                            position.X += dx * scale;
                            position.Y += dy * scale;
                        }

                        continue;
                    }
                }

                if (collectedCount >= _collected.Length)
                {
                    // The release buffer is full for this tick (it is sized to the pickup cap,
                    // so the shipped floor cannot reach this). Leave the pickup for the next
                    // tick rather than paying it without being able to remove it: a paid pickup
                    // that survives would pay again, and "once per pickup" has no exception.
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

                _collected[collectedCount++] = entity;
                remaining--;
            }

            for (int i = 0; i < collectedCount; i++)
            {
                _world.DelEntity(_collected[i]);
            }

            if (remaining <= 0)
            {
                context.EndRequested = true;
            }
        }
    }
}
