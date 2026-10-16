using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Domain.Combat.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The battle contract, run against <see cref="FakeBattleSession"/>. When the
    /// EcsLite session lands (Stage 07) it gets its own derived class from the same
    /// base, and both must pass.
    /// </summary>
    public sealed class FakeBattleSessionContractTests : BattleSessionContractTests<FakeBattleSessionFactory>
    {
        protected override FakeBattleSessionFactory CreateFactory() =>
            new FakeBattleSessionFactory(
                ConfigFixture.Load(),
                new FakeBattleOptions { DummyEntityCount = 16, EntityLifetimeTicks = 8 });

        protected override FakeBattleSessionFactory CreateFactoryWithPressure() =>
            new FakeBattleSessionFactory(
                ConfigFixture.Load(),
                new FakeBattleOptions { DummyEntityCount = 4, EntityLifetimeTicks = 32, DamagePerSecond = 200 });

        [Fact]
        public void A_huge_entity_count_is_capped_at_the_render_limit()
        {
            var factory = new FakeBattleSessionFactory(
                ConfigFixture.Load(),
                new FakeBattleOptions { DummyEntityCount = 100_000, EntityLifetimeTicks = 8 });

            IBattleSession session = factory.Create(BattleTestFixtures.Setup(false));
            session.Step(PlayerInputFrame.Idle);

            Assert.Equal(BattleConstants.MaxRenderEntities, session.Frame.Entities.Length);
        }

        /// <summary>
        /// The fake's state for a seed did not move when Stage 08b added cooldowns to the frame:
        /// the cycle is presentation state the fake manufactures, not gameplay, so it is
        /// deliberately not mixed into the hash (the fake has nothing to fight with).
        /// </summary>
        [Fact]
        public void The_state_hash_of_a_seeded_floor_with_casters_is_unchanged()
        {
            IBattleSession session = CreateFactory().Create(CreateCasterSetup());
            for (int i = 0; i < 300; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(2633275827u, session.ComputeStateHash());
        }

        [Fact]
        public void Dev_kill_all_defeats_the_boss()
        {
            var factory = new FakeBattleSessionFactory(ConfigFixture.Load(), new FakeBattleOptions());
            IBattleSession session = factory.Create(BattleTestFixtures.Setup(true));

            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevKillAll()));

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Won, result.Outcome);
            Assert.Equal(BattleEndReason.BossDefeated, result.EndReason);
        }
    }
}
