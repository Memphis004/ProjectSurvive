using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// A recording of one battle that can be replayed through any
    /// <see cref="IBattleSessionFactory"/> and compared by
    /// <see cref="IBattleSession.ComputeStateHash"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the <b>battle</b> half of replay only: a floor's setup, one input
    /// per <see cref="IBattleSession.Step"/>, and the ticked commands. It is
    /// deliberately smaller than the run-level <c>ReplayLog</c> of Stage 06, which
    /// can embed one <see cref="BattleReplay"/> per floor alongside the shop
    /// decisions and the run seed — no duplication of either.
    /// </para>
    /// <para>
    /// Recording appends to lists and is a development / verification path, never
    /// a per-frame hot path, so it may allocate (unlike <see cref="Step"/>).
    /// </para>
    /// <para>
    /// Replay ordering: for the step that runs while <c>Tick == T</c>, every
    /// command recorded at tick <c>T</c> is submitted first, then the step. Record
    /// a command <em>before</em> the step of its tick to reproduce the original
    /// order.
    /// </para>
    /// </remarks>
    public sealed class BattleReplay
    {
        private readonly List<PlayerInputFrame> _inputs = new List<PlayerInputFrame>();
        private readonly List<ReplayCommand> _commands = new List<ReplayCommand>();

        /// <summary>Creates a replay for a battle setup.</summary>
        public BattleReplay(BattleSetup setup)
        {
            Setup = setup;
        }

        /// <summary>The setup the battle started from.</summary>
        public BattleSetup Setup { get; }

        /// <summary>The inputs, one per step, in order.</summary>
        public IReadOnlyList<PlayerInputFrame> Inputs => _inputs;

        /// <summary>How many commands were recorded.</summary>
        public int CommandCount => _commands.Count;

        /// <summary>Records the input for the next step.</summary>
        public void RecordStep(in PlayerInputFrame input)
        {
            _inputs.Add(input);
        }

        /// <summary>Records a command submitted at <paramref name="tick"/>.</summary>
        public void RecordCommand(long tick, in BattleCommand command)
        {
            _commands.Add(new ReplayCommand(tick, command));
        }

        /// <summary>Forgets every recorded step and command (the setup is kept).</summary>
        public void Clear()
        {
            _inputs.Clear();
            _commands.Clear();
        }

        /// <summary>
        /// Replays the recording through <paramref name="factory"/> and returns the
        /// resulting session (already finished when the original finished).
        /// </summary>
        public IBattleSession Replay(IBattleSessionFactory factory)
        {
            IBattleSession session = factory.Create(Setup);
            int commandIndex = 0;

            for (int i = 0; i < _inputs.Count && session.Phase == BattlePhase.Running; i++)
            {
                while (commandIndex < _commands.Count && _commands[commandIndex].Tick == session.Tick)
                {
                    session.Submit(_commands[commandIndex].Command);
                    commandIndex++;
                }

                session.Step(_inputs[i]);
            }

            // Commands recorded for the final tick, after the last step was supplied.
            while (session.Phase == BattlePhase.Running
                   && commandIndex < _commands.Count
                   && _commands[commandIndex].Tick == session.Tick)
            {
                session.Submit(_commands[commandIndex].Command);
                commandIndex++;
            }

            return session;
        }

        /// <summary>Replays the recording and returns the final state hash.</summary>
        public uint ReplayHash(IBattleSessionFactory factory) => Replay(factory).ComputeStateHash();

        /// <summary>A command recorded at a specific tick.</summary>
        public readonly struct ReplayCommand
        {
            /// <summary>Creates a ticked command.</summary>
            public ReplayCommand(long tick, in BattleCommand command)
            {
                Tick = tick;
                Command = command;
            }

            /// <summary>The tick the command was submitted at.</summary>
            public long Tick { get; }

            /// <summary>The command.</summary>
            public BattleCommand Command { get; }
        }
    }
}
