using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// A fixed-capacity ring of <typeparamref name="T"/> with strictly increasing
    /// sequence numbers and one cursor per reader.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mechanics the battle contract needs (ADR 0007 §5), generic so the same
    /// file serves any game built on this repository (ADR 0024):
    /// </para>
    /// <list type="bullet">
    /// <item><description>the buffer never grows, and <see cref="Write"/> / <see cref="Read"/> allocate nothing;</description></item>
    /// <item><description>sequence numbers are strictly increasing and never reused, so a reader can tell
    /// "nothing new" from "I missed something" by comparing its cursor with <see cref="OldestSeq"/>;</description></item>
    /// <item><description>a reader that falls behind by more than the capacity loses the oldest items, and
    /// <see cref="Read"/> reports how many through <c>dropped</c> instead of hiding the loss;</description></item>
    /// <item><description>readers do not interfere: each owns its cursor, so several can consume at their own rate.</description></item>
    /// </list>
    /// <para>
    /// One writer and any number of readers on one thread; there is no synchronization
    /// and none is wanted (AGENTS rule 6 — the Web target is single-threaded).
    /// </para>
    /// <para>
    /// <typeparamref name="T"/> is stored as-is. The ring does not stamp a sequence
    /// number onto it, because the payload might not have a field for one; a payload
    /// that carries its own sequence number reads <see cref="NextSeq"/> before
    /// writing (see <c>BattleEventRing</c>, ADR 0024 §2).
    /// </para>
    /// </remarks>
    public sealed class EventRing<T>
    {
        private readonly T[] _items;

        /// <summary>The sequence number the next written item will carry.</summary>
        private long _nextSeq;

        /// <summary>Creates a ring with an explicit capacity of at least one.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 1.</exception>
        public EventRing(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1.");
            }

            _items = new T[capacity];
        }

        /// <summary>Capacity of the ring; a reader further behind than this loses items.</summary>
        public int Capacity => _items.Length;

        /// <summary>The sequence number of the oldest item still readable.</summary>
        public long OldestSeq => _nextSeq > _items.Length ? _nextSeq - _items.Length : 0L;

        /// <summary>The sequence number the next written item will carry.</summary>
        public long NextSeq => _nextSeq;

        /// <summary>How many items are readable right now (<c>OldestSeq..NextSeq</c>).</summary>
        public int ReadableCount
        {
            get
            {
                long readable = _nextSeq - OldestSeq;
                return readable > int.MaxValue ? int.MaxValue : (int)readable;
            }
        }

        /// <summary>
        /// Appends one item, overwriting the oldest when the ring is full. Called by
        /// the owning session; nothing else writes here.
        /// </summary>
        /// <returns>The sequence number assigned to the item.</returns>
        public long Write(in T item)
        {
            long seq = _nextSeq;
            _items[(int)(seq % _items.Length)] = item;
            _nextSeq = seq + 1;
            return seq;
        }

        /// <summary>Clears the ring (all cursors become invalid; start new ones at 0).</summary>
        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _nextSeq = 0L;
        }

        /// <summary>
        /// Copies up to <c>destination.Length</c> unread items into
        /// <paramref name="destination"/> and advances <paramref name="cursor"/> past
        /// what was copied.
        /// </summary>
        /// <param name="cursor">
        /// The reader's own position (the sequence number to read next). Start it at
        /// <see cref="OldestSeq"/>; a negative value means "not started yet" and
        /// begins at the oldest surviving item without reporting a drop.
        /// </param>
        /// <param name="destination">Where to write; its length bounds the read.</param>
        /// <param name="dropped">
        /// How many items were lost because the reader fell behind by more than the
        /// ring's capacity, since the last read. 0 when the reader kept up.
        /// </param>
        /// <returns>How many items were copied.</returns>
        public int Read(ref long cursor, Span<T> destination, out int dropped)
        {
            long oldest = OldestSeq;

            if (cursor < 0)
            {
                // Never written: a default cursor starts at the oldest surviving item,
                // and a reader that has not started has not fallen behind.
                cursor = oldest;
                dropped = 0;
            }
            else if (cursor < oldest)
            {
                // A reader behind the oldest surviving item has lost the difference.
                long missed = oldest - cursor;
                dropped = missed > int.MaxValue ? int.MaxValue : (int)missed;
                cursor = oldest;
            }
            else
            {
                dropped = 0;
            }

            long available = _nextSeq - cursor;
            if (available <= 0 || destination.Length == 0)
            {
                return 0;
            }

            int count = available > destination.Length ? destination.Length : (int)available;
            int capacity = _items.Length;
            for (int i = 0; i < count; i++)
            {
                destination[i] = _items[(int)((cursor + i) % capacity)];
            }

            cursor += count;
            return count;
        }
    }
}
