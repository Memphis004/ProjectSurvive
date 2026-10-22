using System;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Pause
{
    /// <summary>
    /// The pause overlay's logic: hold the floor still, let it run again, or leave the floor and
    /// return to the camp (Stage 13c-2b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things are true at once when the player presses หยุด, and each belongs to a different
    /// layer, which is why this is not in the view: the <b>clock</b> stops
    /// (<see cref="BattleSimulationRunner.SetPaused"/>), the <b>overlay</b> appears (the state the
    /// view mirrors), and a <b>retreat</b> is an Application command
    /// (<see cref="RunSession.AbandonFloor"/>). The view forwards presses; this decides.
    /// </para>
    /// <para>
    /// <b>Leaving is not a surrender.</b> <c>ApplyBattleResult</c> ends the run on a defeat, so a
    /// retreat must not travel through it: the floor is simply left unfinished, its HP is carried
    /// back so the pause menu is not a free heal, and the run returns to the camp it departed from
    /// with its purse, its floor cursor and its shelves untouched.
    /// </para>
    /// <para>
    /// The scene change is <b>requested</b>, not performed: <c>BattleSceneDirector</c> is the one
    /// place that owns the loader, and it retries a refused move instead of dropping the player in a
    /// frozen arena that never leaves (the same split as the summary screen).
    /// </para>
    /// </remarks>
    public sealed class BattlePausePresenter : IDisposable
    {
        private readonly BattlePauseState _state;
        private readonly LocalizationService _text;

        private BattleSimulationRunner _runner;
        private RunSession _run;

        public BattlePausePresenter(BattlePauseState state, LocalizationService text)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The scene the overlay has asked to leave for, or null.</summary>
        public SceneId? RequestedScene { get; private set; }

        /// <summary>Why the last leave was refused, or an empty string.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>The floor's HP as of the last frame the runner copied (the retreat's HP).</summary>
        /// <remarks>
        /// The frame when there is one, and the run's own value otherwise (a scene whose floor has
        /// not rendered yet, or a fixture with no view): the two agree the moment the first frame is
        /// copied, and reading the run is the honest fallback rather than a zero that would clamp the
        /// player's HP away.
        /// </remarks>
        public int CurrentHp =>
            _runner?.View?.Snapshot.HasFrame == true ? _runner.View.Snapshot.Hud.Hp : (_run?.Hp ?? 0);

        /// <summary>True while the overlay is up.</summary>
        public bool IsOpen => _state.IsOpen.Value;

        /// <summary>True once the scene's director has handed over the floor it holds.</summary>
        public bool IsAttached => _runner is not null;

        /// <summary>
        /// Hands the presenter the floor it holds and the run it belongs to. Called once by the
        /// scene's director, after the floor began.
        /// </summary>
        public void Attach(BattleSimulationRunner runner, RunSession run)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _run = run ?? throw new ArgumentNullException(nameof(run));

            // The four strings are written here as well as on open, so the HUD's หยุด control says
            // what it does before anyone presses it.
            ApplyStrings();
        }

        /// <summary>Shows the overlay and holds the floor still. Idempotent.</summary>
        public void Open()
        {
            if (_state.IsOpen.Value)
            {
                return;
            }

            if (_runner is null || _run is null)
            {
                // The scene has not attached yet (a fixture, or a press before Begin): a pause that
                // froze nothing would show a button that does nothing, so it is refused instead.
                LastError = "the pause overlay has no floor to hold yet.";
                Debug.LogWarning(LastError);
                return;
            }

            if (_runner.HasEnded)
            {
                // The floor is over and its result is on its way to the camp; pausing a settled
                // floor would only hide the banner the player has already earned.
                return;
            }

            ApplyStrings();
            _state.IsOpen.Value = true;
            _runner.SetPaused(true);
            LastError = string.Empty;
        }

        /// <summary>Hides the overlay and lets the floor run again. Idempotent.</summary>
        public void Resume()
        {
            if (!_state.IsOpen.Value)
            {
                return;
            }

            _state.IsOpen.Value = false;
            _runner?.SetPaused(false);
        }

        /// <summary>Escape's meaning: open while running, resume while paused.</summary>
        public void Toggle()
        {
            if (_state.IsOpen.Value)
            {
                Resume();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// Leaves the floor for the camp without settling it, or refuses and changes nothing.
        /// </summary>
        /// <remarks>
        /// The HP comes from the frame the runner last copied rather than from a fresh tick: while the
        /// overlay is up nothing steps, and the frame on screen is exactly the damage the floor did.
        /// The run's own command clamps it and is the only writer of <c>RunSession.Hp</c>, so a
        /// second writer cannot make the two disagree.
        /// </remarks>
        public Result LeaveForCamp()
        {
            if (_run is null || _runner is null)
            {
                return Refuse("the pause overlay has no floor to leave.");
            }

            if (_runner.HasEnded)
            {
                return Refuse("the floor has already ended; the scene is settling it.");
            }

            Result abandoned = _run.AbandonFloor(CurrentHp);
            if (abandoned.IsFailure)
            {
                return Refuse("the run refused to abandon the floor: " + abandoned.Error.Code);
            }

            // The overlay stays up while the fade runs: a panel that disappeared first would show the
            // arena in play for the frames before the scene change, which reads as the game resuming.
            RequestedScene = SceneId.Camp;
            LastError = string.Empty;
            Debug.Log(
                "The floor was abandoned at " + CurrentHp + " hp; the run returns to the camp on floor "
                + _run.FloorIndex + " with " + _run.Wallet.Bia + " bia.",
                _runner);
            return abandoned;
        }

        /// <summary>Clears the requested scene once the loader accepted it (called by the director).</summary>
        public void DepartureAccepted() => RequestedScene = null;

        private Result Refuse(string reason)
        {
            LastError = reason;
            Debug.LogWarning(reason, _runner);
            return Result.Fail(RunError.NotInBattle, ErrorKind.InvalidState);
        }

        private void ApplyStrings()
        {
            BattleHudState.Set(_state.Title, _text.Get("ui.pause.title"));
            BattleHudState.Set(_state.Warning, _text.Get("ui.pause.warning"));
            BattleHudState.Set(_state.ResumeLabel, _text.Get("ui.pause.resume"));
            BattleHudState.Set(_state.LeaveLabel, _text.Get("ui.pause.leave"));
            BattleHudState.Set(_state.OpenLabel, _text.Get("ui.pause.open"));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Nothing owned: the state belongs to the scope and the runner to the scene.
            _runner = null;
            _run = null;
        }
    }
}
