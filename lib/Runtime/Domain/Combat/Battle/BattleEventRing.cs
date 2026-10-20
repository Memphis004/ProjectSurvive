using System;
using ProjectSurvive.Core.Shared;

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
    /// <para>
    /// The ring mechanics live once, in <see cref="EventRing{T}"/> (Stage 10t, ADR
    /// 0024); this type is the battle's payload on top of them. It stamps
    /// <see cref="BattleEvent.Seq"/> itself before writing, from the ring's own
    /// <see cref="NextSeq"/> — writes are single-threaded (AGENTS rule 6), so the
    /// number read is the number the ring assigns — which keeps
    /// <c>buffer[i].Seq == i</c> true for every reader.
    /// </para>
    /// </remarks>
    public sealed class BattleEventRing : IBattleEventSource
    {
        private readonly EventRing<BattleEvent> _ring;

        /// <summary>Creates a ring with the contract capacity.</summary>
        public BattleEventRing()
            : this(BattleConstants.EventRingCapacity)
        {
        }

        /// <summary>Creates a ring with an explicit capacity of at least one.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 1.</exception>
        public BattleEventRing(int capacity)
        {
            _ring = new EventRing<BattleEvent>(capacity);
        }

        /// <summary>Capacity of the ring; a reader further behind than this loses events.</summary>
        public int Capacity => _ring.Capacity;

        /// <inheritdoc />
        public long OldestSeq => _ring.OldestSeq;

        /// <inheritdoc />
        public long NextSeq => _ring.NextSeq;

        /// <summary>How many events are readable right now (<c>OldestSeq..NextSeq</c>).</summary>
        public int ReadableCount => _ring.ReadableCount;

        /// <summary>
        /// Appends one event, stamped with the sequence number the ring assigns it.
        /// Overwrites the oldest event when the ring is full. Called by the owning
        /// session; nothing else writes here.
        /// </summary>
        /// <returns>The sequence number assigned to the event.</returns>
        public long Write(in BattleEvent template)
        {
            long seq = _ring.NextSeq;
            var stored = new BattleEvent(
                seq, template.Type, template.Entity, template.DefId, template.Value, template.Flags,
                template.X, template.Y, template.Radius, template.Length, template.Angle);
            _ring.Write(stored);
            return seq;
        }

        /// <summary>Clears the ring (all cursors become invalid; start new ones at 0).</summary>
        public void Clear()
        {
            _ring.Clear();
        }

        /// <inheritdoc />
        public int Read(ref long cursor, Span<BattleEvent> destination, out int dropped) =>
            _ring.Read(ref cursor, destination, out dropped);
    }
}
