using System.Diagnostics;
using ProjectSurvive.Core.Infrastructure;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Infrastructure supplies the real <see cref="IClock"/>; Core only knows the
    /// port. These checks are deliberately loose on absolute values — wall-clock
    /// precision is not something a unit test can assert — but strict on the
    /// contract: monotonic, non-negative, and restarted by <c>Restart</c>.
    /// </summary>
    public sealed class InfrastructureTests
    {
        [Fact]
        public void SystemClock_implements_the_clock_port()
        {
            var clock = new SystemClock();
            Assert.IsAssignableFrom<IClock>(clock);
        }

        [Fact]
        public void SystemClock_is_monotonic_and_non_negative()
        {
            var clock = new SystemClock();

            double start = clock.NowSeconds;
            Assert.True(start >= 0.0, "elapsed time must never be negative");

            var deadline = Stopwatch.StartNew();
            double previous = start;
            while (clock.NowSeconds < start + 0.002 && deadline.ElapsedMilliseconds < 5_000)
            {
                double current = clock.NowSeconds;
                Assert.True(current >= previous, "clock went backwards");
                previous = current;
            }

            Assert.True(clock.NowSeconds >= start + 0.002, "clock did not advance");
        }

        [Fact]
        public void Restart_returns_the_elapsed_span_and_resets_the_epoch()
        {
            var clock = new SystemClock();
            var deadline = Stopwatch.StartNew();
            while (clock.NowSeconds < 0.002 && deadline.ElapsedMilliseconds < 5_000)
            {
                // Let some time pass.
            }

            double elapsed = clock.Restart();

            Assert.True(elapsed >= 0.002, "Restart did not report the elapsed span");
            Assert.True(clock.NowSeconds < elapsed, "the epoch was not reset");
        }
    }
}
