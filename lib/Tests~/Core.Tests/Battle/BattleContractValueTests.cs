using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>The small value types of the battle contract.</summary>
    public sealed class BattleContractValueTests
    {
        [Fact]
        public void Tick_rate_agrees_with_the_fixed_ticker()
        {
            Assert.Equal((int)FixedTicker.DefaultTickHz, BattleConstants.TickRate);
            Assert.Equal(FixedTicker.DefaultMaxTicksPerAdvance, BattleConstants.MaxStepsPerFrame);
        }

        [Theory]
        [InlineData(0f, 0f, 0, 0)]
        [InlineData(1f, 0f, 1000, 0)]
        [InlineData(-1f, 0f, -1000, 0)]
        [InlineData(0.5f, 0f, 500, 0)]
        public void FromAxis_scales_a_unit_axis(float x, float y, int expectedX, int expectedY)
        {
            PlayerInputFrame frame = PlayerInputFrame.FromAxis(x, y);

            Assert.Equal(expectedX, frame.MoveX);
            Assert.Equal(expectedY, frame.MoveY);
        }

        [Fact]
        public void FromAxis_normalises_a_diagonal_so_it_is_not_faster()
        {
            PlayerInputFrame frame = PlayerInputFrame.FromAxis(3f, 4f);

            Assert.Equal(600, frame.MoveX);
            Assert.Equal(800, frame.MoveY);
            Assert.Equal(0.6f, frame.AxisX, 3);
            Assert.Equal(0.8f, frame.AxisY, 3);
        }

        [Fact]
        public void FromAxis_clamps_an_over_long_axis()
        {
            PlayerInputFrame frame = PlayerInputFrame.FromAxis(2f, 0f);

            Assert.Equal(BattleConstants.InputAxisScale, frame.MoveX);
            Assert.Equal(1f, frame.AxisX, 3);
        }

        [Fact]
        public void FromAxis_treats_a_non_finite_axis_as_idle()
        {
            Assert.True(PlayerInputFrame.FromAxis(float.NaN, 0f).IsIdle);
            Assert.True(PlayerInputFrame.FromAxis(0f, float.PositiveInfinity).IsIdle);
        }

        [Fact]
        public void Idle_frame_is_zero()
        {
            Assert.True(PlayerInputFrame.Idle.IsIdle);
            Assert.Equal(0, PlayerInputFrame.Idle.MoveX);
            Assert.Equal(0, PlayerInputFrame.Idle.MoveY);
        }

        [Fact]
        public void EntityRef_none_is_invalid_and_equality_is_by_slot_and_version()
        {
            Assert.False(EntityRef.None.IsValid);
            Assert.Equal(default, EntityRef.None);

            var a = new EntityRef(3, 1);
            var b = new EntityRef(3, 1);
            var c = new EntityRef(3, 2);

            Assert.True(a.IsValid);
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.True(a == b);
            Assert.True(a != c);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Dev_commands_are_flagged_and_surrender_is_not()
        {
            Assert.False(BattleCommand.Surrender().IsDevCommand);
            Assert.True(BattleCommand.DevWinNow().IsDevCommand);
            Assert.True(BattleCommand.DevGodMode(true).IsDevCommand);
            Assert.Equal(1, BattleCommand.DevGodMode(true).Arg);
            Assert.Equal(0, BattleCommand.DevGodMode(false).Arg);
        }

        [Fact]
        public void Hud_remaining_seconds_counts_down_from_the_duration()
        {
            var hud = new HudState { ElapsedTicks = 600, FloorDurationTicks = 1800 };

            Assert.Equal(40f, hud.RemainingSeconds(30), 3);
        }
    }
}
