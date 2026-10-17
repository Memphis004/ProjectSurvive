using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>
    /// A run as a seed plus the commands that were issued, in order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the development and verification path (AGENTS rule 6): applying the same commands
    /// to a fresh <see cref="RunSession"/> on the same seed must reproduce the same
    /// <see cref="RunSession.ComputeStateHash"/>. It is not the save format — Stage 12 owns that —
    /// and it may allocate, unlike a per-tick path.
    /// </para>
    /// <para>
    /// A divergence throws <see cref="InvalidOperationException"/>: a log that no longer
    /// reproduces its own run is a bug in the recorder, not a player-facing failure, so it must
    /// not be reported as one.
    /// </para>
    /// </remarks>
    public sealed class ReplayLog
    {
        private readonly List<RunCommand> _commands = new List<RunCommand>();

        /// <summary>Creates an empty log for a run seed.</summary>
        public ReplayLog(uint seed) => Seed = seed;

        /// <summary>The run seed every stream was derived from.</summary>
        public uint Seed { get; }

        /// <summary>The commands, in the order they were accepted.</summary>
        public IReadOnlyList<RunCommand> Commands => _commands;

        /// <summary>How many commands were recorded.</summary>
        public int Count => _commands.Count;

        /// <summary>Appends a command. Called by the run as it accepts one.</summary>
        internal void Record(RunCommandKind kind) => _commands.Add(new RunCommand(_commands.Count, kind));

        /// <summary>Rebuilds the run by applying every recorded command to a fresh session.</summary>
        /// <returns>The replayed run; compare its state hash with the original's.</returns>
        /// <exception cref="InvalidOperationException">The log no longer reproduces its own run.</exception>
        public RunSession Replay()
        {
            // `recording: false`: the replayed run must not grow a second copy of this log.
            var run = new RunSession(Seed, recording: false);

            for (int i = 0; i < _commands.Count; i++)
            {
                RunCommand command = _commands[i];
                Result result = command.Kind switch
                {
                    RunCommandKind.StartRun => run.Start(),
                    RunCommandKind.BeginEncounter => run.BeginEncounter(),
                    RunCommandKind.WinEncounter => run.EndEncounter(EncounterOutcome.Won),
                    RunCommandKind.LoseEncounter => run.EndEncounter(EncounterOutcome.Lost),
                    RunCommandKind.EndRun => run.EndRun(),
                    RunCommandKind.Reset => run.Reset(),
                    _ => throw new InvalidOperationException("Unhandled run command: " + command.Kind),
                };

                if (result.IsFailure)
                {
                    throw new InvalidOperationException(
                        "Replay diverged at command " + i + " (" + command + "): " + result.Error);
                }
            }

            return run;
        }
    }
}
