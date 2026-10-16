using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using R3;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The dev menu's logic: whether it may show at all, which <see cref="BattleCommand"/> each
    /// control stands for, and the developer-facing status line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 10c's DevMenu (\"อมตะ, เพิ่มเบี้ย, ข้ามชั้น, spawn ผี\") is a development tool. Its whole
    /// vocabulary is <see cref="BattleCommand"/>, so every control goes through the same
    /// <c>IBattleSession.Submit</c> a surrender uses, lands in the same command switch of the
    /// running floor, and is refused by the session with <see cref="CommandResult.RejectedNotAllowed"/>
    /// when the floor was not started with <c>BattleSetup.DevCheatsAllowed</c>. That is the second
    /// gate: <see cref="Visible"/> hides the menu, and the floor refuses the command anyway.
    /// </para>
    /// <para>
    /// <b>Why its strings are not in the localization table.</b> Every string here is developer
    /// diagnostics — the name of a command and the result the session returned — not player-facing
    /// content, and the menu does not exist in a release build at all. The same reasoning already
    /// excludes the title screen's diagnostics line (\"build …, config …\") from the table; see
    /// <c>TitlePresenter</c>. Player-facing text stays in the table only (AGENTS rule 5).
    /// </para>
    /// <para>
    /// <b>Only the intent is computed here.</b> A control asks for a command and the caller submits
    /// it; the result comes back through <see cref="NoteResult"/>, which is what moves the state.
    /// The god-mode toggle is the one place that matters: pressing it asks for the opposite of the
    /// state that is <i>actually in force</i>, and that state only moves when the floor accepted the
    /// command — so a press a release build refuses leaves the label telling the truth.
    /// </para>
    /// </remarks>
    public sealed class DevMenuPresenter : IDisposable
    {
        private readonly IGameConfig _config;
        private bool _godModeRequested;

        /// <summary>
        /// Whether the menu may be shown. On in a Development build and in the Editor, off in a
        /// release build — the decision is <see cref="DevCheats.Allowed"/>, made once at startup by
        /// the shell.
        /// </summary>
        public ReactiveProperty<bool> Visible { get; } = new ReactiveProperty<bool>(false);

        /// <summary>The god mode the floor last accepted.</summary>
        public ReactiveProperty<bool> GodMode { get; } = new ReactiveProperty<bool>(false);

        /// <summary>The last command's outcome, in developer terms.</summary>
        public ReactiveProperty<string> Status { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Creates the presenter.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public DevMenuPresenter(IGameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Bia one press of \"add bia\" grants.
        /// </summary>
        /// <remarks>
        /// Read from the economy table (<c>economy.start_bia</c>) rather than written here: every
        /// balance number lives in <c>data/</c> (AGENTS rule 5), including the ones only a developer
        /// sees. One press grants one starting purse.
        /// </remarks>
        public int BiaGrant => _config.Economy.StartBia;

        /// <summary>Reads the shell's decision and puts the menu in its initial state.</summary>
        public void Begin()
        {
            Visible.Value = DevCheats.Allowed;
            GodMode.Value = false;
            Status.Value = Visible.Value ? DevMenuText.Enabled : DevMenuText.Disabled;
        }

        /// <summary>The next god-mode command: the opposite of the state in force.</summary>
        public BattleCommand NextGodMode()
        {
            _godModeRequested = !GodMode.Value;
            return BattleCommand.DevGodMode(_godModeRequested);
        }

        /// <summary>An \"add bia\" command for <see cref="BiaGrant"/>.</summary>
        public BattleCommand AddBia() => BattleCommand.DevAddBia(BiaGrant);

        /// <summary>A \"spawn one more ghost\" command.</summary>
        public BattleCommand SpawnGhost() => BattleCommand.DevSpawnGhost();

        /// <summary>A \"kill every ghost\" command.</summary>
        public BattleCommand KillAll() => BattleCommand.DevKillAll();

        /// <summary>A \"win this floor now\" command (the skip-floor control).</summary>
        public BattleCommand WinNow() => BattleCommand.DevWinNow();

        /// <summary>A \"lose this floor now\" command.</summary>
        public BattleCommand LoseNow() => BattleCommand.DevLoseNow();

        /// <summary>
        /// Records what the session answered, and moves the state the answer justifies.
        /// </summary>
        /// <param name="kind">The command that was submitted.</param>
        /// <param name="result">The session's own result, passed through untouched.</param>
        public void NoteResult(BattleCommandKind kind, CommandResult result)
        {
            if (kind == BattleCommandKind.DevGodMode && result == CommandResult.Ok)
            {
                // Accepted: the toggle now describes what the floor is really doing.
                GodMode.Value = _godModeRequested;
            }

            Status.Value = DevMenuText.Result(kind, result);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Visible.Dispose();
            GodMode.Dispose();
            Status.Dispose();
        }
    }

    /// <summary>
    /// The dev menu's own strings. Not in the localization table on purpose — see
    /// <see cref="DevMenuPresenter"/>'s remarks: these are developer diagnostics in a menu that only
    /// exists outside a release build.
    /// </summary>
    public static class DevMenuText
    {
        /// <summary>The panel's heading, naming it as a test tool.</summary>
        public const string Title = "DEV MENU (test only)";

        /// <summary>The god-mode control's caption.</summary>
        public const string GodMode = "อมตะ / god mode";

        /// <summary>The add-bia control's caption.</summary>
        public const string AddBia = "เพิ่มเบี้ย / add bia";

        /// <summary>The spawn-a-ghost control's caption.</summary>
        public const string SpawnGhost = "เพิ่มผี / spawn ghost";

        /// <summary>The kill-every-ghost control's caption.</summary>
        public const string KillAll = "ฆ่าผีทั้งหมด / kill all";

        /// <summary>The skip-floor control's caption.</summary>
        public const string WinNow = "ข้ามชั้น (ชนะ) / win now";

        /// <summary>The lose-now control's caption.</summary>
        public const string LoseNow = "แพ้ทันที / lose now";

        /// <summary>Status when the menu is available.</summary>
        public const string Enabled = "dev: on (Development build or Editor)";

        /// <summary>Status when the build does not allow cheats.</summary>
        public const string Disabled = "dev: off (release build — DevCheats.Allowed is false)";

        /// <summary>The toggle's caption while the floor is immortal.</summary>
        public const string GodModeOn = "อมตะ: เปิด / on";

        /// <summary>The toggle's caption while the floor is not.</summary>
        public const string GodModeOff = "อมตะ: ปิด / off";

        /// <summary>One status line: which command, and what the session answered.</summary>
        public static string Result(BattleCommandKind kind, CommandResult result) =>
            "dev: " + kind + " -> " + result;
    }
}
