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
    /// </remarks>
    public sealed class PlayerMoveSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Velocity> _velocities = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _velocities = world.GetPool<Velocity>();
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

            ref Position position = ref _positions.Get(player);
            ref Velocity velocity = ref _velocities.Get(player);

            velocity.X = input.AxisX * context.MoveSpeed;
            velocity.Y = input.AxisY * context.MoveSpeed;

            position.X += velocity.X * BattleArena.TickDeltaSeconds;
            position.Y += velocity.Y * BattleArena.TickDeltaSeconds;

            BattleContext.ClampToArena(ref position.X, ref position.Y, BattleArena.PlayerRadius);
        }
    }
}
