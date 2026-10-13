using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// One thing that happened, in the order it happened.
    /// </summary>
    /// <remarks>
    /// A value type so the event ring never allocates. <see cref="Seq"/> is a
    /// strictly increasing counter (one per event, never reused) so a reader can
    /// detect that it fell behind. <see cref="DefId"/> and <see cref="Value"/> are
    /// the event's typed payload — see <c>llmwiki/systems/battle-contract.md</c>
    /// for the meaning per event type.
    /// </remarks>
    public readonly struct BattleEvent
    {
        /// <summary>Creates an event.</summary>
        public BattleEvent(
            long seq,
            BattleEventType type,
            EntityRef entity = default,
            int defId = -1,
            int value = 0,
            BattleEventFlags flags = BattleEventFlags.None,
            float x = 0f,
            float y = 0f)
        {
            Seq = seq;
            Type = type;
            Entity = entity;
            DefId = defId;
            Value = value;
            Flags = flags;
            X = x;
            Y = y;
        }

        /// <summary>Strictly increasing sequence number; one per event, never reused.</summary>
        public long Seq { get; }

        /// <summary>What happened.</summary>
        public BattleEventType Type { get; }

        /// <summary>The entity the event is about, when it has one.</summary>
        public EntityRef Entity { get; }

        /// <summary>Numeric def id (enemy / skill / item / status), or -1 when the event carries none.</summary>
        public int DefId { get; }

        /// <summary>Event-specific integer payload; 0 when the event carries none.</summary>
        public int Value { get; }

        /// <summary>Optional tags.</summary>
        public BattleEventFlags Flags { get; }

        /// <summary>World X of the event, when it has a position.</summary>
        public float X { get; }

        /// <summary>World Y of the event, when it has a position.</summary>
        public float Y { get; }

        /// <inheritdoc />
        public override string ToString() => "#" + Seq + " " + Type + " v=" + Value;
    }

    /// <summary>
    /// A replayable, multi-reader stream of <see cref="BattleEvent"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each reader owns its own <c>cursor</c> (the <c>Seq</c> it will read next),
    /// so audio, VFX and the HUD can consume at different rates without
    /// interfering. Reading never mutates any state a rule depends on.
    /// </para>
    /// <para>
    /// The stream is bounded: a reader that falls further behind than the ring's
    /// capacity loses the oldest events, and they are reported through
    /// <c>dropped</c> rather than silently skipped so Presentation can react.
    /// </para>
    /// </remarks>
    public interface IBattleEventSource
    {
        /// <summary>The sequence number of the oldest event still readable (a new reader starts here).</summary>
        long OldestSeq { get; }

        /// <summary>The sequence number the next written event will carry.</summary>
        long NextSeq { get; }

        /// <summary>
        /// Copies up to <c>destination.Length</c> unread events into
        /// <paramref name="destination"/>, advances <paramref name="cursor"/> past
        /// what was copied, and returns how many events were copied.
        /// </summary>
        /// <param name="cursor">
        /// The reader's own position (the <c>Seq</c> to read next). Start it at
        /// <see cref="OldestSeq"/>. Updated in place.
        /// </param>
        /// <param name="destination">Where to write; its length bounds the read.</param>
        /// <param name="dropped">
        /// How many events were lost because the reader fell behind by more than
        /// the ring's capacity, since the last read. 0 when the reader kept up.
        /// </param>
        int Read(ref long cursor, Span<BattleEvent> destination, out int dropped);
    }
}
