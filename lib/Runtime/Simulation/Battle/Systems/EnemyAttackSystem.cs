using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>The stages a telegraphed ghost attack passes through (Stage 11b).</summary>
    /// <remarks>
    /// Every ghost with an attack runs the same stages and skips the ones its row leaves at
    /// zero, which is what lets one small machine describe two very different ghosts: Krasue
    /// fills the strike and follow-up windows with a dash and a drain, Nang Tani leaves them
    /// empty and hits once with a ring and once with vines.
    /// </remarks>
    public enum EnemyAttackState
    {
        /// <summary>Walking at the player, waiting for the cycle to open.</summary>
        Chase = 0,

        /// <summary>Holding still while the warning is on the floor.</summary>
        Telegraph = 1,

        /// <summary>The strike landing; a dash travels its line during this window.</summary>
        Strike = 2,

        /// <summary>Holding still while the second warning is on the floor.</summary>
        FollowUpTelegraph = 3,

        /// <summary>The second attack's window: a drain aura, or a single vine lash.</summary>
        FollowUp = 4,

        /// <summary>Backing away before the next cycle.</summary>
        Retreat = 5,

        /// <summary>Standing still until the cooldown elapses.</summary>
        Recover = 6,
    }

    /// <summary>
    /// Runs each ghost's telegraphed attack: one small per-kind state machine, a warning
    /// event before every strong hit, and nothing but component writes on the hot path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The contract the player reads.</b> A strong attack never lands without a
    /// <see cref="BattleEventType.TelegraphStarted"/> earlier in the cycle, and the event
    /// carries the footprint the damage actually uses: <c>Value</c> is the
    /// <see cref="TelegraphShape"/>, <c>X</c>/<c>Y</c> the origin, <c>Radius</c> the radius
    /// or half-width, <c>Length</c> the line's length and <c>Angle</c> its direction. The
    /// matching <see cref="BattleEventType.TelegraphEnded"/> is emitted in the tick the
    /// stage ends, so a decal never outlives the hazard it warns about.
    /// </para>
    /// <para>
    /// <b>Why the stages are shared.</b> Krasue and Nang Tani differ in <i>what</i> their
    /// windows do — a dash and a drain versus a ring and a lash — not in the order of
    /// "warn, then hit". A second machine would be a second place to get the warning
    /// wrong, and the Core tests assert the order for both ghosts.
    /// </para>
    /// <para>
    /// <b>Steering cooperates, it does not compete.</b> <see cref="EnemySteerSystem"/> moves
    /// a ghost only while it is <see cref="EnemyAttackState.Chase"/>; every other stage
    /// writes the ghost's position itself, so a charging Krasue and a standing Nang Tani
    /// cannot be dragged at walking speed through their own telegraph.
    /// </para>
    /// </remarks>
    public sealed class EnemyAttackSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Velocity> _velocities = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<Collider> _colliders = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsPool<EnemyAttack> _attacks = null!;
        private EcsPool<EnemyBrain> _brains = null!;
        private EcsFilter _filter = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _velocities = world.GetPool<Velocity>();
            _healths = world.GetPool<Health>();
            _colliders = world.GetPool<Collider>();
            _enemies = world.GetPool<EnemyTag>();
            _attacks = world.GetPool<EnemyAttack>();
            _brains = world.GetPool<EnemyBrain>();
            _filter = world
                .Filter<EnemyAttack>()
                .Inc<EnemyBrain>()
                .Inc<Position>()
                .Inc<Health>()
                .End(BattleArena.FilterDenseCapacity);
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
            float playerRadius = _colliders.Get(player).Radius;

            foreach (int entity in _filter)
            {
                if (_healths.Get(entity).Hp <= 0)
                {
                    continue;
                }

                ref Position position = ref _positions.Get(entity);
                ref EnemyAttack attack = ref _attacks.Get(entity);
                ref EnemyBrain brain = ref _brains.Get(entity);

                switch ((EnemyAttackState)brain.State)
                {
                    case EnemyAttackState.Chase:
                        StepChase(context, entity, ref brain, in attack, in position, playerX, playerY);
                        break;

                    case EnemyAttackState.Telegraph:
                        StepTelegraph(
                            context, entity, ref brain, ref attack, in position,
                            playerX, playerY, playerRadius);
                        break;

                    case EnemyAttackState.Strike:
                        StepStrike(context, entity, ref brain, in attack, ref position, ref _velocities.Get(entity));
                        break;

                    case EnemyAttackState.FollowUpTelegraph:
                        StepFollowUpTelegraph(
                            context, entity, ref brain, ref attack, in position,
                            playerX, playerY, playerRadius, ref playerPosition);
                        break;

                    case EnemyAttackState.FollowUp:
                        StepFollowUp(
                            context, entity, ref brain, in attack, in position,
                            playerX, playerY, playerRadius, ref playerPosition);
                        break;

                    case EnemyAttackState.Retreat:
                        StepRetreat(
                            entity, ref brain, in attack, ref position, ref _velocities.Get(entity),
                            playerX, playerY);
                        break;

                    default:
                        StepRecover(ref brain, in attack);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------
        // stages
        // ------------------------------------------------------------------

        private void StepChase(
            BattleContext context, int entity, ref EnemyBrain brain, in EnemyAttack attack,
            in Position position, float playerX, float playerY)
        {
            float dx = playerX - position.X;
            float dy = playerY - position.Y;

            if (((dx * dx) + (dy * dy)) > attack.Range * attack.Range)
            {
                return;
            }

            LockAim(ref brain, dx, dy);

            if (attack.TelegraphTicks <= 0)
            {
                // A row with no warning cannot telegraph; the validator rejects it, and a ghost
                // that somehow carries one hits at once rather than standing still for ever.
                ResolvePrimary(
                    context, in attack, in brain, in position, playerX, playerY,
                    _colliders.Get(context.PlayerEntity).Radius);
                EnterAfterStrike(context, entity, ref brain, in attack, in position);
                return;
            }

            brain.State = (int)EnemyAttackState.Telegraph;
            brain.StateTicks = attack.TelegraphTicks;
            EmitTelegraph(context, entity, in attack, in brain, in position, followUp: false, started: true);
        }

        private void StepTelegraph(
            BattleContext context, int entity, ref EnemyBrain brain, ref EnemyAttack attack,
            in Position position, float playerX, float playerY, float playerRadius)
        {
            brain.StateTicks--;

            if (brain.StateTicks > 0)
            {
                return;
            }

            EmitTelegraph(context, entity, in attack, in brain, in position, followUp: false, started: false);
            ResolvePrimary(context, in attack, in brain, in position, playerX, playerY, playerRadius);

            brain.State = (int)EnemyAttackState.Strike;
            brain.StateTicks = attack.StrikeTicks;

            if (attack.StrikeTicks <= 0)
            {
                EnterAfterStrike(context, entity, ref brain, in attack, in position);
            }
        }

        /// <summary>
        /// The strike window. A dash travels its line during it; a ring has nothing left to
        /// do, because it already landed in the tick its warning ended.
        /// </summary>
        private void StepStrike(
            BattleContext context, int entity, ref EnemyBrain brain, in EnemyAttack attack,
            ref Position position, ref Velocity velocity)
        {
            brain.StateTicks--;

            if (attack.Length > 0f && attack.StrikeTicks > 0)
            {
                float speed = attack.Length / (attack.StrikeTicks * BattleArena.TickDeltaSeconds);
                velocity.X = brain.AimX * speed;
                velocity.Y = brain.AimY * speed;

                position.X += velocity.X * BattleArena.TickDeltaSeconds;
                position.Y += velocity.Y * BattleArena.TickDeltaSeconds;
                BattleContext.ClampToArena(ref position.X, ref position.Y, _colliders.Get(entity).Radius);
            }

            if (brain.StateTicks > 0)
            {
                return;
            }

            velocity.X = 0f;
            velocity.Y = 0f;
            EnterAfterStrike(context, entity, ref brain, in attack, in position);
        }

        private void StepFollowUpTelegraph(
            BattleContext context, int entity, ref EnemyBrain brain, ref EnemyAttack attack,
            in Position position, float playerX, float playerY, float playerRadius,
            ref Position playerPosition)
        {
            brain.StateTicks--;

            if (brain.StateTicks > 0)
            {
                return;
            }

            EmitTelegraph(context, entity, in attack, in brain, in position, followUp: true, started: false);

            if (attack.FollowUpTicks > 0)
            {
                brain.State = (int)EnemyAttackState.FollowUp;
                brain.StateTicks = attack.FollowUpTicks;
                brain.DamageTicks = 1;
                return;
            }

            ResolveFollowUp(
                context, in attack, in brain, in position, playerX, playerY, playerRadius, ref playerPosition);
            EnterRecover(ref brain, in attack);
        }

        private void StepFollowUp(
            BattleContext context, int entity, ref EnemyBrain brain, in EnemyAttack attack,
            in Position position, float playerX, float playerY, float playerRadius,
            ref Position playerPosition)
        {
            brain.StateTicks--;
            brain.DamageTicks--;

            if (brain.DamageTicks <= 0)
            {
                ResolveFollowUp(
                    context, in attack, in brain, in position, playerX, playerY, playerRadius,
                    ref playerPosition);
                brain.DamageTicks = attack.FollowUpTickTicks > 0 ? attack.FollowUpTickTicks : 1;
            }

            if (brain.StateTicks > 0)
            {
                return;
            }

            if (attack.RetreatTicks > 0)
            {
                brain.State = (int)EnemyAttackState.Retreat;
                brain.StateTicks = attack.RetreatTicks;
                return;
            }

            EnterRecover(ref brain, in attack);
        }

        private void StepRetreat(
            int entity, ref EnemyBrain brain, in EnemyAttack attack, ref Position position,
            ref Velocity velocity, float playerX, float playerY)
        {
            brain.StateTicks--;

            float dx = position.X - playerX;
            float dy = position.Y - playerY;
            float squared = (dx * dx) + (dy * dy);

            if (squared > 1e-8f)
            {
                float inverse = 1f / (float)Math.Sqrt(squared);
                velocity.X = dx * inverse * attack.RetreatSpeed;
                velocity.Y = dy * inverse * attack.RetreatSpeed;
            }
            else
            {
                velocity.X = 0f;
                velocity.Y = 0f;
            }

            position.X += velocity.X * BattleArena.TickDeltaSeconds;
            position.Y += velocity.Y * BattleArena.TickDeltaSeconds;
            BattleContext.ClampToArena(ref position.X, ref position.Y, _colliders.Get(entity).Radius);

            if (brain.StateTicks > 0)
            {
                return;
            }

            velocity.X = 0f;
            velocity.Y = 0f;
            EnterRecover(ref brain, in attack);
        }

        private static void StepRecover(ref EnemyBrain brain, in EnemyAttack attack)
        {
            brain.StateTicks--;

            if (brain.StateTicks > 0)
            {
                return;
            }

            brain.State = (int)EnemyAttackState.Chase;
            brain.StateTicks = 0;
            brain.DamageTicks = 0;
        }

        // ------------------------------------------------------------------
        // transitions and damage
        // ------------------------------------------------------------------

        private void EnterAfterStrike(
            BattleContext context, int entity, ref EnemyBrain brain, in EnemyAttack attack,
            in Position position)
        {
            if (attack.FollowUpTelegraphTicks > 0)
            {
                brain.State = (int)EnemyAttackState.FollowUpTelegraph;
                brain.StateTicks = attack.FollowUpTelegraphTicks;
                EmitTelegraph(context, entity, in attack, in brain, in position, followUp: true, started: true);
                return;
            }

            EnterRecover(ref brain, in attack);
        }

        private static void EnterRecover(ref EnemyBrain brain, in EnemyAttack attack)
        {
            brain.State = (int)EnemyAttackState.Recover;
            brain.StateTicks = attack.CooldownTicks > 0 ? attack.CooldownTicks : 1;
            brain.DamageTicks = 0;
        }

        /// <summary>
        /// Applies the attack's own footprint: a line for a dash, a circle for a ring. The
        /// footprint is the same one the warning drew, so a player who stepped out of it is
        /// not hit.
        /// </summary>
        private static void ResolvePrimary(
            BattleContext context, in EnemyAttack attack, in EnemyBrain brain,
            in Position position, float playerX, float playerY, float playerRadius)
        {
            bool hit = attack.Length > 0f
                ? EnemyAttackRules.LineHits(
                    position.X, position.Y, brain.AimX, brain.AimY,
                    attack.Length, attack.Radius, playerX, playerY, playerRadius)
                : EnemyAttackRules.CircleHits(
                    position.X, position.Y, attack.Radius, playerX, playerY, playerRadius);

            if (hit && attack.Damage > 0)
            {
                context.PlayerHitDamage += attack.Damage;
            }
        }

        /// <summary>
        /// Applies the second attack's footprint, and throws a player it hits when the row
        /// asks for it. A push moves the player and nothing else: no damage, no status, no
        /// random, so it stays a pure function of the tick's positions.
        /// </summary>
        /// <remarks>
        /// The hit test uses the player's position from the start of the tick, like every
        /// other resolve in this system; a push therefore moves the player for the next
        /// tick's tests rather than for another ghost's warning in the same one.
        /// </remarks>
        private static void ResolveFollowUp(
            BattleContext context, in EnemyAttack attack, in EnemyBrain brain,
            in Position position, float playerX, float playerY, float playerRadius,
            ref Position playerPosition)
        {
            bool hit = attack.FollowUpLength > 0f
                ? EnemyAttackRules.LineHits(
                    position.X, position.Y, brain.AimX, brain.AimY,
                    attack.FollowUpLength, attack.FollowUpRadius, playerX, playerY, playerRadius)
                : EnemyAttackRules.CircleHits(
                    position.X, position.Y, attack.FollowUpRadius, playerX, playerY, playerRadius);

            if (!hit)
            {
                return;
            }

            if (attack.FollowUpDamage > 0)
            {
                context.PlayerHitDamage += attack.FollowUpDamage;
            }

            if (attack.FollowUpPush > 0f)
            {
                PushAway(in attack, in brain, in position, ref playerPosition, playerRadius);
            }
        }

        /// <summary>
        /// Moves the player <see cref="EnemyAttack.FollowUpPush"/> units away from the ghost
        /// that hit them, so a gale at the landing point throws them along the dive rather
        /// than back down it. A player standing exactly on the ghost is thrown along the
        /// ghost's locked aim, because there is no direction in a zero vector.
        /// </summary>
        private static void PushAway(
            in EnemyAttack attack, in EnemyBrain brain, in Position position,
            ref Position playerPosition, float playerRadius)
        {
            float dx = playerPosition.X - position.X;
            float dy = playerPosition.Y - position.Y;
            float squared = (dx * dx) + (dy * dy);

            float directionX;
            float directionY;

            if (squared > 1e-8f)
            {
                float inverse = 1f / (float)Math.Sqrt(squared);
                directionX = dx * inverse;
                directionY = dy * inverse;
            }
            else
            {
                directionX = brain.AimX;
                directionY = brain.AimY;
            }

            playerPosition.X += directionX * attack.FollowUpPush;
            playerPosition.Y += directionY * attack.FollowUpPush;

            BattleContext.ClampToArena(ref playerPosition.X, ref playerPosition.Y, playerRadius);
        }

        private static void LockAim(ref EnemyBrain brain, float dx, float dy)
        {
            float squared = (dx * dx) + (dy * dy);
            if (squared > 1e-8f)
            {
                float inverse = 1f / (float)Math.Sqrt(squared);
                brain.AimX = dx * inverse;
                brain.AimY = dy * inverse;
                return;
            }

            brain.AimX = 1f;
            brain.AimY = 0f;
        }

        /// <summary>
        /// Publishes the warning the floor decal draws. Every number the presentation needs
        /// travels with it, so the view never re-derives a footprint from a def it cannot see.
        /// </summary>
        private void EmitTelegraph(
            BattleContext context, int entity, in EnemyAttack attack, in EnemyBrain brain,
            in Position position, bool followUp, bool started)
        {
            float radius = followUp ? attack.FollowUpRadius : attack.Radius;
            float length = followUp ? attack.FollowUpLength : attack.Length;
            var shape = length > 0f ? TelegraphShape.Line : TelegraphShape.Circle;

            context.Emit(
                started ? BattleEventType.TelegraphStarted : BattleEventType.TelegraphEnded,
                context.Ref(entity),
                _enemies.Get(entity).DefId,
                (int)shape,
                BattleEventFlags.None,
                position.X,
                position.Y,
                radius,
                length,
                (float)Math.Atan2(brain.AimY, brain.AimX));
        }
    }
}
