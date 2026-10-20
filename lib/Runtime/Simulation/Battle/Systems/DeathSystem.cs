using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Removes the ghosts at zero HP: counts the kill once, announces it, queues the drop
    /// and releases the entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exactly once.</b> The kill counter and the <c>EnemyKilled</c> event happen in
    /// this one place, and the entity is gone before the next tick, so no path can pay a
    /// ghost out twice — "ศัตรูตายให้รางวัลครั้งเดียว".
    /// </para>
    /// <para>
    /// The player is <b>not</b> destroyed here. Being at zero HP ends the floor
    /// (<see cref="FloorEndSystem"/>), and the run keeps the last HP it saw
    /// (<c>BattleResult.FinalHp</c>); a destroyed player entity would have to be re-created
    /// for the render snapshot that describes the losing tick.
    /// </para>
    /// <para>
    /// <c>DevKillAll</c> arrives as <see cref="BattleContext.KillAllRequested"/>: the system
    /// zeroes every ghost's HP and then lets the ordinary rule above run, so a dev kill and
    /// a fought kill produce the same events and the same drops.
    /// </para>
    /// <para>
    /// <b>Stage 11c: a boss eats the corpses around it.</b> A ghost that dies inside the boss's
    /// <c>boss_corpse_range</c> gives the boss <c>boss_corpse_heal</c> HP, never above its max,
    /// and the meal is published as <see cref="BattleEventType.CorpseConsumed"/> with the HP it
    /// actually regained. This is the one place a death is known and the body still exists, so
    /// it is where the rule lives; a boss that has just died is at 0 HP and eats nothing. The
    /// boss's own death ends the possession it was holding, because the clock belongs to the
    /// ghost that left the corpse-eater on the field.
    /// </para>
    /// </remarks>
    public sealed class DeathSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsWorld _world = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsPool<BossMind> _bossMinds = null!;
        private EcsPool<Possessed> _possessed = null!;
        private EcsFilter _filter = null!;
        private int[] _dead = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _world = systems.GetWorld();
            _positions = _world.GetPool<Position>();
            _healths = _world.GetPool<Health>();
            _enemies = _world.GetPool<EnemyTag>();
            _bossMinds = _world.GetPool<BossMind>();
            _possessed = _world.GetPool<Possessed>();
            _filter = _world.Filter<EnemyTag>().Inc<Health>().End(BattleArena.FilterDenseCapacity);
            _dead = new int[BattleArena.MaxEnemies];
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;

            if (context.KillAllRequested)
            {
                context.KillAllRequested = false;
                foreach (int entity in _filter)
                {
                    ref Health health = ref _healths.Get(entity);
                    if (health.Hp > 0)
                    {
                        health.Hp = 0;
                    }
                }
            }

            int deadCount = 0;

            foreach (int entity in _filter)
            {
                if (_healths.Get(entity).Hp > 0)
                {
                    continue;
                }

                EnemyTag tag = _enemies.Get(entity);
                ref Position position = ref _positions.Get(entity);
                float x = position.X;
                float y = position.Y;

                context.Kills++;
                context.Emit(
                    BattleEventType.EnemyKilled,
                    context.Ref(entity),
                    tag.DefId,
                    context.Kills,
                    Flags(tag),
                    x,
                    y);
                context.AddDrop(x, y, tag.DefId, tag.DropValue);

                if (tag.IsBoss)
                {
                    context.BossHp = 0;
                    context.BossEntity = -1;
                    EndPossession(context);
                }
                else
                {
                    EatCorpse(context, x, y);
                }

                if (deadCount < _dead.Length)
                {
                    _dead[deadCount++] = entity;
                }
            }

            for (int i = 0; i < deadCount; i++)
            {
                _world.DelEntity(_dead[i]);
            }

            context.EnemiesAlive -= deadCount;
            if (context.EnemiesAlive < 0)
            {
                context.EnemiesAlive = 0;
            }
        }

        /// <summary>
        /// Feeds a corpse to the floor's boss when it fell inside its reach (Stage 11c).
        /// </summary>
        /// <remarks>
        /// The heal is capped at the boss's max and only the <i>actual</i> regain is published, so a
        /// boss at full health pays nothing and the event never claims HP it did not get. The corpse's
        /// own max HP is deliberately not read: the row decides what a corpse is worth, so two ghost
        /// kinds are the same meal and a tuning pass changes one number.
        /// </remarks>
        private void EatCorpse(BattleContext context, float x, float y)
        {
            int boss = context.BossEntity;
            if (!context.IsAlive(boss) || !_bossMinds.Has(boss) || !_positions.Has(boss))
            {
                return;
            }

            ref BossMind mind = ref _bossMinds.Get(boss);
            ref Health health = ref _healths.Get(boss);
            if (mind.CorpseHeal <= 0 || health.Hp <= 0)
            {
                return;
            }

            ref Position bossPosition = ref _positions.Get(boss);
            if (!BossRules.WithinReach(
                    bossPosition.X, bossPosition.Y, mind.CorpseRange, x, y, 0f))
            {
                return;
            }

            int regained = BossRules.HealFromCorpse(health.Hp, health.MaxHp, mind.CorpseHeal);
            if (regained <= 0)
            {
                return;
            }

            health.Hp += regained;
            context.BossHp = health.Hp;

            context.Emit(
                BattleEventType.CorpseConsumed,
                context.Ref(boss),
                _enemies.Get(boss).DefId,
                regained,
                BattleEventFlags.Boss,
                x,
                y);
        }

        /// <summary>Clears the reversal the dying boss was holding over the player.</summary>
        private void EndPossession(BattleContext context)
        {
            int player = context.PlayerEntity;
            if (context.IsAlive(player) && _possessed.Has(player))
            {
                _possessed.Get(player).RemainingTicks = 0;
            }
        }

        private static BattleEventFlags Flags(in EnemyTag tag)
        {
            if (tag.IsBoss)
            {
                return BattleEventFlags.Boss;
            }

            return tag.IsElite ? BattleEventFlags.Elite : BattleEventFlags.None;
        }
    }
}
