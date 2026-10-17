using System;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The generic event ring (Stage 10t): fixed capacity, strictly increasing
    /// sequence numbers, one cursor per reader, honest dropped reporting, and no
    /// allocation once warmed up.
    /// </summary>
    /// <remarks>
    /// These are the mechanics ADR 0007 §5 fixed for the battle contract, moved
    /// into <c>Shared</c> so the same file serves the template branch's own
    /// payload types (ADR 0024). The battle-specific half lives in
    /// <see cref="BattleEventRingTests"/>, which asserts the same behaviour
    /// through <c>BattleEvent</c> and its sequence stamping.
    /// </remarks>
    public sealed class EventRingTests
    {
        private readonly struct Item
        {
            public Item(int value) => Value = value;

            public int Value { get; }
        }

        [Fact]
        public void Readers_keep_independent_cursors()
        {
            var ring = new EventRing<Item>(8);
            for (int i = 0; i < 5; i++)
            {
                ring.Write(new Item(i));
            }

            var buffer = new Item[5];

            long a = ring.OldestSeq;
            Assert.Equal(5, ring.Read(ref a, buffer, out _));
            Assert.Equal(0, buffer[0].Value);
            Assert.Equal(4, buffer[4].Value);

            // A second reader starts fresh and sees the same five items.
            long b = ring.OldestSeq;
            Assert.Equal(5, ring.Read(ref b, buffer, out _));
            Assert.Equal(0, buffer[0].Value);

            // The first reader is now caught up.
            Assert.Equal(0, ring.Read(ref a, buffer, out _));
        }

        [Fact]
        public void Falling_behind_the_capacity_reports_the_dropped_count()
        {
            var ring = new EventRing<Item>(4);
            var buffer = new Item[4];
            long cursor = ring.OldestSeq;

            for (int i = 0; i < 10; i++)
            {
                ring.Write(new Item(i));
            }

            int read = ring.Read(ref cursor, buffer, out int dropped);

            Assert.Equal(4, read);
            Assert.Equal(6, dropped);
            // The oldest surviving four items are the last four written.
            Assert.Equal(6, buffer[0].Value);
            Assert.Equal(9, buffer[3].Value);
        }

        [Fact]
        public void Sequence_numbers_increase_by_one_and_never_repeat()
        {
            var ring = new EventRing<Item>(8);

            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(i, ring.Write(new Item(i)));
            }

            Assert.Equal(6, ring.NextSeq);
            Assert.Equal(0, ring.OldestSeq);
            Assert.Equal(6, ring.ReadableCount);

            var buffer = new Item[6];
            long cursor = 0;
            int read = ring.Read(ref cursor, buffer, out _);

            Assert.Equal(6, read);
            for (int i = 0; i < read; i++)
            {
                Assert.Equal(i, buffer[i].Value);
            }
        }

        [Fact]
        public void Sequence_numbers_keep_increasing_after_the_ring_wraps()
        {
            var ring = new EventRing<Item>(4);
            for (int i = 0; i < 10; i++)
            {
                ring.Write(new Item(i));
            }

            // The oldest surviving item is #6 and the next one written is #10.
            Assert.Equal(6, ring.OldestSeq);
            Assert.Equal(10, ring.NextSeq);
            Assert.Equal(4, ring.ReadableCount);
            Assert.Equal(10, ring.Write(new Item(10)));
        }

        [Fact]
        public void Clear_resets_the_sequence()
        {
            var ring = new EventRing<Item>(4);
            ring.Write(new Item(1));
            ring.Write(new Item(2));

            ring.Clear();

            Assert.Equal(0, ring.NextSeq);
            Assert.Equal(0, ring.OldestSeq);
            Assert.Equal(0, ring.ReadableCount);
        }

        /// <summary>
        /// A cursor that was never set (the default <c>-1</c>, or any negative value) is a new
        /// reader, not a reader that fell behind: it starts at the oldest surviving item and is not
        /// charged a dropped count it never lost.
        /// </summary>
        [Fact]
        public void A_negative_cursor_starts_at_the_oldest_item_without_reporting_a_drop()
        {
            var ring = new EventRing<Item>(4);
            for (int i = 0; i < 6; i++)
            {
                ring.Write(new Item(i));
            }

            var buffer = new Item[4];
            long cursor = -1;

            int read = ring.Read(ref cursor, buffer, out int dropped);

            Assert.Equal(4, read);
            Assert.Equal(0, dropped);
            Assert.Equal(2, buffer[0].Value);
            Assert.Equal(5, buffer[3].Value);
        }

        [Fact]
        public void A_read_is_bounded_by_the_destination()
        {
            var ring = new EventRing<Item>(8);
            for (int i = 0; i < 5; i++)
            {
                ring.Write(new Item(i));
            }

            var buffer = new Item[2];
            long cursor = ring.OldestSeq;

            Assert.Equal(2, ring.Read(ref cursor, buffer, out _));
            Assert.Equal(0, buffer[0].Value);
            Assert.Equal(1, buffer[1].Value);

            // The cursor moved past what was copied, so the rest arrives next time
            // — still bounded by the destination.
            Assert.Equal(2, ring.Read(ref cursor, buffer, out _));
            Assert.Equal(2, buffer[0].Value);
            Assert.Equal(3, buffer[1].Value);

            Assert.Equal(1, ring.Read(ref cursor, buffer, out _));
            Assert.Equal(4, buffer[0].Value);
        }

        [Fact]
        public void A_capacity_below_one_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventRing<Item>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventRing<Item>(-3));
        }

        [Fact]
        public void Write_and_read_do_not_allocate()
        {
            var ring = new EventRing<Item>(64);
            var buffer = new Item[16];
            long cursor = ring.OldestSeq;

            // Warm up JIT and the reader path before the measured window.
            for (int i = 0; i < 10_000; i++)
            {
                ring.Write(new Item(i));
                ring.Read(ref cursor, buffer, out _);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10_000; i++)
            {
                ring.Write(new Item(i));
                ring.Read(ref cursor, buffer, out _);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }
    }
}
