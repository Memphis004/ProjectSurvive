using System;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Battle
{
    /// <summary>
    /// Plays one floor of the run in the Battle scene, and settles it back into the run when it
    /// ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin. Everything it composes is either the scene's wiring (authored by
    /// <c>ProjectSetup</c> through the Editor API, AGENTS rule 11) or a service resolved from
    /// <see cref="BattleSceneScope"/>. The result is the one place that knows both halves: the
    /// presentation's <see cref="BattleSimulationRunner"/> and the composition root's real-vs-fake
    /// choice.
    /// </para>
    /// <para>
    /// <b>The floor comes from the run (Stage 10a).</b> The camp performed the departure — the
    /// phase check, the bag lock and the loadout the battle runs — so the setup is waiting in
    /// <see cref="RunSession.PendingSetup"/> and this scene simply starts it. Nothing here rebuilds
    /// a run, which is what makes the same scene serve floor 1 through floor 5: the floor id, the
    /// HP and the purse all come from the run, and the only thing that changes between floors is
    /// which floor the run is on.
    /// </para>
    /// <para>
    /// <b>And it settles it.</b> When the floor ends this hands the result to
    /// <see cref="RunSession.ApplyBattleResult"/> — the one command that moves HP, bia and the
    /// clear-heal into the run — and then leaves for the camp. <c>BattleSetup.CurrentHp</c> in and
    /// <c>BattleResult.FinalHp</c> out is the whole of the HP contract, and the 15% heal belongs to
    /// the run, not to the battle (see <see cref="BattleSetup"/>'s remarks).
    /// </para>
    /// <para>
    /// It resolves services with explicit calls rather than field injection, because a
    /// <c>MonoBehaviour</c>'s injection needs a reflected constructor path and IL2CPP strips what
    /// no IL call site reaches (<c>llmwiki/gotchas/il2cpp-di-and-reflection.md</c>).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleSceneDirector : MonoBehaviour
    {
        [SerializeField] private BattleSceneScope _scope;
        [SerializeField] private BattleSimulationRunner _runner;

        private RunSession _run;
        private SceneLoader _loader;
        private bool _settled;
        private bool _standalone;
        private SceneId? _leavingFor;
        private bool _leaveRefusedLogged;

        /// <summary>The runner this scene drives (null until the scene has started).</summary>
        public BattleSimulationRunner Runner => _runner;

        /// <summary>The run this floor belongs to (null until the scene has started).</summary>
        public RunSession Run => _run;

        /// <summary>Why the last start refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>
        /// True when this scene had to start the run and depart on its own, because it was opened
        /// without a camp (<c>?scene=Battle</c>). False on the normal path, where the camp departed.
        /// </summary>
        public bool Standalone => _standalone;

        /// <summary>True once the finished floor has been settled into the run.</summary>
        public bool Settled => _settled;

        /// <summary>The settlement the finished floor produced, or null.</summary>
        public FloorSettlement? LastSettlement { get; private set; }

        private void Start() => TryStart();

        private void OnDestroy()
        {
            if (_runner is not null)
            {
                // The one teardown path: the runner disposes the view, which disposes the session
                // and destroys its ECS world. "dispose IBattleSession ทุกครั้งที่ออกจาก SceneBattle"
                // is this line, and BattlePlayModeTests asserts that runner.Session is null after
                // the scene was left.
                _runner.Shutdown();
            }
        }

        private void Update()
        {
            if (_settled)
            {
                // The floor is settled, but its departure may not have been accepted yet: the loader
                // refuses a second transition while one is in flight, and one is in flight exactly
                // when the player arrived here through the fade. Asking again is the whole of the
                // recovery, and it costs one comparison per frame until it lands.
                TryLeave();
                return;
            }

            if (_runner is null || !_runner.HasEnded || _run is null)
            {
                return;
            }

            Settle();
        }

        /// <summary>
        /// Builds and begins the floor. Idempotent: a second call while running does nothing, so a
        /// test may drive it without waiting for <c>Start</c>.
        /// </summary>
        public bool TryStart()
        {
            if (_runner is not null && _runner.IsRunning)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                StartError = "BattleSceneDirector has no built BattleSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            if (_runner is null)
            {
                StartError = "BattleSceneDirector has no BattleSimulationRunner.";
                Debug.LogError(StartError, this);
                return false;
            }

            // The run, from the App scope's owner: created on demand, so a scene opened with no
            // camp is still playable, and shared with the camp otherwise.
            _run = _scope.Container.Resolve<RunScopeOwner>().RequireRun();
            _loader = _scope.Container.Resolve<SceneLoader>();

            if (!ResolveFloor(out BattleSetup setup, out int startingBia))
            {
                Debug.LogError("Battle scene could not start a floor: " + StartError, this);
                return false;
            }

            StartError = string.Empty;
            _settled = false;
            LastSettlement = null;

            // The scene marker, published by the scene that is on screen. Title and the Thai text
            // spike publish their own the same way; leaving the Battle scene out would make a
            // `?scene=Battle` handover invisible to a browser test, which is the only place the
            // handover can be observed (a WebGL build cannot run Unity's test framework).
            WebGlBridge.PublishScene(gameObject.scene.name);

            var context = new BattleRunnerContext
            {
                Factory = _scope.Container.Resolve<IBattleSessionFactory>(),
                Setup = setup,
                StartingBia = startingBia,
                VisualSeed = _run.Seed,
                Text = _scope.Container.Resolve<LocalizationService>(),
                Config = _scope.Container.Resolve<IGameConfig>(),
                HudState = _scope.Container.Resolve<BattleHudState>(),
                Hud = _scope.Container.Resolve<BattleHudPresenter>(),
                Speed = _scope.Container.Resolve<BattleSpeed>(),
            };

            _runner.Begin(context);
            return true;
        }

        /// <summary>
        /// The floor to run: the one the camp already departed for, or — when this scene was opened
        /// without a camp — one it departs for itself.
        /// </summary>
        private bool ResolveFloor(out BattleSetup setup, out int startingBia)
        {
            setup = null;
            startingBia = 0;

            BattleSetup pending = _run.PendingSetup;
            if (pending is not null)
            {
                // The normal path: the camp performed the departure (phase check, bag lock,
                // resolved loadout) and this is the floor it validated.
                setup = pending;
                startingBia = _run.Wallet.Bia;
                return true;
            }

            // No departure is outstanding — the scene was reached on its own. Start (or continue)
            // the run and depart here instead, so `?scene=Battle` still shows a real floor.
            _standalone = true;

            if (!BattleRunSource.TryStartFrom(
                    _run, _scope.Container.Resolve<IGameConfig>(), _scope.EquipSecondWeapon,
                    out setup, out startingBia, out string error))
            {
                StartError = error;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Settles the finished floor into the run and leaves for the next scene.
        /// </summary>
        /// <remarks>
        /// A lost floor — or a last cleared one — ends the run: the settlement is applied for real
        /// (HP and bia are the run's), and the scene leaves for the summary screen the same way it
        /// leaves for the camp. The settlement happens <b>before</b> the departure, so the numbers the
        /// summary screen reads are already the run's when that scene starts.
        /// </remarks>
        private void Settle()
        {
            _settled = true;

            BattleResult result = _runner.Result;
            if (result is null)
            {
                // The runner reports "ended" only with a result; if that ever changes, the floor
                // stays settled and the run is untouched rather than paid out for nothing.
                Debug.LogWarning("The floor ended without a BattleResult; nothing was settled.", this);
                return;
            }

            Result<FloorSettlement> settled = _run.ApplyBattleResult(result);
            if (settled.IsFailure)
            {
                StartError = "the floor's result was refused: " + settled.Error.Code;
                Debug.LogError(StartError, this);
                return;
            }

            FloorSettlement settlement = settled.Value;
            LastSettlement = settlement;

            // Where the settlement leaves for: the summary screen when the run ended here (a floor
            // it did not clear, or its last one), the camp otherwise. Both are ordinary scene
            // departures through the one loader.
            Debug.Log(
                "The floor is settled: " + result.Outcome + "/" + result.EndReason + " on floor " +
                settlement.FloorIndex + " (" + _run.FloorsCleared + "/" + _run.TotalFloors +
                " cleared, " + _run.Wallet.Bia + " bia, " + _run.Hp + "/" + _run.MaxHp + " hp).",
                this);

            _leavingFor = settlement.RunCompleted ? SceneId.Result : SceneId.Camp;
            TryLeave();
        }

        /// <summary>
        /// Asks the loader for the departure the settlement chose, and keeps asking while it refuses.
        /// </summary>
        /// <remarks>
        /// The only refusal a settled floor can meet is a transition already in flight, and the
        /// scenes it leaves for are all in the build settings. A single attempt would be enough in
        /// practice, but not robust: the floor is settled, so nothing else will ever ask again, and
        /// one refused frame would strand the player in a finished arena. The first refusal is logged
        /// and the retries are silent, so a genuine failure (a scene missing from the build) is still
        /// visible in the console exactly once instead of once per frame.
        /// </remarks>
        private void TryLeave()
        {
            if (_leavingFor is not SceneId target)
            {
                return;
            }

            if (_loader is not null && _loader.TryGo(target))
            {
                _leaveRefusedLogged = false;
                StartError = string.Empty;
                return;
            }

            StartError = _loader is null
                ? "there is no scene loader, so the settled floor cannot leave for " + target + "."
                : "the move to " + target + " was refused: " + _loader.LastError;

            if (_leaveRefusedLogged)
            {
                return;
            }

            _leaveRefusedLogged = true;
            Debug.LogWarning(StartError, this);
        }
    }
}
