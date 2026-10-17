using System;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The generic Prev/Cur double buffer (Stage 10t): two slots swapped on
    /// <c>Publish</c>, a frame in progress invisible until it is published, the
    /// capacity bound, and no allocation once warmed up.
    /// </summary>
    /// <remarks>
    /// ADR 0012's mechanics with the payload made generic, so the same file serves
    /// the template branch (ADR 0024). The battle-specific half — the HUD's single
    /// value, the cooldown list and the contract caps — stays in
    /// <see cref="BattleFrameBufferTests"/>.
    /// </remarks>
    public sealed class PrevCurBufferTests
    {
        [Fact]
        public void The_first_frame_has_no_previous_items()
        {
            var buffer = new PrevCurBuffer<int>(4);
            buffer.BeginFrame();
            buffer.Add(10);
            buffer.Add(20);
            buffer.Publish();

            Assert.Equal(2, buffer.CurrentCount);
            Assert.Equal(0, buffer.PreviousCount);
            Assert.Equal(10, buffer.Current[0]);
            Assert.Equal(20, buffer.Current[1]);
        }

        [Fact]
        public void Publish_swaps_current_and_previous_every_tick()
        {
            var buffer = new PrevCurBuffer<int>(4);

            buffer.BeginFrame();
            buffer.Add(0);
            buffer.Add(1);
            buffer.Publish();

            buffer.BeginFrame();
            buffer.Add(2);
            buffer.Publish();

            // Tick 1 is current; tick 0 is still readable as the other end of the
            // interpolation pair, untouched by the tick that came after it.
            Assert.Equal(1, buffer.CurrentCount);
            Assert.Equal(2, buffer.Current[0]);

            Assert.Equal(2, buffer.PreviousCount);
            Assert.Equal(0, buffer.Previous[0]);
            Assert.Equal(1, buffer.Previous[1]);

            buffer.BeginFrame();
            buffer.Add(3);
            buffer.Add(4);
            buffer.Add(5);
            buffer.Publish();

            // Two slots only: Previous is the immediately preceding tick, not history.
            Assert.Equal(3, buffer.CurrentCount);
            Assert.Equal(1, buffer.PreviousCount);
            Assert.Equal(2, buffer.Previous[0]);
        }

        [Fact]
        public void A_frame_in_progress_is_not_published_until_publish_ran()
        {
            var buffer = new PrevCurBuffer<int>(4);
            buffer.BeginFrame();
            buffer.Add(0);
            buffer.Publish();

            buffer.BeginFrame();
            buffer.Add(7);
            buffer.Add(8);

            // Readers see the last completed frame, never a half-built one.
            Assert.Equal(1, buffer.CurrentCount);
            Assert.Equal(0, buffer.Current[0]);
            Assert.Equal(0, buffer.PreviousCount);

            buffer.Publish();

            Assert.Equal(2, buffer.CurrentCount);
            Assert.Equal(7, buffer.Current[0]);
        }

        [Fact]
        public void Add_stops_at_the_capacity()
        {
            var buffer = new PrevCurBuffer<int>(2);

            buffer.BeginFrame();
            for (int i = 0; i < 10; i++)
            {
                buffer.Add(i);
            }

            buffer.Publish();

            // The frame is a view: surplus items are dropped, never buffered by
            // growing the slot (which would allocate on the hot path).
            Assert.Equal(2, buffer.CurrentCount);
            Assert.Equal(0, buffer.Current[0]);
            Assert.Equal(1, buffer.Current[1]);
        }

        [Fact]
        public void A_zero_capacity_buffer_is_empty_and_never_throws()
        {
            var buffer = new PrevCurBuffer<int>(0);

            buffer.BeginFrame();
            buffer.Add(1);
            buffer.Publish();

            Assert.Equal(0, buffer.Capacity);
            Assert.Equal(0, buffer.CurrentCount);
            Assert.Equal(0, buffer.PreviousCount);
            Assert.True(buffer.Current.IsEmpty);
            Assert.True(buffer.Previous.IsEmpty);
        }

        [Fact]
        public void A_negative_capacity_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PrevCurBuffer<int>(-1));
        }

        [Fact]
        public void Begin_frame_publish_and_read_do_not_allocate()
        {
            var buffer = new PrevCurBuffer<int>(64);

            // Warm up JIT.
            for (int i = 0; i < 10_000; i++)
            {
                buffer.BeginFrame();
                buffer.Add(i);
                buffer.Publish();

                // Reading the spans is where a naive implementation would allocate;
                // the count is used afterwards so the reads cannot be elided.
                _ = buffer.Current.Length;
                _ = buffer.Previous.Length;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            int seen = 0;
            for (int i = 0; i < 10_000; i++)
            {
                buffer.BeginFrame();
                buffer.Add(i);
                buffer.Publish();
                seen += buffer.Current.Length;
                seen += buffer.Previous.Length;
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.True(seen > 0);
            Assert.Equal(0L, after - before);
        }
    }
}
