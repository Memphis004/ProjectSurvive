using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Casts automatically: regenerates focus, runs every casting clock down, and fires
    /// the ready weapon whose base cooldown is the shortest — the prompt's "AutoCast
    /// (MP, cooldown ต่ำสุด)".
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no aiming input and no target selection UI: the prototype's single class
    /// casts by itself, at the nearest ghost, and the player plays by moving. Which
    /// weapon fires is decided by <b>lowest cooldown first</b>, which is the whole of the
    /// prototype's auto-cast rule (GDD v0.3 has no ability slots or priority list, and
    /// inventing one here would be a design decision, not an implementation one).
    /// </para>
    /// <para>
    /// <b>MP is a real cost, not decoration.</b> A cast is refused when the pool is short,
    /// so a build whose casts cost more than its focus income casts less often than its
    /// cooldowns would allow — the number the balance pass will be tuning. Cooldowns still
    /// run down while there is nothing to hit, so "casts per floor" can only ever be lower
    /// than <c>floor(duration / cooldown)</c>.
    /// </para>
    /// <para>
    /// Targeting reads the spatial hash, which <see cref="SpatialHashSystem"/> rebuilds
    /// later in the same tick — so the target is <b>one tick old</b>, and the liveness
    /// check below is required rather than defensive: a ghost that died in the previous
    /// tick may still be in the grid.
    /// </para>
    /// </remarks>
    public sealed class AutoCastSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Mana> _manas = null!;
        private EcsPool<Caster> _casters = null!;
        private EcsFilter _casterFilter = null!;
        private int[] _scratch = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _manas = world.GetPool<Mana>();
            _casters = world.GetPool<Caster>();
            _casterFilter = world.Filter<Caster>().End(BattleArena.FilterDenseCapacity);
            _scratch = new int[BattleArena.MaxEnemies];
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

            ref Mana mana = ref _manas.Get(player);

            RegainFocus(context, ref mana);
            TickCooldowns();

            if (context.EnemiesAlive <= 0)
            {
                return;
            }

            ref Position playerPosition = ref _positions.Get(player);
            float x = playerPosition.X;
            float y = playerPosition.Y;

            if (!context.Hash.TryFindNearest(x, y, BattleArena.Radius, out int target, out float _))
            {
                return;
            }

            if (!context.IsAlive(target))
            {
                return;
            }

            int best = SelectCaster(mana.Mp);
            if (best < 0)
            {
                return;
            }

            ref Caster caster = ref _casters.Get(best);
            int behavior = caster.Behavior;

            caster.CooldownTicks = caster.PeriodTicks;
            mana.Mp -= caster.MpCost;

            if (behavior == (int)SkillBehavior.Projectile || behavior == (int)SkillBehavior.Pierce)
            {
                FireProjectiles(context, ref caster, x, y, target);
            }
            else
            {
                StrikeEveryGhostInReach(context, ref caster, x, y);
            }
        }

        private void RegainFocus(BattleContext context, ref Mana mana)
        {
            if (mana.Mp >= mana.MaxMp)
            {
                mana.Carry = 0f;
                return;
            }

            mana.Carry += context.FocusPerSecond * BattleArena.TickDeltaSeconds;
            int gain = (int)mana.Carry;
            if (gain <= 0)
            {
                return;
            }

            mana.Carry -= gain;
            mana.Mp += gain;
            if (mana.Mp > mana.MaxMp)
            {
                mana.Mp = mana.MaxMp;
            }
        }

        private void TickCooldowns()
        {
            foreach (int entity in _casterFilter)
            {
                ref Caster caster = ref _casters.Get(entity);
                if (caster.CooldownTicks > 0)
                {
                    caster.CooldownTicks--;
                }
            }
        }

        /// <summary>
        /// The ready, affordable caster with the shortest cooldown. A tie keeps the lowest
        /// entity id, which is the first loadout entry, so the choice is deterministic.
        /// </summary>
        private int SelectCaster(int availableMp)
        {
            int best = -1;
            int bestPeriod = int.MaxValue;

            foreach (int entity in _casterFilter)
            {
                Caster caster = _casters.Get(entity);
                if (caster.Damage <= 0)
                {
                    continue;
                }

                if (caster.CooldownTicks > 0 || caster.MpCost > availableMp)
                {
                    continue;
                }

                if (caster.PeriodTicks < bestPeriod)
                {
                    bestPeriod = caster.PeriodTicks;
                    best = entity;
                }
            }

            return best;
        }

        private void FireProjectiles(
            BattleContext context, ref Caster caster, float x, float y, int target)
        {
            ref Position targetPosition = ref _positions.Get(target);
            float dx = targetPosition.X - x;
            float dy = targetPosition.Y - y;
            float baseAngle = (float)Math.Atan2(dy, dx);

            float fan = BattleArena.MultiShotFanDegrees * BattleArena.DegreesToRadians;
            int count = caster.ProjectileCount;
            int lifetime = ProjectileLifetime(caster);

            for (int i = 0; i < count; i++)
            {
                float offset = (i - ((count - 1) * 0.5f)) * fan;
                float angle = baseAngle + offset;
                float directionX = (float)Math.Cos(angle);
                float directionY = (float)Math.Sin(angle);

                int entity = context.Spawner.SpawnProjectile(
                    caster.DefId,
                    caster.Damage,
                    caster.Element,
                    caster.PierceCount + 1,
                    caster.ProjectileSpeed,
                    x + (directionX * BattleArena.PlayerRadius),
                    y + (directionY * BattleArena.PlayerRadius),
                    directionX,
                    directionY,
                    lifetime);

                context.ProjectilesAlive++;
                context.Emit(
                    BattleEventType.ProjectileFired,
                    context.Ref(entity),
                    caster.DefId,
                    0,
                    BattleEventFlags.None,
                    x,
                    y);
            }
        }

        /// <summary>
        /// An aura or a melee sweep resolves on the caster's own tick: every ghost within
        /// reach takes the hit, and no projectile is created.
        /// </summary>
        private void StrikeEveryGhostInReach(BattleContext context, ref Caster caster, float x, float y)
        {
            int found = context.Hash.QueryRadius(x, y, caster.Range, _scratch);
            for (int i = 0; i < found; i++)
            {
                int target = _scratch[i];
                if (!context.IsAlive(target))
                {
                    continue;
                }

                context.AddHit(target, caster.Damage, caster.Element, caster.DefId);
            }
        }

        /// <summary>
        /// How long a projectile lives, in ticks: its range divided by its speed, which is
        /// the only reading of <c>range</c> that makes it a reach rather than a radius.
        /// Both numbers are data.
        /// </summary>
        private static int ProjectileLifetime(in Caster caster)
        {
            if (!(caster.ProjectileSpeed > 0f) || !(caster.Range > 0f))
            {
                return 1;
            }

            int ticks = (int)Math.Round(caster.Range / caster.ProjectileSpeed * BattleConstants.TickRate);
            return ticks < 1 ? 1 : ticks;
        }
    }
}
