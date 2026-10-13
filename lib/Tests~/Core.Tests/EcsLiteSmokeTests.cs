using Leopotam.EcsLite;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The battle simulation (Stage 07) builds on the vendored EcsLite. This
    /// proves the vendored sources really compile and run outside Unity, which is
    /// what makes the headless test harness worth having.
    /// </summary>
    public sealed class EcsLiteSmokeTests
    {
        private struct Position
        {
            public int X;
            public int Y;
        }

        [Fact]
        public void Vendored_ecslite_creates_a_world_pool_and_filter()
        {
            var world = new EcsWorld();
            try
            {
                EcsPool<Position> positions = world.GetPool<Position>();

                int first = world.NewEntity();
                int second = world.NewEntity();

                ref Position firstPosition = ref positions.Add(first);
                firstPosition.X = 3;
                firstPosition.Y = 4;
                positions.Add(second);

                EcsFilter filter = world.Filter<Position>().End();
                Assert.Equal(2, filter.GetEntitiesCount());

                int visited = 0;
                foreach (int entity in filter)
                {
                    visited++;
                    ref Position position = ref positions.Get(entity);
                    if (entity == first)
                    {
                        Assert.Equal(3, position.X);
                        Assert.Equal(4, position.Y);
                    }
                }

                Assert.Equal(2, visited);

                // Removing the last component destroys the entity: the filter shrinks
                // immediately and only the survivor is visited. Liveness is checked
                // through the filter, not through world.GetUsedEntitiesCount(), which
                // counts allocated entity slots including recycled ones (worlds.cs).
                positions.Del(second);
                Assert.Equal(1, filter.GetEntitiesCount());
                foreach (int entity in filter)
                {
                    Assert.Equal(first, entity);
                }
            }
            finally
            {
                world.Destroy();
            }
        }

        [Fact]
        public void Vendored_ecslite_runs_a_system_over_the_world()
        {
            var world = new EcsWorld();
            IEcsSystems systems = null!;
            try
            {
                systems = new EcsSystems(world);
                systems.Add(new MoveSystem());
                systems.Init();

                int entity = world.NewEntity();
                ref Position position = ref world.GetPool<Position>().Add(entity);
                position.X = 1;
                position.Y = 1;

                systems.Run();

                // Re-read through the pool: a ref taken before the world mutated
                // (Run() built a filter) must not be trusted.
                ref Position moved = ref world.GetPool<Position>().Get(entity);
                Assert.Equal(2, moved.X);
                Assert.Equal(2, moved.Y);
            }
            finally
            {
                systems?.Destroy();
                world.Destroy();
            }
        }

        private sealed class MoveSystem : IEcsInitSystem, IEcsRunSystem
        {
            private EcsPool<Position> _positions = null!;

            public void Init(IEcsSystems systems)
            {
                _positions = systems.GetWorld().GetPool<Position>();
            }

            public void Run(IEcsSystems systems)
            {
                foreach (int entity in systems.GetWorld().Filter<Position>().End())
                {
                    ref Position position = ref _positions.Get(entity);
                    position.X++;
                    position.Y++;
                }
            }
        }
    }
}
