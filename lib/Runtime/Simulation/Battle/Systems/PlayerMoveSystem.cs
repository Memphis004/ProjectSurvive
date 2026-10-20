using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Applies one tick of the player's movement input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The input arrives as integer axis values already normalised at the boundary
    /// (<see cref="PlayerInputFrame.FromAxis"/>), so a diagonal is not faster and no
    /// float from the caller reaches the simulation. Movement is
    /// <c>speed × input × tickLength</c>, and the speed comes from the loadout the bag
    /// resolver produced — including the weight penalty — never from a constant here.
    /// </para>
    /// <para>
    /// The player is kept inside the arena, which is what stops "run away forever" from
    /// being a strategy; the bound is structural (<see cref="BattleArena"/>), not balance.
    /// </para>
    /// <para>
    /// <b>Stage 11c:</b> a boss's possession reverses both axes for as long as the player's
    /// <see cref="Possessed"/> clock has ticks. The reversal lives here — in the rule that
    /// turns input into velocity — and nowhere else: the view is told the same tick is flipped
    /// through the HUD, and no presentation code touches the input it passes down.
    /// </para>
    /// </remarks>
    public sealed class PlayerMoveSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Velocity> _velocities = null!;
        private EcsPool<Possessed> _possessed = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _velocities = world.GetPool<Velocity>();
            _possessed = world.GetPool<Possessed>();
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

            PlayerInputFrame input = context.Input;

            float axisX = input.AxisX;
            float axisY = input.AxisY;

            if (_possessed.Has(player) && BossRules.IsFlipped(in _possessed.Get(player)))
            {
                // "สิงจนปุ่มเดินสลับทิศ": both axes are negated, so up walks down and left
                // walks right. Negating both (rather than one) is the mirror the GDD asks for.
                axisX = -axisX;
                axisY = -axisY;
            }

            ref Position position = ref _positions.Get(player);
            ref Velocity velocity = ref _velocities.Get(player);

            velocity.X = axisX * context.MoveSpeed;
            velocity.Y = axisY * context.MoveSpeed;

            position.X += velocity.X * BattleArena.TickDeltaSeconds;
            position.Y += velocity.Y * BattleArena.TickDeltaSeconds;

            BattleContext.ClampToArena(ref position.X, ref position.Y, BattleArena.PlayerRadius);
        }
    }
}
