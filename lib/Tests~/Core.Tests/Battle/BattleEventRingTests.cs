using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The <see cref="IBattleEventSource"/> contract as implemented by
    /// <see cref="BattleEventRing"/>: independent readers, strictly increasing
    /// sequence numbers, honest dropped reporting, and no allocation.
    /// </summary>
    public sealed class BattleEventRingTests
    {
        private static BattleEvent Event(int value) =>
            new BattleEvent(0, BattleEventType.EnemySpawned, default, -1, value);

        [Fact]
        public void Readers_keep_independent_cursors()
        {
            var ring = new BattleEventRing(8);
            for (int i = 0; i < 5; i++)
            {
                ring.Write(Event(i));
            }

            var buffer = new BattleEvent[5];

            long a = ring.OldestSeq;
            Assert.Equal(5, ring.Read(ref a, buffer, out _));
            Assert.Equal(0, buffer[0].Value);
            Assert.Equal(4, buffer[4].Value);

            // A second reader starts fresh and sees the same five events.
            long b = ring.OldestSeq;
            Assert.Equal(5, ring.Read(ref b, buffer, out _));
            Assert.Equal(0, buffer[0].Value);

            // The first reader is now caught up.
            Assert.Equal(0, ring.Read(ref a, buffer, out _));
        }

        [Fact]
        public void Falling_behind_the_capacity_reports_the_dropped_count()
        {
            var ring = new BattleEventRing(4);
            var buffer = new BattleEvent[4];
            long cursor = ring.OldestSeq; // 0

            for (int i = 0; i < 10; i++)
            {
                ring.Write(Event(i));
            }

            int read = ring.Read(ref cursor, buffer, out int dropped);

            Assert.Equal(4, read);
            Assert.Equal(6, dropped);
            // The oldest surviving four events are the last four written.
            Assert.Equal(6, buffer[0].Value);
            Assert.Equal(9, buffer[3].Value);
        }

        [Fact]
        public void Sequence_numbers_increase_by_one_and_never_repeat()
        {
            var ring = new BattleEventRing(8);
            for (int i = 0; i < 6; i++)
            {
                ring.Write(Event(i));
            }

            var buffer = new BattleEvent[6];
            long cursor = 0;
            int read = ring.Read(ref cursor, buffer, out _);

            Assert.Equal(6, read);
            for (int i = 0; i < read; i++)
            {
                Assert.Equal(i, buffer[i].Seq);
                Assert.Equal(i, buffer[i].Value);
            }
        }

        [Fact]
        public void Clear_resets_the_sequence()
        {
            var ring = new BattleEventRing(4);
            ring.Write(Event(1));
            ring.Write(Event(2));

            ring.Clear();

            Assert.Equal(0, ring.NextSeq);
            Assert.Equal(0, ring.OldestSeq);
            Assert.Equal(0, ring.ReadableCount);
        }

        [Fact]
        public void Write_and_read_do_not_allocate()
        {
            var ring = new BattleEventRing(64);
            var buffer = new BattleEvent[16];
            long cursor = 0;

            // Warm up JIT.
            for (int i = 0; i < 10_000; i++)
            {
                ring.Write(Event(i));
                ring.Read(ref cursor, buffer, out _);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10_000; i++)
            {
                ring.Write(Event(i));
                ring.Read(ref cursor, buffer, out _);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }
    }
}
