using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// A reusable <see cref="IBattleFrame"/> backed by a fixed entity array.
    /// </summary>
    /// <remarks>
    /// One instance per session, updated in place each tick, so reading a frame
    /// allocates nothing. Capacity is <see cref="BattleConstants.MaxRenderEntities"/>;
    /// callers that add more simply stop at the cap (the frame is a *view*, never
    /// the source of truth). Shared by the fake session now and the EcsLite
    /// simulation later so both honour one frame contract.
    /// </remarks>
    public sealed class BattleFrameBuffer : IBattleFrame
    {
        private readonly RenderEntity[] _entities;
        private HudState _hud;
        private int _count;
        private long _tick;

        /// <summary>Creates a buffer with the default capacity.</summary>
        public BattleFrameBuffer()
            : this(BattleConstants.MaxRenderEntities)
        {
        }

        /// <summary>Creates a buffer with an explicit capacity, clamped to the contract maximum.</summary>
        public BattleFrameBuffer(int capacity)
        {
            int capped = capacity < 0 ? 0 : capacity;
            if (capped > BattleConstants.MaxRenderEntities)
            {
                capped = BattleConstants.MaxRenderEntities;
            }

            _entities = new RenderEntity[capped];
        }

        /// <inheritdoc />
        public long Tick => _tick;

        /// <inheritdoc />
        public HudState Hud => _hud;

        /// <inheritdoc />
        public ReadOnlySpan<RenderEntity> Entities => new ReadOnlySpan<RenderEntity>(_entities, 0, _count);

        /// <summary>Capacity of the underlying array.</summary>
        public int Capacity => _entities.Length;

        /// <summary>How many entities the current frame exposes.</summary>
        public int Count => _count;

        /// <summary>Starts a new frame: clears the entity list and sets the tick.</summary>
        public void BeginFrame(long tick)
        {
            _tick = tick;
            _count = 0;
        }

        /// <summary>Replaces the HUD snapshot.</summary>
        public void SetHud(in HudState hud)
        {
            _hud = hud;
        }

        /// <summary>Appends one entity, ignoring it when the buffer is full.</summary>
        public void Add(in RenderEntity entity)
        {
            if (_count >= _entities.Length)
            {
                return;
            }

            _entities[_count] = entity;
            _count++;
        }
    }
}
