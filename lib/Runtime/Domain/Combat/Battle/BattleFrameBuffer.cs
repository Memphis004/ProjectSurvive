using System;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The one <see cref="IBattleFrame"/>: a reusable double buffer of
    /// <see cref="RenderEntity"/> snapshots (Prev/Cur), the loadout cooldown list that
    /// swaps with them, and a single <see cref="HudState"/>.
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
    /// Since Stage 10t the swap itself is the generic
    /// <see cref="PrevCurBuffer{T}"/> in <c>Shared</c> (ADR 0024): this class holds
    /// one for the entities and one for the cooldown list. The capacity caps stay
    /// here because they are battle-contract bounds, not properties of a generic
    /// buffer.
    /// </para>
    /// <para>
    /// <see cref="HudState"/> is deliberately *not* double-buffered: a HUD is a
    /// single by-value snapshot overwritten each tick, and nothing interpolates it.
    /// The cooldown list *is* swapped with the entities (Stage 08b) — it is not a
    /// single value but a list filled entry by entry, so overwriting it in place
    /// would let a reader holding a frame see a half-written list.
    /// </para>
    /// </remarks>
    public sealed class BattleFrameBuffer : IBattleFrame
    {
        /// <summary>The entity slots, swapped as one pair on <see cref="Publish"/>.</summary>
        private readonly PrevCurBuffer<RenderEntity> _entities;

        /// <summary>The cooldown slots, swapping with the entity slots so a reader is never mid-write.</summary>
        private readonly PrevCurBuffer<LoadoutCooldown> _cooldowns;

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

            int coolCapped = capped > BattleConstants.MaxLoadoutEntries ? BattleConstants.MaxLoadoutEntries : capped;

            _entities = new PrevCurBuffer<RenderEntity>(capped);
            _cooldowns = new PrevCurBuffer<LoadoutCooldown>(coolCapped);
        }

        /// <inheritdoc />
        public long Tick => _tick;

        /// <inheritdoc />
        public HudState Hud => _hud;

        /// <inheritdoc />
        public ReadOnlySpan<RenderEntity> Entities => _entities.Current;

        /// <inheritdoc />
        public ReadOnlySpan<RenderEntity> PreviousEntities => _entities.Previous;

        /// <inheritdoc />
        public ReadOnlySpan<LoadoutCooldown> Cooldowns => _cooldowns.Current;

        /// <summary>Capacity of either entity slot.</summary>
        public int Capacity => _entities.Capacity;

        /// <summary>How many entities the current frame exposes.</summary>
        public int Count => _entities.CurrentCount;

        /// <summary>How many entities the previous frame exposes.</summary>
        public int PreviousCount => _entities.PreviousCount;

        /// <summary>How many loadout cooldowns the current frame exposes.</summary>
        public int CooldownCount => _cooldowns.CurrentCount;

        /// <summary>Starts the frame for <paramref name="tick"/> and rewinds the write cursors.</summary>
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
            _entities.BeginFrame();
            _cooldowns.BeginFrame();
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
            _entities.Add(entity);
        }

        /// <summary>Appends one loadout entry's casting state, ignoring it when the slot is full.</summary>
        /// <remarks>
        /// Written into the slot the next <see cref="Publish"/> swaps in, exactly like the entities, so
        /// a reader holding the published frame never sees half a list. The cap is
        /// <see cref="BattleConstants.MaxLoadoutEntries"/>; a loadout that exceeded it would lose its
        /// tail here, which a real bag cannot reach (the board has fewer cells than that).
        /// </remarks>
        public void AddCooldown(in LoadoutCooldown cooldown)
        {
            _cooldowns.Add(cooldown);
        }

        /// <summary>Ends the frame and swaps the slots, making what was written the current frame.</summary>
        /// <remarks>
        /// Call once, after the tick's entities are all in. Nothing is copied: each
        /// buffer's two slot references trade places, so the frame just built becomes
        /// <see cref="Entities"/> and the one it replaced becomes
        /// <see cref="PreviousEntities"/> with its count intact. <see cref="Tick"/> is
        /// applied here rather than in <see cref="BeginFrame"/> so it always names
        /// the frame <see cref="Entities"/> actually holds.
        /// </remarks>
        public void Publish()
        {
            _entities.Publish();
            _cooldowns.Publish();
            _tick = _pendingTick;
        }
    }
}
