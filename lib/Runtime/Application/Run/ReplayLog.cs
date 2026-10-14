using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>
    /// The whole run as a seed plus the commands that were issued (GDD: \"ReplayLog
    /// (command + seed)\"), with one <see cref="BattleReplay"/> per floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The battle half is <b>not</b> reimplemented here: a floor's
    /// <see cref="BattleReplay"/> (Stage 02b) already carries its setup, one input
    /// per tick and its ticked commands, and replays them through any
    /// <see cref="IBattleSessionFactory"/> to the same
    /// <see cref="IBattleSession.ComputeStateHash"/>. This log only decides
    /// <i>which</i> battle belongs to which floor and in what order.
    /// </para>
    /// <para>
    /// A replay is therefore: a fresh <see cref="RunSession"/> on the same seed, the
    /// recorded run commands applied in order, and each floor settled from its
    /// replayed battle. Anything that diverges throws — a log that no longer
    /// reproduces its own run is a bug in the recorder, not a player-facing failure.
    /// </para>
    /// <para>
    /// This is a development and verification path, so it may allocate (unlike
    /// <c>IBattleSession.Step</c>).
    /// </para>
    /// </remarks>
    public sealed class ReplayLog
    {
        private readonly List<RunCommand> _commands = new List<RunCommand>();
        private readonly List<BattleReplay> _battles = new List<BattleReplay>();

        /// <summary>Creates an empty log for a run seed.</summary>
        public ReplayLog(uint seed)
        {
            Seed = seed;
        }

        /// <summary>The run seed every stream was derived from.</summary>
        public uint Seed { get; }

        /// <summary>The run commands, in the order they were accepted.</summary>
        public IReadOnlyList<RunCommand> Commands => _commands;

        /// <summary>One battle recording per started floor, in floor order.</summary>
        public IReadOnlyList<BattleReplay> Battles => _battles;

        /// <summary>How many run commands were recorded.</summary>
        public int CommandCount => _commands.Count;

        /// <summary>How many floor battles were recorded.</summary>
        public int BattleCount => _battles.Count;

        /// <summary>Appends a command. Called by the run as it accepts one.</summary>
        internal void Record(in RunCommand command) => _commands.Add(command);

        /// <summary>Appends a floor's battle recording.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="replay"/> is null.</exception>
        internal void RecordBattle(BattleReplay replay) =>
            _battles.Add(replay ?? throw new ArgumentNullException(nameof(replay)));

        /// <summary>
        /// Rebuilds the run by applying every recorded command to a fresh session and
        /// replaying each floor's battle through <paramref name="factory"/>.
        /// </summary>
        /// <returns>The replayed run; its <c>ComputeStateHash()</c> must equal the original's.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="InvalidOperationException">The log no longer reproduces its own run.</exception>
        public RunSession Replay(IGameConfig config, IBackpackSnapshotCodec codec, IBattleSessionFactory factory)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (codec is null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            // `recording: false`: the replayed run must not grow a second copy of the log.
            var run = new RunSession(config, codec, Seed, recording: false);
            int battleIndex = 0;

            for (int i = 0; i < _commands.Count; i++)
            {
                RunCommand command = _commands[i];

                switch (command.Kind)
                {
                    case RunCommandKind.StartRun:
                        Require(run.StartRun(), command, i);
                        break;

                    case RunCommandKind.Purchase:
                        Require(run.Purchase(command.Index), command, i);
                        break;

                    case RunCommandKind.Sell:
                        Require(run.Sell(command.Piece), command, i);
                        break;

                    case RunCommandKind.SetSlotLocked:
                        Require(run.SetSlotLocked(command.Index, command.Flag), command, i);
                        break;

                    case RunCommandKind.RefreshShop:
                        Require(run.RefreshShop(), command, i);
                        break;

                    case RunCommandKind.MovePiece:
                        Require(
                            run.MovePiece(command.Piece, new CellOffset(command.X, command.Y), command.Rotation),
                            command, i);
                        break;

                    case RunCommandKind.RotatePiece:
                        Require(run.RotatePiece(command.Piece), command, i);
                        break;

                    case RunCommandKind.TransferToChest:
                        Require(run.TransferToChest(command.Piece), command, i);
                        break;

                    case RunCommandKind.MergeItems:
                        Require(run.MergeItems(command.Piece, command.Second), command, i);
                        break;

                    case RunCommandKind.Undo:
                        Require(run.Undo(), command, i);
                        break;

                    case RunCommandKind.Reset:
                        Require(run.Reset(), command, i);
                        break;

                    case RunCommandKind.StartFloor:
                        Require(run.StartFloor(), command, i);
                        if (battleIndex >= _battles.Count)
                        {
                            throw new InvalidOperationException(
                                "ReplayLog has a StartFloor at command " + i + " but only " + _battles.Count
                                + " battle recordings.");
                        }

                        Require(SettleReplayedBattle(run, _battles[battleIndex], factory), command, i);
                        battleIndex++;
                        break;

                    default:
                        throw new InvalidOperationException("Unhandled run command: " + command.Kind);
                }
            }

            return run;
        }

        /// <summary>Replays the log and returns the resulting run state hash.</summary>
        public uint ReplayStateHash(IGameConfig config, IBackpackSnapshotCodec codec, IBattleSessionFactory factory) =>
            Replay(config, codec, factory).ComputeStateHash();

        /// <summary>
        /// Replays one floor's battle through <paramref name="factory"/> and settles it
        /// into the run — the same two steps <c>SceneFlow</c> performs live.
        /// </summary>
        private static Result SettleReplayedBattle(RunSession run, BattleReplay replay, IBattleSessionFactory factory)
        {
            IBattleSession battle = replay.Replay(factory);
            if (!battle.TryGetResult(out BattleResult result))
            {
                return Result.Fail(RunError.BattleRunning, ErrorKind.InvalidState);
            }

            Result<FloorSettlement> settled = run.ApplyBattleResult(result);
            return settled.IsFailure ? Result.Fail(settled.Error) : Result.Ok();
        }

        private static void Require(Result result, in RunCommand command, int index)
        {
            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    "Replay diverged at command " + index + " (" + command + "): " + result.Error);
            }
        }

        private static void Require<T>(Result<T> result, in RunCommand command, int index)
        {
            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    "Replay diverged at command " + index + " (" + command + "): " + result.Error);
            }
        }
    }
}
