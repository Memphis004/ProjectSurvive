using System.Collections.Generic;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Applies the tick's queued damage, carries an elemental effect onto the ghost it
    /// hit, and runs the effects already riding on a ghost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Element and status.</b> A hit whose skill carries an element leaves the
    /// <see cref="Status"/> row the <c>status</c> table declares for that element on the
    /// ghost: damage per application, cadence, duration and stack cap all come from the row
    /// (Stage 11a, replacing ADR 0017's "re-use the skill's damage" interim rule).
    /// </para>
    /// <para>
    /// <b>Once.</b> The damage queue can hold several hits on one ghost in one tick, and a
    /// hit that lands after the ghost is already at zero is dropped: an entity dies from
    /// exactly one application, and the death system is the only place that counts a kill.
    /// </para>
    /// <para>
    /// <b>The player's HP lives in the entity</b>, not in a mirrored field, so the HUD, the
    /// floor-end rule and the result all read one number.
    /// </para>
    /// </remarks>
    public sealed class DamageStatusSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<Status> _statuses = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsFilter _statusFilter = null!;

        /// <summary>Status row by element value, built once at session start.</summary>
        private StatusConfig?[] _byElement = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _healths = world.GetPool<Health>();
            _statuses = world.GetPool<Status>();
            _enemies = world.GetPool<EnemyTag>();
            _statusFilter = world.Filter<Status>().Inc<Health>().End(BattleArena.FilterDenseCapacity);

            // One lookup per element, allocated once: a hit must not scan the table on a
            // tick whose whole point is to allocate nothing.
            IReadOnlyList<StatusConfig> rows = _context.Config.Statuses;
            int highest = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if ((int)rows[i].Element > highest)
                {
                    highest = (int)rows[i].Element;
                }
            }

            _byElement = new StatusConfig?[highest + 1];
            for (int i = 0; i < rows.Count; i++)
            {
                StatusConfig row = rows[i];
                int element = (int)row.Element;
                if (element > 0 && _byElement[element] is null)
                {
                    _byElement[element] = row;
                }
            }
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            TickStatuses(_context);
            ApplyQueuedHits(_context);
            HurtPlayer(_context);
        }

        private void TickStatuses(BattleContext context)
        {
            foreach (int entity in _statusFilter)
            {
                if (_healths.Get(entity).Hp <= 0)
                {
                    continue;
                }

                ref Status status = ref _statuses.Get(entity);
                status.RemainingTicks--;

                if (status.RemainingTicks <= 0)
                {
                    context.Emit(
                        BattleEventType.StatusExpired,
                        context.Ref(entity),
                        status.DefId,
                        status.Element);
                    _statuses.Del(entity);
                    continue;
                }

                status.NextTickIn--;
                if (status.NextTickIn > 0)
                {
                    continue;
                }

                // The burning ghost takes its own damage; a burn is not an element, so
                // nothing rides on the damage it deals.
                context.AddHit(entity, status.DamagePerTick, 0, status.DefId);
                status.NextTickIn = status.TickInterval;
            }
        }

        private void ApplyQueuedHits(BattleContext context)
        {
            int queued = context.HitCount;

            for (int i = 0; i < queued; i++)
            {
                int target = context.HitTargetAt(i);
                if (!context.IsAlive(target))
                {
                    continue;
                }

                ref Health health = ref _healths.Get(target);
                if (health.Hp <= 0)
                {
                    continue;
                }

                int damage = context.HitDamageAt(i);
                int remaining = health.Hp - damage;
                health.Hp = remaining < 0 ? 0 : remaining;

                int element = context.HitElementAt(i);
                int defId = context.HitDefIdAt(i);

                ref Position position = ref _positions.Get(target);
                context.Emit(
                    BattleEventType.DamageDealt,
                    context.Ref(target),
                    defId,
                    damage,
                    BattleEventFlags.None,
                    position.X,
                    position.Y);

                if (_enemies.Has(target) && _enemies.Get(target).IsBoss)
                {
                    context.BossHp = health.Hp;
                }

                if (element != 0)
                {
                    ApplyStatus(target, element);
                }
            }
        }

        private void ApplyStatus(int target, int element)
        {
            if (element <= 0 || element >= _byElement.Length)
            {
                return;
            }

            StatusConfig? config = _byElement[element];
            if (config is null)
            {
                // No row for this element means no effect: the battle never invents one.
                return;
            }

            int defId = _context.DefIds.Status(config.Id);

            if (_statuses.Has(target))
            {
                ref Status existing = ref _statuses.Get(target);
                StatusRules.Apply(ref existing, config, defId);
                return;
            }

            ref Status status = ref _statuses.Add(target);
            StatusRules.Apply(ref status, config, defId);
        }

        private void HurtPlayer(BattleContext context)
        {
            int damage = context.PlayerHitDamage;
            if (damage <= 0)
            {
                return;
            }

            int player = context.PlayerEntity;
            ref Health health = ref _healths.Get(player);
            if (context.GodMode || health.Hp <= 0)
            {
                return;
            }

            int remaining = health.Hp - damage;
            health.Hp = remaining < 0 ? 0 : remaining;

            ref Position position = ref _positions.Get(player);
            context.Emit(
                BattleEventType.PlayerDamaged,
                context.Ref(player),
                -1,
                damage,
                BattleEventFlags.None,
                position.X,
                position.Y);
        }
    }
}
