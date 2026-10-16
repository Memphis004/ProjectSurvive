using System;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// A private copy of one published frame, taken the moment the tick that produced it
    /// finishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IBattleFrame.Entities"/> and <see cref="IBattleFrame.PreviousEntities"/>
    /// are views over two slots the session reuses in place: they stay readable only until
    /// the next <c>Step</c> or <c>Submit</c> ([ADR 0012], and the acceptance line "span ของ
    /// <c>Entities</c>/<c>PreviousEntities</c> ถูก copy ก่อน <c>Step</c>/<c>Submit</c>
    /// ครั้งถัดไป"). The runner steps, copies here, and only then renders — so the render
    /// code reading a span is never reading a slot the simulation has started overwriting.
    /// </para>
    /// <para>
    /// Both arrays are allocated once at the contract's cap
    /// (<see cref="BattleConstants.MaxRenderEntities"/>), so a copy costs a bounded
    /// <c>memcpy</c>-shaped loop and no allocation, and a frame that somehow exceeded the
    /// cap is truncated rather than grown: the frame is a view and never the source of
    /// truth. <see cref="Cooldowns"/> is copied the same way, at
    /// <see cref="BattleConstants.MaxLoadoutEntries"/> (Stage 08c), because the mini bag reads a
    /// cooldown on the frame the tick belongs to — never a clock of its own.
    /// </para>
    /// <para>
    /// The previous tick is indexed by entity slot (<see cref="TryGetPrevious"/>) rather
    /// than searched, because a linear match per entity would be quadratic at the render
    /// cap. The index is only trusted when the version matches, so a recycled slot cannot
    /// interpolate a new entity from a dead one's position.
    /// </para>
    /// </remarks>
    public sealed class BattleFrameSnapshot
    {
        private readonly RenderEntity[] _current = new RenderEntity[BattleConstants.MaxRenderEntities];
        private readonly RenderEntity[] _previous = new RenderEntity[BattleConstants.MaxRenderEntities];
        private readonly LoadoutCooldown[] _cooldowns = new LoadoutCooldown[BattleConstants.MaxLoadoutEntries];

        /// <summary>Slot index → position in <see cref="_previous"/>, plus one; 0 = absent.</summary>
        private readonly int[] _previousSlot = new int[BattleConstants.MaxRenderEntities + 1];

        /// <summary>How many entities the current tick published.</summary>
        public int CurrentCount { get; private set; }

        /// <summary>How many entities the previous tick published.</summary>
        public int PreviousCount { get; private set; }

        /// <summary>The tick this snapshot describes.</summary>
        public long Tick { get; private set; }

        /// <summary>The HUD values at that tick (by value; never interpolated).</summary>
        public HudState Hud { get; private set; }

        /// <summary>True once a frame has been copied (false before the first tick).</summary>
        public bool HasFrame { get; private set; }

        /// <summary>How many loadout entries the current tick's cooldown list holds.</summary>
        public int CooldownCount { get; private set; }

        /// <summary>
        /// The loadout's casting state at this tick (Stage 08b's <see cref="IBattleFrame.Cooldowns"/>),
        /// copied the same way the entities are: it is a view over the frame's own memory and dies on
        /// the next <c>Step</c>.
        /// </summary>
        /// <remarks>
        /// Copied rather than kept as a span for the reason the entities are (ADR 0012): the mini bag
        /// reads it on the render frame the tick belongs to, and the next tick reuses the slot. It is a
        /// different list from <see cref="Current"/>: a loadout entry is not an entity, and a cooldown is
        /// a count rather than a position, so it is never interpolated.
        /// </remarks>
        public ReadOnlySpan<LoadoutCooldown> Cooldowns =>
            new ReadOnlySpan<LoadoutCooldown>(_cooldowns, 0, CooldownCount);

        /// <summary>The current tick's entities.</summary>
        public ReadOnlySpan<RenderEntity> Current =>
            new ReadOnlySpan<RenderEntity>(_current, 0, CurrentCount);

        /// <summary>The previous tick's entities (empty on the session's first frame).</summary>
        public ReadOnlySpan<RenderEntity> Previous =>
            new ReadOnlySpan<RenderEntity>(_previous, 0, PreviousCount);

        /// <summary>
        /// Copies <paramref name="frame"/>. Call this immediately after the last
        /// <c>Step</c> of a frame and before any other Step/Submit.
        /// </summary>
        public void CopyFrom(IBattleFrame frame)
        {
            if (frame is null)
            {
                return;
            }

            Tick = frame.Tick;
            Hud = frame.Hud;

            ReadOnlySpan<RenderEntity> current = frame.Entities;
            ReadOnlySpan<RenderEntity> previous = frame.PreviousEntities;

            int currentCount = Clamp(current.Length);
            for (int i = 0; i < currentCount; i++)
            {
                _current[i] = current[i];
            }

            CurrentCount = currentCount;

            Array.Clear(_previousSlot, 0, _previousSlot.Length);

            int previousCount = Clamp(previous.Length);
            for (int i = 0; i < previousCount; i++)
            {
                RenderEntity entity = previous[i];
                _previous[i] = entity;

                int slot = entity.Entity.Index;
                if (slot > 0 && slot <= BattleConstants.MaxRenderEntities)
                {
                    _previousSlot[slot] = i + 1;
                }
            }

            PreviousCount = previousCount;

            ReadOnlySpan<LoadoutCooldown> cooldowns = frame.Cooldowns;
            int cooldownCount = ClampCooldowns(cooldowns.Length);
            for (int i = 0; i < cooldownCount; i++)
            {
                _cooldowns[i] = cooldowns[i];
            }

            CooldownCount = cooldownCount;
            HasFrame = true;
        }

        /// <summary>
        /// The same entity as it was at the previous tick, when it existed then. False on
        /// the tick an entity spawned, which is what tells the renderer to start it at its
        /// current position instead of sliding it in from a recycled slot's last owner.
        /// </summary>
        public bool TryGetPrevious(in EntityRef entity, out RenderEntity previous)
        {
            int slot = entity.Index;
            if (slot > 0 && slot <= BattleConstants.MaxRenderEntities)
            {
                int position = _previousSlot[slot];
                if (position > 0 && position <= PreviousCount)
                {
                    RenderEntity candidate = _previous[position - 1];
                    if (candidate.Entity == entity)
                    {
                        previous = candidate;
                        return true;
                    }
                }
            }

            previous = default;
            return false;
        }

        private static int Clamp(int count) =>
            count > BattleConstants.MaxRenderEntities ? BattleConstants.MaxRenderEntities : count;

        private static int ClampCooldowns(int count) =>
            count > BattleConstants.MaxLoadoutEntries ? BattleConstants.MaxLoadoutEntries : count;
    }
}
