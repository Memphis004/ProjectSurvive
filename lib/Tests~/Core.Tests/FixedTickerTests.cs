using System;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The battle simulation runs at a fixed 30 Hz regardless of frame rate
    /// (AGENTS.md rule 6), and the conversion from frame deltas to tick counts
    /// has to be deterministic and drift-free.
    /// </summary>
    public sealed class FixedTickerTests
    {
        [Fact]
        public void Default_rate_is_30_hz()
        {
            var ticker = new FixedTicker();

            Assert.Equal(30.0, FixedTicker.DefaultTickHz);
            Assert.Equal(30.0, ticker.TickHz);
            Assert.Equal(1.0 / 30.0, ticker.TickSeconds, 12);
        }

        [Fact]
        public void One_second_of_sixty_fps_frames_runs_30_ticks()
        {
            var ticker = new FixedTicker();

            // Deliberately not one 1.0 s frame: that exceeds maxTicksPerAdvance
            // and is covered by the stall tests below.
            Assert.Equal(30, SumTicks(ticker, 1.0, 1.0 / 60.0));
            Assert.Equal(30L, ticker.TickIndex);
        }

        [Fact]
        public void Sixty_fps_produces_30_ticks_per_second_without_drift()
        {
            var ticker = new FixedTicker();
            long total = 0;

            // 10 seconds of 60 fps frames: every second must contribute exactly
            // 30 ticks, and no single call may exceed the per-advance cap.
            for (int frame = 0; frame < 600; frame++)
            {
                int ticks = ticker.Advance(1.0 / 60.0);
                Assert.InRange(ticks, 0, FixedTicker.DefaultMaxTicksPerAdvance);
                total += ticks;
            }

            Assert.Equal(300L, total);
            Assert.Equal(300L, ticker.TickIndex);
            Assert.Equal(0L, ticker.DroppedTicks);
            Assert.True(ticker.PendingSeconds < ticker.TickSeconds);
        }

        [Fact]
        public void Jittery_frame_times_still_track_real_time()
        {
            var ticker = new FixedTicker();
            long total = 0;
            double elapsed = 0.0;

            // Alternating fast/slow frames, ~17 ms each: real time must be
            // converted to ticks without accumulating drift.
            for (int frame = 0; frame < 1000; frame++)
            {
                double delta = frame % 2 == 0 ? 1.0 / 60.0 : 1.0 / 59.0;
                elapsed += delta;
                total += ticker.Advance(delta);
            }

            long expected = (long)(elapsed / ticker.TickSeconds);
            Assert.InRange(total, expected - 1, expected + 1);
        }

        [Fact]
        public void Sub_tick_deltas_accumulate_until_a_tick_is_due()
        {
            var ticker = new FixedTicker();

            Assert.Equal(0, ticker.Advance(ticker.TickSeconds / 4.0));
            Assert.Equal(0, ticker.Advance(ticker.TickSeconds / 4.0));
            Assert.Equal(1, ticker.Advance(ticker.TickSeconds / 4.0 + ticker.TickSeconds / 4.0));
            Assert.Equal(1L, ticker.TickIndex);
        }

        [Fact]
        public void A_long_frame_is_clamped_and_the_surplus_is_counted()
        {
            var ticker = new FixedTicker();

            int ticks = ticker.Advance(1.0);

            Assert.Equal(FixedTicker.DefaultMaxTicksPerAdvance, ticks);
            Assert.Equal(26L, ticker.DroppedTicks); // 30 ticks were due
            Assert.Equal(4L, ticker.TickIndex);
            Assert.Equal(0.0, ticker.PendingSeconds);
        }

        [Fact]
        public void A_stalled_frame_does_not_replay_a_backlog()
        {
            var ticker = new FixedTicker();

            int ticks = ticker.Advance(10.0);

            Assert.Equal(4, ticks);
            Assert.Equal(296L, ticker.DroppedTicks); // 300 ticks were due
            Assert.Equal(0.0, ticker.PendingSeconds);
        }

        [Fact]
        public void Non_positive_or_non_finite_deltas_never_advance_the_simulation()
        {
            var ticker = new FixedTicker();

            Assert.Equal(0, ticker.Advance(0.0));
            Assert.Equal(0, ticker.Advance(-1.0));
            Assert.Equal(0, ticker.Advance(double.NaN));
            Assert.Equal(0, ticker.Advance(double.PositiveInfinity));
            Assert.Equal(0, ticker.Advance(double.NegativeInfinity));
            Assert.Equal(0L, ticker.TickIndex);
            Assert.Equal(0.0, ticker.PendingSeconds);
        }

        [Fact]
        public void An_absurd_frame_length_is_bounded()
        {
            var ticker = new FixedTicker();

            int ticks = ticker.Advance(1e300);

            Assert.Equal(FixedTicker.DefaultMaxTicksPerAdvance, ticks);
            Assert.True(ticker.DroppedTicks > 0);
            Assert.True(ticker.TickIndex == FixedTicker.DefaultMaxTicksPerAdvance);
        }

        [Fact]
        public void A_delta_sequence_replays_identically()
        {
            var deltas = new[] { 1.0 / 60.0, 1.0 / 60.0, 0.05, 0.008, 0.033, 0.2, 1.0 / 60.0 };
            var first = new FixedTicker();
            var second = new FixedTicker();

            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < deltas.Length; i++)
                {
                    Assert.Equal(first.Advance(deltas[i]), second.Advance(deltas[i]));
                }
            }

            Assert.Equal(first.TickIndex, second.TickIndex);
            Assert.Equal(first.DroppedTicks, second.DroppedTicks);
            Assert.Equal(first.PendingSeconds, second.PendingSeconds);
        }

        [Fact]
        public void Other_rates_are_supported_for_tuning()
        {
            var ticker = new FixedTicker(60.0);

            Assert.Equal(60.0, ticker.TickHz);
            Assert.Equal(60, SumTicks(ticker, 1.0, 1.0 / 60.0));
        }

        [Fact]
        public void Reset_clears_every_counter()
        {
            var ticker = new FixedTicker();
            ticker.Advance(1.0);
            ticker.Advance(0.5);

            ticker.Reset();

            Assert.Equal(0L, ticker.TickIndex);
            Assert.Equal(0L, ticker.DroppedTicks);
            Assert.Equal(0.0, ticker.PendingSeconds);
        }

        [Fact]
        public void Invalid_configuration_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTicker(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTicker(-30.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTicker(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTicker(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTicker(30.0, 0));
        }

        private static long SumTicks(FixedTicker ticker, double totalSeconds, double delta)
        {
            long total = 0;
            int frames = (int)Math.Round(totalSeconds / delta);
            for (int i = 0; i < frames; i++)
            {
                total += ticker.Advance(delta);
            }

            return total;
        }
    }
}
