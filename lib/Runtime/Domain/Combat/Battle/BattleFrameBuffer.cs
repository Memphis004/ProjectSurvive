using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The one <see cref="IBattleFrame"/>: a reusable double buffer of
    /// <see cref="RenderEntity"/> snapshots (Prev/Cur) plus a single
    /// <see cref="HudState"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One instance per session, updated in place every tick, so publishing and
    /// reading a frame allocate nothing after construction. Two fixed arrays of at
    /// most <see cref="BattleConstants.MaxRenderEntities"/> alternate roles: the
    /// session writes into one while readers read the other, and
    /// <see cref="Publish"/> swaps them. After a publish, <see cref="Entities"/> is
    /// the tick just simulated ("Cur") and <see cref="PreviousEntities"/> is the
    /// tick before it ("Prev") — that pair is what lets Presentation interpolate
    /// Prev → Cur with an alpha while the simulation runs at 30 Hz.
    /// </para>
    /// <para>
    /// Why two slots and not a copy: the previous tick must still be readable while
    /// the next one is being built, and the buffer is a *view* rather than the
    /// source of truth, so copying a frame per tick would be pure cost. Inherent
    /// consequence: <see cref="PreviousEntities"/> is only meaningful between
    /// ticks — the next tick overwrites that slot in place. The contract already
    /// says both spans are valid only until the next
    /// <see cref="IBattleSession.Step"/> / <see cref="IBattleSession.Submit"/>.
    /// </para>
    /// <para>
    /// One writer (the session's tick) and any number of readers, on one thread.
    /// There is no synchronization and none is wanted: the Web target is
    /// single-threaded (AGENTS rule 6) and a lock on the hot path would be both an
    /// allocation and a determinism hazard.
    /// </para>
    /// <para>
    /// Shared on purpose. The fake (Application) publishes through this class, and
    /// the EcsLite simulation (Simulation, Stage 07) must publish through the same
    /// class rather than implementing <see cref="IBattleFrame"/> again, so a
    /// Presentation written against the fake sees identical frame behaviour from
    /// the real battle. An architecture test enforces that there is exactly one
    /// implementation.
    /// </para>
    /// <para>
    /// <see cref="HudState"/> is deliberately *not* double-buffered: a HUD is a
    /// single by-value snapshot overwritten each tick, and nothing interpolates it.
    /// </para>
    /// </remarks>
    public sealed class BattleFrameBuffer : IBattleFrame
    {
        private readonly RenderEntity[] _slotA;
        private readonly RenderEntity[] _slotB;

        /// <summary>The slot holding the published frame ("Cur").</summary>
        private RenderEntity[] _current;

        /// <summary>The slot holding the tick before it ("Prev") — and the write target.</summary>
        private RenderEntity[] _previous;

        private int _currentCount;
        private int _previousCount;
        private int _written;
        private HudState _hud;
        private long _tick;
        private long _pendingTick;

        /// <summary>Creates a buffer with the contract's capacity.</summary>
        public BattleFrameBuffer()
            : this(BattleConstants.MaxRenderEntities)
        {
        }

        /// <summary>Creates a buffer with an explicit capacity, clamped to the contract maximum.</summary>
        /// <remarks>
        /// Clamped rather than rejected because the cap is a contract bound, not a
        /// caller error: a caller that asks for more than
        /// <see cref="BattleConstants.MaxRenderEntities"/> gets the maximum.
        /// </remarks>
        public BattleFrameBuffer(int capacity)
        {
            int capped = capacity < 0 ? 0 : capacity;
            if (capped > BattleConstants.MaxRenderEntities)
            {
                capped = BattleConstants.MaxRenderEntities;
            }

            _slotA = new RenderEntity[capped];
            _slotB = new RenderEntity[capped];
            _current = _slotA;
            _previous = _slotB;
        }

        /// <inheritdoc />
        public long Tick => _tick;

        /// <inheritdoc />
        public HudState Hud => _hud;

        /// <inheritdoc />
        public ReadOnlySpan<RenderEntity> Entities => new ReadOnlySpan<RenderEntity>(_current, 0, _currentCount);

        /// <inheritdoc />
        public ReadOnlySpan<RenderEntity> PreviousEntities =>
            new ReadOnlySpan<RenderEntity>(_previous, 0, _previousCount);

        /// <summary>Capacity of either slot.</summary>
        public int Capacity => _slotA.Length;

        /// <summary>How many entities the current frame exposes.</summary>
        public int Count => _currentCount;

        /// <summary>How many entities the previous frame exposes.</summary>
        public int PreviousCount => _previousCount;

        /// <summary>Starts the frame for <paramref name="tick"/> and rewinds the write cursor.</summary>
        /// <remarks>
        /// Does not touch the published frame, so a reader that is mid-frame keeps
        /// seeing the last completed tick and <see cref="Tick"/> keeps naming it.
        /// The tick being written lands in the previous frame's slot, so
        /// <see cref="PreviousEntities"/> becomes stale as soon as the first
        /// <see cref="Add"/> of this frame runs.
        /// </remarks>
        public void BeginFrame(long tick)
        {
            _pendingTick = tick;
            _written = 0;
        }

        /// <summary>Replaces the HUD snapshot (a single value, copied straight over).</summary>
        /// <remarks>
        /// The one field that is *not* swapped by <see cref="Publish"/>: a HUD is
        /// never interpolated, so it is simply overwritten (ADR 0012). Between
        /// <see cref="BeginFrame"/> and <see cref="Publish"/> it can therefore run a
        /// tick ahead of <see cref="Tick"/> / <see cref="Entities"/> — read frames
        /// between ticks, as the contract already requires.
        /// </remarks>
        public void SetHud(in HudState hud)
        {
            _hud = hud;
        }

        /// <summary>Appends one entity, ignoring it when the slot is full.</summary>
        public void Add(in RenderEntity entity)
        {
            if (_written >= _previous.Length)
            {
                return;
            }

            _previous[_written] = entity;
            _written++;
        }

        /// <summary>Ends the frame and swaps the slots, making what was written the current frame.</summary>
        /// <remarks>
        /// Call once, after the tick's entities are all in. Nothing is copied: the
        /// two slot references trade places, so the frame just built becomes
        /// <see cref="Entities"/> and the one it replaced becomes
        /// <see cref="PreviousEntities"/> with its count intact. <see cref="Tick"/> is
        /// applied here rather than in <see cref="BeginFrame"/> so it always names
        /// the frame <see cref="Entities"/> actually holds.
        /// </remarks>
        public void Publish()
        {
            RenderEntity[] bulk = _current;
            int bulkCount = _currentCount;

            _current = _previous;
            _currentCount = _written;
            _tick = _pendingTick;

            _previous = bulk;
            _previousCount = bulkCount;

            _written = 0;
        }
    }
}
