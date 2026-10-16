using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>Optional tags on a <see cref="LoadoutCooldown"/>.</summary>
    [Flags]
    public enum LoadoutCooldownFlags : byte
    {
        /// <summary>No tags.</summary>
        None = 0,

        /// <summary>
        /// The entry's cooldown has elapsed but the player's MP is below the cast's cost, so it
        /// cannot cast even though it is ready. It says nothing about whether a target exists or
        /// whether another weapon outranks it — those are the simulation's business, not the
        /// frame's.
        /// </summary>
        ManaShort = 1,
    }

    /// <summary>
    /// The casting state of one loadout entry — a weapon or a familiar — in the frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Added in Stage 08b. The HUD has to show, on each piece laying on the board, how far its
    /// cooldown has run down; the contract had no such data, and the alternative — letting a View
    /// run its own clock and guess — is how a HUD starts disagreeing with the fight (it cannot know
    /// the resolved cooldown, a paused floor, or a cast refused for want of MP).
    /// </para>
    /// <para>
    /// This is a *presentation* view of simulation state, produced once per tick: nothing reads it
    /// back, and it never decides a rule. It carries ticks rather than seconds because the
    /// simulation's own clock is ticks at
    /// <see cref="BattleConstants.TickRate"/>; a view that wants seconds divides by the rate, and
    /// one that wants a progress bar divides by <see cref="PeriodTicks"/>.
    /// </para>
    /// </remarks>
    public readonly struct LoadoutCooldown
    {
        /// <summary>
        /// <see cref="InstanceId"/> for an entry that did not come from a board piece (a hand-built
        /// loadout, or a test). Instance ids start at 1, so <c>0</c> never names a real piece.
        /// </summary>
        public const int NoInstance = 0;

        /// <summary>Creates one entry's casting state.</summary>
        public LoadoutCooldown(
            int instanceId,
            int remainingTicks,
            int periodTicks,
            LoadoutCooldownFlags flags = LoadoutCooldownFlags.None)
        {
            InstanceId = instanceId;
            RemainingTicks = remainingTicks < 0 ? 0 : remainingTicks;
            PeriodTicks = periodTicks < 1 ? 1 : periodTicks;
            Flags = flags;
        }

        /// <summary>
        /// Board instance id of the piece this entry came from (<c>ItemInstance.InstanceId</c>), or
        /// <see cref="NoInstance"/> when it came from no piece. This is the key a HUD binds a board
        /// cell to.
        /// </summary>
        public int InstanceId { get; }

        /// <summary>Ticks until this entry may cast again; <c>0</c> means the cooldown has elapsed.</summary>
        public int RemainingTicks { get; }

        /// <summary>Length of this entry's cooldown in ticks, at least 1.</summary>
        public int PeriodTicks { get; }

        /// <summary>Optional tags.</summary>
        public LoadoutCooldownFlags Flags { get; }

        /// <summary>True once the cooldown has elapsed (whether or not anything allows the cast).</summary>
        public bool IsReady => RemainingTicks <= 0;

        /// <summary>True when this entry is ready and only MP is holding the cast back.</summary>
        public bool ReadyButManaShort => (Flags & LoadoutCooldownFlags.ManaShort) != 0;
    }
}
