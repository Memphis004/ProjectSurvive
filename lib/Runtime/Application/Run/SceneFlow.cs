using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>
    /// Drives the run's Camp → Battle → Camp loop and says which scene that is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>only</b> place a battle session is created. The factory arrives through
    /// the constructor (port / DI), so this class never names
    /// <c>FakeBattleSession</c> — the fake is what the composition root injects
    /// today and the EcsLite simulation is what Stage 07 injects afterwards, with no
    /// change here. That is also what makes the acceptance test \"replay ชั้นด้วย
    /// BattleReplay แล้วได้ ComputeStateHash เดิม\" meaningful: the same recording runs
    /// through either implementation.
    /// </para>
    /// <para>
    /// While a floor runs, this class records a <see cref="BattleReplay"/> for it and
    /// attaches it to the run's log, so the run-level replay can reconstruct the
    /// floor's result instead of trusting a stored one.
    /// </para>
    /// </remarks>
    public sealed class SceneFlow
    {
        private readonly RunSession _run;
        private readonly IBattleSessionFactory _factory;
        private IBattleSession? _battle;
        private BattleReplay? _replay;

        /// <summary>Creates the flow for a run and a battle factory.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public SceneFlow(RunSession run, IBattleSessionFactory factory)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>The run being driven.</summary>
        public RunSession Run => _run;

        /// <summary>The current flow phase.</summary>
        public RunFlowState State => _run.FlowState;

        /// <summary>The scene that renders <see cref="State"/>.</summary>
        public SceneId CurrentScene => SceneFlowMap.SceneOf(_run.FlowState);

        /// <summary>The floor's battle session, or null when no floor is running.</summary>
        public IBattleSession? Battle => _battle;

        /// <summary>The recording of the current floor, or null when no floor is running.</summary>
        public BattleReplay? CurrentBattleReplay => _replay;

        /// <summary>True while a floor is in progress.</summary>
        public bool IsInBattle => _battle is not null;

        /// <summary>
        /// Leaves Camp for the next floor: the run validates the bag and locks it, then
        /// this creates the session through the factory.
        /// </summary>
        /// <returns>The session, or the run's refusal (e.g. <see cref="RunError.NoArcaneWeapon"/>).</returns>
        public Result<IBattleSession> EnterBattle()
        {
            if (_battle is not null)
            {
                return Result.Fail<IBattleSession>(RunError.BattleRunning, ErrorKind.InvalidState);
            }

            Result<BattleSetup> started = _run.StartFloor();
            if (started.IsFailure)
            {
                return Result.Fail<IBattleSession>(started.Error);
            }

            _battle = _factory.Create(started.Value);
            _replay = new BattleReplay(started.Value);
            return Result.Ok(_battle);
        }

        /// <summary>Advances the current floor by exactly one fixed tick, recording the input.</summary>
        public Result StepBattle(in PlayerInputFrame input)
        {
            if (_battle is null || _replay is null)
            {
                return Result.Fail(RunError.NotInBattle, ErrorKind.InvalidState);
            }

            _battle.Step(input);
            _replay.RecordStep(input);
            return Result.Ok();
        }

        /// <summary>Submits a command to the current floor, recording it at its tick.</summary>
        public CommandResult SubmitBattleCommand(in BattleCommand command)
        {
            if (_battle is null || _replay is null)
            {
                return CommandResult.RejectedWrongPhase;
            }

            CommandResult submitted = _battle.Submit(command);
            if (submitted == CommandResult.Ok)
            {
                _replay.RecordCommand(_battle.Tick, command);
            }

            return submitted;
        }

        /// <summary>
        /// Settles the finished floor into the run: the battle's result is applied
        /// (HP, bia, consumables, the clear heal) and its recording is attached to the
        /// run log.
        /// </summary>
        /// <returns>
        /// The settlement, or <see cref="RunError.BattleRunning"/> while the floor is
        /// still in progress, or <see cref="RunError.NotInBattle"/> when no floor is
        /// running.
        /// </returns>
        public Result<FloorSettlement> SettleBattle()
        {
            if (_battle is null || _replay is null)
            {
                return Result.Fail<FloorSettlement>(RunError.NotInBattle, ErrorKind.InvalidState);
            }

            if (_battle.Phase == BattlePhase.Running)
            {
                return Result.Fail<FloorSettlement>(RunError.BattleRunning, ErrorKind.InvalidState);
            }

            if (!_battle.TryGetResult(out BattleResult result))
            {
                return Result.Fail<FloorSettlement>(RunError.BattleRunning, ErrorKind.InvalidState);
            }

            _run.RecordBattle(_replay);
            Result<FloorSettlement> settled = _run.ApplyBattleResult(result);

            _battle = null;
            _replay = null;
            return settled;
        }

        /// <summary>
        /// Runs a floor to its end with an idle player and settles it. The scripted /
        /// headless path (a bot of Stage 07 replaces the idle input); Presentation
        /// drives <see cref="StepBattle"/> itself instead.
        /// </summary>
        public Result<FloorSettlement> RunFloorToEnd(int maxSteps = 100_000)
        {
            if (_battle is null)
            {
                return Result.Fail<FloorSettlement>(RunError.NotInBattle, ErrorKind.InvalidState);
            }

            int steps = 0;
            while (_battle.Phase == BattlePhase.Running)
            {
                if (steps++ >= maxSteps)
                {
                    return Result.Fail<FloorSettlement>(RunError.BattleRunning, ErrorKind.InvalidState);
                }

                _battle.Step(PlayerInputFrame.Idle);
                _replay!.RecordStep(PlayerInputFrame.Idle);
            }

            return SettleBattle();
        }
    }
}
