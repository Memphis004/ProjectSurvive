using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Simulation.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The battle contract of Stage 02b, run against the real EcsLite simulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is the whole point of the abstract suite: the fake was proven against
    /// these rules from Stage 02b, and the real battle is held to exactly the same ones.
    /// Nothing here is skipped, relaxed or re-implemented — the derived hooks only supply
    /// what a factory needs.
    /// </para>
    /// <para>
    /// <b>Why a contract factory differs from the shipped one.</b> The suite drives the
    /// player with <see cref="PlayerInputFrame.Idle"/> and never moves, so an idle player
    /// has to survive a floor to its timer for the outcome rules to be tested at all. The
    /// real floors are tuned for a kiting player: a stationary body takes contact damage
    /// from every ghost that reaches it and dies in seconds. The suite also expects a
    /// battle that generates more events than the ring can hold, which a single-boss floor
    /// with nothing to kill never does. Both premises are supplied through the load
    /// generator knobs (<see cref="EcsBattleOptions"/>), which exist for exactly this and
    /// change no balance number; the loadout below hands in the HP a non-moving player
    /// needs. These are not balance claims — balance is measured by the bot simulation
    /// (<c>tools/sim.ps1</c>) and checked in <see cref="EcsBattleRulesTests"/>.
    /// </para>
    /// </remarks>
    public sealed class EcsBattleSessionContractTests : BattleSessionContractTests<EcsBattleSessionFactory>
    {
        /// <summary>
        /// HP for the contract loadout. Large enough that the tick-long contact damage of a
        /// stationary player cannot end the floor early, so the timer is what decides.
        /// </summary>
        private const int ContractMaxHp = 100_000;

        /// <summary>The load the contract battle carries, and how fast it churns.</summary>
        private const int ContractLoadEnemies = 32;

        /// <summary>Ticks between retirement batches; a non-zero value makes the battle churn.</summary>
        private const int ContractLifetimeTicks = 8;

        protected override EcsBattleSessionFactory CreateFactory() =>
            new EcsBattleSessionFactory(ConfigFixture.Load(), ContractOptions());

        protected override EcsBattleSessionFactory CreateFactoryWithPressure() =>
            new EcsBattleSessionFactory(ConfigFixture.Load(), ContractOptions());

        protected override BattleSetup CreateSetup(
            bool bossFloor,
            int currentHp = BattleTestFixtures.DefaultMaxHp,
            bool devCheatsAllowed = true)
        {
            BattleSetup setup = BattleTestFixtures.Setup(bossFloor, currentHp, devCheatsAllowed: devCheatsAllowed);
            setup.CurrentHp = ContractMaxHp;
            setup.Loadout.Stats.MaxHp = ContractMaxHp;

            // No weapons on purpose: the suite's boss floor must still be running when its
            // timer runs out ("a boss floor that runs out of time is lost with timeout"),
            // and a real weapon kills the 320 HP boss long before then.
            return setup;
        }

        private static EcsBattleOptions ContractOptions() =>
            new EcsBattleOptions
            {
                MaintainLoadTestEntities = true,
                LoadTestEnemies = ContractLoadEnemies,
                LoadTestEntityLifetimeTicks = ContractLifetimeTicks,
            };

        /// <summary>
        /// The real session is disposable and its world is released; the contract has no
        /// lifetime rule because the fake owns nothing, so it is asserted here.
        /// </summary>
        [Fact]
        public void Dispose_releases_the_world_and_is_idempotent()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));
            for (int i = 0; i < 10; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            var disposable = Assert.IsType<EcsBattleSimulation>(session);
            disposable.Dispose();
            disposable.Dispose();

            // A disposed session is inert rather than crashing: it stops, and reports nothing.
            long tick = session.Tick;
            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(tick, session.Tick);
        }

        /// <summary>The same seed and the same inputs produce the same state — in one runtime.</summary>
        [Fact]
        public void Seeded_replay_is_reproducible_in_one_runtime()
        {
            BattleSetup setup = CreateSetup(false);

            uint first = HashAfter300Ticks(CreateFactory(), setup);
            uint second = HashAfter300Ticks(CreateFactory(), setup);
            Assert.Equal(first, second);

            BattleSetup other = CreateSetup(false);
            other.Seed = setup.Seed + 1u;
            Assert.NotEqual(first, HashAfter300Ticks(CreateFactory(), other));
        }

        private static uint HashAfter300Ticks(EcsBattleSessionFactory factory, BattleSetup setup)
        {
            IBattleSession session = factory.Create(setup);
            for (int i = 0; i < 300; i++)
            {
                session.Step(PlayerInputFrame.FromAxis(i % 2 == 0 ? 1f : -1f, 0.5f));
            }

            return session.ComputeStateHash();
        }

        /// <summary>
        /// The state a seed produces did not move when Stage 08b added cooldowns to the frame.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cooldown frame is presentation state built from components that already existed, so
        /// it must not be able to change a fight. The pin is a literal recorded before the change
        /// and asserted after it: a recording of the same seed and the same 300 inputs, with a real
        /// weapon and familiar casting (kills, drops and all), so the hash covers the code the
        /// change touched. Bumping a cooldown here while editing the publish path is exactly the
        /// failure this catches.
        /// </para>
        /// <para>
        /// The number is a fingerprint of one runtime build, not a save format and not a claim that
        /// two machines agree (AGENTS rule 6): it is only ever compared against itself.
        /// </para>
        /// </remarks>
        [Fact]
        public void The_state_hash_of_a_seeded_floor_with_casters_is_unchanged()
        {
            uint hash = HashAfter300Ticks(CreateFactory(), CreateCasterSetup());

            Assert.Equal(3218847520u, hash);
        }
    }
}
