using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Publishes the tick's render snapshot and HUD through the one
    /// <see cref="BattleFrameBuffer"/> the fake also uses (ADR 0012).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs last, so the frame describes the tick that just finished — including what
    /// died, what dropped and whether the floor is over. Publishing is a swap of two
    /// preallocated slots, so the frame costs nothing to build and nothing to replace, and a
    /// reader between ticks is holding the slot the next tick is not writing.
    /// </para>
    /// <para>
    /// The frame is a <b>view</b>, never the source of truth: it stops at
    /// <c>MaxRenderEntities</c> and drops the rest rather than growing. Nothing in the
    /// simulation reads it back.
    /// </para>
    /// <para>
    /// Entity order is fixed — player, ghosts, projectiles, drops — so two runs of one seed
    /// publish byte-identical frames, which is what the contract suite's Prev/Cur case
    /// compares.
    /// </para>
    /// </remarks>
    public sealed class FramePublishSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Velocity> _velocities = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsPool<Mana> _manas = null!;
        private EcsFilter _enemyFilter = null!;
        private EcsFilter _projectileFilter = null!;
        private EcsFilter _pickupFilter = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _velocities = world.GetPool<Velocity>();
            _healths = world.GetPool<Health>();
            _enemies = world.GetPool<EnemyTag>();
            _manas = world.GetPool<Mana>();
            _enemyFilter = world.Filter<EnemyTag>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
            _projectileFilter = world.Filter<Projectile>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
            _pickupFilter = world.Filter<Pickup>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;
            BattleFrameBuffer frame = context.Frame;

            frame.BeginFrame(context.Tick);
            frame.SetHud(BuildHud(context));

            int player = context.PlayerEntity;
            if (context.IsAlive(player))
            {
                ref Position position = ref _positions.Get(player);
                frame.Add(new RenderEntity(
                    context.Ref(player), RenderEntityKind.Player, position.X, position.Y));
            }

            PublishEnemies(context, frame);
            PublishProjectiles(context, frame);
            PublishPickups(context, frame);

            frame.Publish();
        }

        private HudState BuildHud(BattleContext context)
        {
            var hud = new HudState
            {
                Currency = context.CurrencyEarned,
                Kills = context.Kills,
                ElapsedTicks = (int)context.Tick,
                FloorDurationTicks = context.FloorDurationTicks,
                WaveIndex = context.WaveIndex,
                BossHp = context.BossSpawned ? context.BossHp : 0,
                BossMaxHp = context.BossSpawned ? context.BossMaxHp : 0,
                HasBoss = context.Floor.IsBoss,
            };

            int player = context.PlayerEntity;
            if (context.IsAlive(player))
            {
                Health health = _healths.Get(player);
                hud.Hp = health.Hp;
                hud.MaxHp = health.MaxHp;

                if (_manas.Has(player))
                {
                    Mana mana = _manas.Get(player);
                    hud.Mp = mana.Mp;
                    hud.MaxMp = mana.MaxMp;
                }
            }

            return hud;
        }

        private void PublishEnemies(BattleContext context, BattleFrameBuffer frame)
        {
            foreach (int entity in _enemyFilter)
            {
                Health health = _healths.Get(entity);
                if (health.Hp <= 0)
                {
                    continue;
                }

                EnemyTag tag = _enemies.Get(entity);
                ref Position position = ref _positions.Get(entity);
                ref Velocity velocity = ref _velocities.Get(entity);

                float percent = health.MaxHp > 0 ? health.Hp / (float)health.MaxHp : 0f;

                frame.Add(new RenderEntity(
                    context.Ref(entity),
                    RenderEntityKind.Enemy,
                    position.X,
                    position.Y,
                    Heading(velocity.X, velocity.Y),
                    1f,
                    percent < 0f ? 0f : (percent > 1f ? 1f : percent),
                    EnemyFlags(tag, velocity.X)));
            }
        }

        private void PublishProjectiles(BattleContext context, BattleFrameBuffer frame)
        {
            foreach (int entity in _projectileFilter)
            {
                ref Position position = ref _positions.Get(entity);
                ref Velocity velocity = ref _velocities.Get(entity);

                frame.Add(new RenderEntity(
                    context.Ref(entity),
                    RenderEntityKind.Projectile,
                    position.X,
                    position.Y,
                    Heading(velocity.X, velocity.Y)));
            }
        }

        private void PublishPickups(BattleContext context, BattleFrameBuffer frame)
        {
            foreach (int entity in _pickupFilter)
            {
                ref Position position = ref _positions.Get(entity);
                frame.Add(new RenderEntity(
                    context.Ref(entity), RenderEntityKind.Pickup, position.X, position.Y));
            }
        }

        private static RenderEntityFlags EnemyFlags(in EnemyTag tag, float velocityX)
        {
            RenderEntityFlags flags = RenderEntityFlags.None;
            if (tag.IsBoss)
            {
                flags |= RenderEntityFlags.Boss;
            }

            if (tag.IsElite)
            {
                flags |= RenderEntityFlags.Elite;
            }

            if (velocityX < 0f)
            {
                flags |= RenderEntityFlags.FacingLeft;
            }

            return flags;
        }

        /// <summary>Yaw of a moving entity in degrees, or 0 when it stands still.</summary>
        private static float Heading(float velocityX, float velocityY)
        {
            if (velocityX == 0f && velocityY == 0f)
            {
                return 0f;
            }

            return (float)Math.Atan2(velocityY, velocityX) * BattleArena.RadiansToDegrees;
        }
    }
}
