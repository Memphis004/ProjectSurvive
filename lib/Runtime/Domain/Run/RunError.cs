using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The stable, machine-readable failure codes of a run command and of the
    /// wallet (AGENTS rule 7).
    /// </summary>
    /// <remarks>
    /// Player-facing text comes from localization, keyed <c>error.&lt;code&gt;</c>.
    /// Like the <c>inventory.*</c> codes of Stage 05 these are API: Stage 09 maps
    /// them to Thai strings, and renaming one without adding the keys leaves the
    /// player with a blank message.
    /// </remarks>
    public static class RunError
    {
        /// <summary>The command arrived in a phase that does not accept it (\"transition ผิดลำดับ\").</summary>
        public const string WrongState = "run.wrong_state";

        /// <summary>
        /// StartFloor was refused because the resolved loadout has no usable arcane
        /// weapon (\"ออกเดินทางไม่ได้ถ้าไม่มีอาวุธอาคมบนช่องใช้งานอย่างน้อย 1 ชิ้น\").
        /// This is the reason code the UI shows.
        /// </summary>
        public const string NoArcaneWeapon = "run.no_arcane_weapon";

        /// <summary>
        /// The floor cursor is past the end of the floor table, so there is nothing left to
        /// start. A <b>precondition</b> guard on the cursor, not a refusal a player can reach:
        /// the phase machine only offers a departure while a floor is left, which the
        /// invariant test pins down.
        /// </summary>
        public const string NoFloorLeft = "run.no_floor_left";

        /// <summary>
        /// The config has no floors at all (a data bug, not a player mistake). Reachable only
        /// through a degenerate floor table; every table generated from `data/` has five rows.
        /// </summary>
        public const string UnknownFloor = "run.unknown_floor";

        /// <summary>A battle result was offered while no floor was in progress.</summary>
        public const string NotInBattle = "run.not_in_battle";

        /// <summary>The floor's battle is still running, so it has no result to settle.</summary>
        public const string BattleRunning = "run.battle_running";

        /// <summary>A run command named a piece the run does not own.</summary>
        public const string UnknownPiece = "run.unknown_piece";
    }

    /// <summary>The stable failure codes of <see cref="Wallet"/>.</summary>
    public static class WalletError
    {
        /// <summary>The wallet does not hold enough bia for the spend.</summary>
        public const string InsufficientBia = "wallet.insufficient_bia";
    }
}
