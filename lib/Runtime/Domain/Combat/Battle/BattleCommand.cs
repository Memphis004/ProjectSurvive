namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// An out-of-band instruction for a running battle (surrender, dev cheats).
    /// </summary>
    /// <remarks>
    /// Commands are separate from <see cref="PlayerInputFrame"/> because they are
    /// rare events, not per-tick data, and because a dev command must be
    /// explicitly allowed by <see cref="BattleSetup.DevCheatsAllowed"/> rather
    /// than always available. Movement never goes through here.
    /// </remarks>
    public readonly struct BattleCommand
    {
        /// <summary>Creates a command with an argument.</summary>
        public BattleCommand(BattleCommandKind kind, int arg = 0)
        {
            Kind = kind;
            Arg = arg;
        }

        /// <summary>What the command does.</summary>
        public BattleCommandKind Kind { get; }

        /// <summary>Command-specific argument; 0 when unused.</summary>
        public int Arg { get; }

        /// <summary>True for the commands behind <see cref="BattleSetup.DevCheatsAllowed"/>.</summary>
        public bool IsDevCommand => Kind != BattleCommandKind.Surrender;

        /// <summary>A <see cref="BattleCommandKind.Surrender"/> command.</summary>
        public static BattleCommand Surrender() => new BattleCommand(BattleCommandKind.Surrender);

        /// <summary>A <see cref="BattleCommandKind.DevKillAll"/> command.</summary>
        public static BattleCommand DevKillAll() => new BattleCommand(BattleCommandKind.DevKillAll);

        /// <summary>A <see cref="BattleCommandKind.DevWinNow"/> command.</summary>
        public static BattleCommand DevWinNow() => new BattleCommand(BattleCommandKind.DevWinNow);

        /// <summary>A <see cref="BattleCommandKind.DevLoseNow"/> command.</summary>
        public static BattleCommand DevLoseNow() => new BattleCommand(BattleCommandKind.DevLoseNow);

        /// <summary>A <see cref="BattleCommandKind.DevGodMode"/> command (arg 1 = on, 0 = off).</summary>
        public static BattleCommand DevGodMode(bool enabled) =>
            new BattleCommand(BattleCommandKind.DevGodMode, enabled ? 1 : 0);

        /// <summary>A <see cref="BattleCommandKind.DevAddBia"/> command for a positive amount.</summary>
        public static BattleCommand DevAddBia(int amount) => new BattleCommand(BattleCommandKind.DevAddBia, amount);

        /// <summary>A <see cref="BattleCommandKind.DevSpawnGhost"/> command (no argument).</summary>
        public static BattleCommand DevSpawnGhost() => new BattleCommand(BattleCommandKind.DevSpawnGhost);
    }
}
