using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The shared <see cref="IBattleEventSource"/>: a fixed-capacity ring of
    /// <see cref="BattleEvent"/> with independent reader cursors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by the owning session, read by presentation. Neither <see cref="Write"/>
    /// nor <see cref="Read"/> allocates, and the buffer never grows — a reader that
    /// falls behind by more than the capacity loses the oldest events, which
    /// <see cref="Read"/> reports through <c>dropped</c> instead of hiding.
    /// </para>
    /// <para>
    /// Sequence numbers are strictly increasing and never reused, so a reader can
    /// tell "nothing new" from "I missed some" by comparing its cursor with
    /// <see cref="OldestSeq"/>.
    /// </para>
    /// </remarks>
    public sealed class BattleEventRing : IBattleEventSource
    {
        private readonly BattleEvent[] _events;

        /// <summary>The sequence number the next written event will carry.</summary>
        private long _nextSeq;

        /// <summary>Creates a ring with the contract capacity.</summary>
        public BattleEventRing()
            : this(BattleConstants.EventRingCapacity)
        {
        }

        /// <summary>Creates a ring with an explicit capacity of at least one.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 1.</exception>
        public BattleEventRing(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1.");
            }

            _events = new BattleEvent[capacity];
        }

        /// <summary>Capacity of the ring; a reader further behind than this loses events.</summary>
        public int Capacity => _events.Length;

        /// <inheritdoc />
        public long OldestSeq => _nextSeq > _events.Length ? _nextSeq - _events.Length : 0L;

        /// <inheritdoc />
        public long NextSeq => _nextSeq;

        /// <summary>How many events are readable right now (<c>OldestSeq..NextSeq</c>).</summary>
        public int ReadableCount
        {
            get
            {
                long readable = _nextSeq - OldestSeq;
                return readable > int.MaxValue ? int.MaxValue : (int)readable;
            }
        }

        /// <summary>
        /// Appends one event. Overwrites the oldest event when the ring is full.
        /// Called by the owning session; nothing else writes here.
        /// </summary>
        /// <returns>The sequence number assigned to the event.</returns>
        public long Write(in BattleEvent template)
        {
            long seq = _nextSeq;
            var stored = new BattleEvent(
                seq, template.Type, template.Entity, template.DefId, template.Value, template.Flags, template.X, template.Y);
            _events[(int)(seq % _events.Length)] = stored;
            _nextSeq = seq + 1;
            return seq;
        }

        /// <summary>Clears the ring (all cursors become invalid; start new ones at 0).</summary>
        public void Clear()
        {
            Array.Clear(_events, 0, _events.Length);
            _nextSeq = 0L;
        }

        /// <inheritdoc />
        public int Read(ref long cursor, Span<BattleEvent> destination, out int dropped)
        {
            long oldest = OldestSeq;

            // A reader behind the oldest surviving event has lost the difference.
            if (cursor < oldest)
            {
                long missed = oldest - cursor;
                dropped = missed > int.MaxValue ? int.MaxValue : (int)missed;
                cursor = oldest;
            }
            else
            {
                dropped = 0;
            }

            if (cursor < 0)
            {
                // Never written, or a caller passing a default cursor: start at the oldest surviving event.
                cursor = oldest;
                dropped = 0;
            }

            long available = _nextSeq - cursor;
            if (available <= 0 || destination.Length == 0)
            {
                return 0;
            }

            int count = available > destination.Length ? destination.Length : (int)available;
            int capacity = _events.Length;
            for (int i = 0; i < count; i++)
            {
                destination[i] = _events[(int)((cursor + i) % capacity)];
            }

            cursor += count;
            return count;
        }
    }
}
