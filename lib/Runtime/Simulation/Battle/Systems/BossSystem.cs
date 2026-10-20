using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>The stages a boss's possession passes through (Stage 11c).</summary>
    public enum BossPossessionState
    {
        /// <summary>Not possessing: walking at the player and waiting out the cooldown.</summary>
        Idle = 0,

        /// <summary>Standing still while the warning is on the floor.</summary>
        Warn = 1,

        /// <summary>The player's movement input is reversed for the row's duration.</summary>
        Active = 2,
    }

    /// <summary>
    /// Runs the boss: its two phases, the corpses it eats and the possession that reverses
    /// the player's controls (Stage 11c).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The phase is a threshold, not a timer.</b> Phase 2 begins on the tick the boss's HP is
    /// at or below the row's <c>boss_phase2_hp_percent</c> of its max — resolved to absolute HP
    /// once, at spawn (<see cref="BossRules"/>) — and it never goes back, so a heal cannot
    /// re-open phase 1. The crossing is announced with
    /// <see cref="BattleEventType.BossPhaseChanged"/> and its <c>Value</c> is the new phase.
    /// </para>
    /// <para>
    /// <b>Possession is warned, then applies.</b> In phase 2, when the player is inside the row's
    /// reach and the cooldown has run out, the boss stops and publishes a
    /// <see cref="BattleEventType.TelegraphStarted"/> circle around itself — the same channel and
    /// the same payload a ghost's strong attack uses (Stage 11b), because "something is about to
    /// hit you here" is one fact with one shape. A player who leaves the circle before the warning
    /// ends is not possessed, which is what makes the warning worth drawing.
    /// </para>
    /// <para>
    /// <b>The reversal is simulation state, not a view trick.</b> The tick that lands a possession
    /// writes <see cref="Possessed.RemainingTicks"/> on the player and this system runs the clock
    /// down; <see cref="PlayerMoveSystem"/> negates the axis while it lasts. The view learns about
    /// it only through <c>HudState.ControlFlipTicks</c>, which <see cref="FramePublishSystem"/>
    /// copies from that same component, so a HUD and a stick can never disagree about whether the
    /// controls are reversed (ADR 0030).
    /// </para>
    /// <para>
    /// <b>It runs before the player moves.</b> The system sits before <see cref="PlayerMoveSystem"/>
    /// in the run order, so a possession that lands on tick T is felt by the movement on tick T
    /// rather than one tick later, and the tick that ends a reversal is already unflipped.
    /// </para>
    /// </remarks>
    public sealed class BossSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<Collider> _colliders = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsPool<BossMind> _bosses = null!;
        private EcsPool<Possessed> _possessed = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _healths = world.GetPool<Health>();
            _colliders = world.GetPool<Collider>();
            _enemies = world.GetPool<EnemyTag>();
            _bosses = world.GetPool<BossMind>();
            _possessed = world.GetPool<Possessed>();
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;
            int boss = context.BossEntity;

            if (!context.IsAlive(boss) || !_bosses.Has(boss) || !_positions.Has(boss))
            {
                return;
            }

            ref BossMind mind = ref _bosses.Get(boss);

            AdvancePhase(context, boss, ref mind);
            AdvancePossession(context, boss, ref mind);
        }

        /// <summary>
        /// Moves the boss to phase 2 the tick its HP reaches the row's threshold, once.
        /// </summary>
        private void AdvancePhase(BattleContext context, int boss, ref BossMind mind)
        {
            if (mind.Phase >= 2 || mind.Phase2HpThreshold <= 0)
            {
                return;
            }

            Health health = _healths.Get(boss);
            if (health.Hp <= 0 || health.Hp > mind.Phase2HpThreshold)
            {
                return;
            }

            mind.Phase = 2;

            ref Position position = ref _positions.Get(boss);
            context.Emit(
                BattleEventType.BossPhaseChanged,
                context.Ref(boss),
                _enemies.Get(boss).DefId,
                mind.Phase,
                BattleEventFlags.Boss,
                position.X,
                position.Y);
        }

        /// <summary>
        /// Runs the possession clock: warn, apply, wait, repeat.
        /// </summary>
        private void AdvancePossession(BattleContext context, int boss, ref BossMind mind)
        {
            if (mind.PossessTicks <= 0 || mind.PossessRange <= 0f)
            {
                return;
            }

            int player = context.PlayerEntity;
            if (!context.IsAlive(player))
            {
                return;
            }

            ref Possessed possessed = ref _possessed.Get(player);

            // The reversal belongs to the player and outlives a stage change, so it is counted
            // down whatever the boss is doing; killing the boss is what ends it early
            // (DeathSystem), not the FSM reaching a different stage.
            if (possessed.RemainingTicks > 0)
            {
                possessed.RemainingTicks--;
            }

            switch ((BossPossessionState)mind.State)
            {
                case BossPossessionState.Warn:
                    StepWarn(context, boss, ref mind, ref possessed);
                    break;

                case BossPossessionState.Active:
                    StepActive(ref mind, ref possessed);
                    break;

                default:
                    StepIdle(context, boss, ref mind);
                    break;
            }
        }

        /// <summary>Phase 2 only: waits out the cooldown, then opens a warning inside reach.</summary>
        private void StepIdle(BattleContext context, int boss, ref BossMind mind)
        {
            if (mind.Phase < 2 || mind.PossessTelegraphTicks <= 0)
            {
                return;
            }

            if (mind.CooldownTicks > 0)
            {
                mind.CooldownTicks--;
                return;
            }

            ref Position bossPosition = ref _positions.Get(boss);
            ref Position playerPosition = ref _positions.Get(context.PlayerEntity);

            if (!BossRules.WithinReach(
                    bossPosition.X, bossPosition.Y, mind.PossessRange,
                    playerPosition.X, playerPosition.Y, _colliders.Get(context.PlayerEntity).Radius))
            {
                return;
            }

            mind.State = (int)BossPossessionState.Warn;
            mind.StateTicks = mind.PossessTelegraphTicks;
            EmitTelegraph(context, boss, in mind, started: true);
        }

        /// <summary>
        /// Counts the warning down and lands it: a player still inside the circle is possessed,
        /// one who stepped out is not.
        /// </summary>
        private void StepWarn(
            BattleContext context, int boss, ref BossMind mind, ref Possessed possessed)
        {
            mind.StateTicks--;

            if (mind.StateTicks > 0)
            {
                return;
            }

            EmitTelegraph(context, boss, in mind, started: false);

            ref Position bossPosition = ref _positions.Get(boss);
            int player = context.PlayerEntity;
            ref Position playerPosition = ref _positions.Get(player);

            bool caught = BossRules.WithinReach(
                bossPosition.X, bossPosition.Y, mind.PossessRange,
                playerPosition.X, playerPosition.Y, _colliders.Get(player).Radius);

            if (caught)
            {
                possessed.RemainingTicks = mind.PossessTicks;
                mind.State = (int)BossPossessionState.Active;
                mind.StateTicks = 0;
                return;
            }

            // Warned, missed: the boss walks on and waits a full cooldown before trying again.
            mind.State = (int)BossPossessionState.Idle;
            mind.StateTicks = 0;
            mind.CooldownTicks = mind.PossessIntervalTicks;
        }

        /// <summary>Holds the boss in the reversal until the player's own clock runs out.</summary>
        private void StepActive(ref BossMind mind, ref Possessed possessed)
        {
            if (possessed.RemainingTicks > 0)
            {
                return;
            }

            mind.State = (int)BossPossessionState.Idle;
            mind.StateTicks = 0;
            mind.CooldownTicks = mind.PossessIntervalTicks;
        }

        private void EmitTelegraph(
            BattleContext context, int boss, in BossMind mind, bool started)
        {
            ref Position position = ref _positions.Get(boss);

            context.Emit(
                started ? BattleEventType.TelegraphStarted : BattleEventType.TelegraphEnded,
                context.Ref(boss),
                _enemies.Get(boss).DefId,
                (int)TelegraphShape.Circle,
                BattleEventFlags.Boss,
                position.X,
                position.Y,
                mind.PossessRange,
                0f,
                0f);
        }
    }
}
