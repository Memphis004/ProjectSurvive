using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// Two fixed slots of <typeparamref name="T"/>, swapped every tick: the one just
    /// written ("Cur") and the tick before it ("Prev").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The arrangement ADR 0012 chose for the battle frame, generic so the same file
    /// serves any game built on this repository (ADR 0024). The session writes into a
    /// slot while readers read the other, and <see cref="Publish"/> swaps the slot
    /// references — nothing is copied:
    /// </para>
    /// <code>
    ///   BeginFrame()  rewind the write cursor   (the published slot is untouched)
    ///   Add(item)     append into the slot the next Publish swaps in
    ///   Publish()     swap: what was written becomes Cur, what Cur was becomes Prev
    /// </code>
    /// <para>
    /// After a publish, <see cref="Current"/> is the tick just produced and
    /// <see cref="Previous"/> is the one before it *with its count intact*, because
    /// the frame was never overwritten — only the slot roles changed. Two slots means
    /// Prev is the immediately preceding tick, never history, and the slot being
    /// written <b>is</b> the slot that holds Prev, so <see cref="Previous"/> is only
    /// meaningful *between* ticks.
    /// </para>
    /// <para>
    /// One writer and any number of readers on one thread; no synchronization, no
    /// allocation after construction (AGENTS rule 6).
    /// </para>
    /// </remarks>
    public sealed class PrevCurBuffer<T>
    {
        private readonly T[] _slotA;
        private readonly T[] _slotB;

        /// <summary>The slot holding the published frame ("Cur").</summary>
        private T[] _current;

        /// <summary>The slot holding the tick before it ("Prev") — and the write target.</summary>
        private T[] _previous;

        private int _currentCount;
        private int _previousCount;
        private int _written;

        /// <summary>Creates a buffer with an explicit capacity of at least zero.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
        public PrevCurBuffer(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must not be negative.");
            }

            _slotA = new T[capacity];
            _slotB = new T[capacity];
            _current = _slotA;
            _previous = _slotB;
        }

        /// <summary>Capacity of either slot.</summary>
        public int Capacity => _slotA.Length;

        /// <summary>How many items the current frame exposes.</summary>
        public int CurrentCount => _currentCount;

        /// <summary>How many items the previous frame exposes.</summary>
        public int PreviousCount => _previousCount;

        /// <summary>The frame just published ("Cur"); valid until the next <see cref="BeginFrame"/>.</summary>
        public ReadOnlySpan<T> Current => new ReadOnlySpan<T>(_current, 0, _currentCount);

        /// <summary>The tick before it ("Prev"); valid until the next <see cref="BeginFrame"/>.</summary>
        public ReadOnlySpan<T> Previous => new ReadOnlySpan<T>(_previous, 0, _previousCount);

        /// <summary>Starts a frame and rewinds the write cursor.</summary>
        /// <remarks>
        /// Does not touch the published frame, so a reader between ticks keeps seeing
        /// the last completed one. The frame being written lands in the previous
        /// frame's slot, so <see cref="Previous"/> becomes stale as soon as the first
        /// <see cref="Add"/> of this frame runs.
        /// </remarks>
        public void BeginFrame()
        {
            _written = 0;
        }

        /// <summary>Appends one item, ignoring it when the slot is full.</summary>
        public void Add(in T item)
        {
            if (_written >= _previous.Length)
            {
                return;
            }

            _previous[_written] = item;
            _written++;
        }

        /// <summary>Ends the frame and swaps the slots, making what was written the current frame.</summary>
        /// <remarks>
        /// Nothing is copied: the two slot references trade places, so the frame just
        /// built becomes <see cref="Current"/> and the one it replaced becomes
        /// <see cref="Previous"/> with its count intact.
        /// </remarks>
        public void Publish()
        {
            T[] bulk = _current;
            int bulkCount = _currentCount;

            _current = _previous;
            _currentCount = _written;

            _previous = bulk;
            _previousCount = bulkCount;

            _written = 0;
        }
    }
}
