using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.SpineActors;
using R3;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Puts the ghost-doctor's <see cref="SpinePlayerView"/> where the battle's own player entity is,
    /// and gives it the pose the frame asks for (Stage 17b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Presentation only, and it reads what the view already copied.</b> The player is still one
    /// <see cref="RenderEntityKind.Player"/> entity in the frame — this replaces the sprite that
    /// entity used to be drawn with, not the entity. The speed is not a number of its own: it is
    /// <c>(current − previous) × tickRate</c> out of the same <see cref="BattleFrameSnapshot"/> the
    /// interpolating sprites are drawn from, so the figure and the ghosts cannot disagree about how
    /// fast the doctor is walking.
    /// </para>
    /// <para>
    /// <b>Nothing here is a rule.</b> The pose comes from <see cref="PlayerPoseSelector"/>, the facing
    /// from the frame's own <c>dx</c>, the draw order from <see cref="BattleWorldMap.SortingOrder"/>,
    /// and "dead" from the floor's own result — the phase the session ended on. The hurt reaction is a
    /// one-shot animation played from a <see cref="BattleEventType.PlayerDamaged"/> event, which may
    /// never change an outcome: a dropped event costs a flash, nothing else.
    /// </para>
    /// <para>
    /// <b>It owns no clock.</b> <see cref="Render"/> steps the animation state with the frame time the
    /// caller hands in, divided by the battle speed, because the speed lives on the animation state's
    /// own <c>TimeScale</c> (ADR 0037 §5). <c>Time.timeScale</c> is never touched, here or anywhere
    /// else in the arena.
    /// </para>
    /// <para>
    /// Everything it tells the view is sent only when the value changed: one <c>Play</c> per pose
    /// change, one sorting-order write per row crossed, one <c>SetSpeed</c> per speed change. A floor
    /// that stands still therefore plays one animation for a whole minute, which is what
    /// <c>spinePlayCalls</c> in the battle report is worth reading for.
    /// </para>
    /// </remarks>
    public sealed class PlayerFigurePresenter : IBattleEventListener, IDisposable
    {
        /// <summary>
        /// How far the figure has to move sideways in one tick before it turns. A twitch of float
        /// noise, not a threshold: whether the doctor is walking at all is the pose rule's call, and
        /// the facing follows the sign of a movement that already happened.
        /// </summary>
        public const float FacingDeadZone = 0.0001f;

        private readonly BattleSpeed _speed;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(1);

        private MonoBehaviour _host;
        private IPlayerFigure _view;
        private float _billboardPitch;
        private Vector3 _from;
        private Vector3 _to;
        private bool _hasPlayer;
        private bool _hasPrevious;
        private bool _dead;
        private bool _paused;
        private float _speedMultiplier = 1f;
        private int _sortingOrder;
        private int _sortingWrites;
        private int _speedWrites;
        private bool _disposed;

        /// <summary>Creates a presenter that follows the scene's speed control.</summary>
        /// <param name="speed">The 1×/2× control, or null in a fixture that has none.</param>
        public PlayerFigurePresenter(BattleSpeed speed)
        {
            _speed = speed;
        }

        /// <summary>
        /// The scene's figure component (whichever backend it is), or null before
        /// <see cref="Attach"/>. A scene field is a <see cref="MonoBehaviour"/> because Unity cannot
        /// serialize an interface; <see cref="Figure"/> is the call contract off the same object.
        /// </summary>
        public MonoBehaviour View => _host;

        /// <summary>The figure's call contract, or null before <see cref="Attach"/>.</summary>
        public IPlayerFigure Figure => _view;

        /// <summary>True once the figure has been built and is the thing drawn for the player.</summary>
        public bool IsReady => _view != null && _view.IsBegun;

        /// <summary>Why the figure could not be built, or null.</summary>
        public string ErrorCode => _view == null ? null : _view.ErrorCode;

        /// <summary>Animations this floor has started (Begin's idle is the first).</summary>
        public int PlayCalls => _view == null ? 0 : _view.PlayCalls;

        /// <summary>The animation playing right now, or null.</summary>
        public string Animation => _view == null ? null : _view.CurrentAnimation;

        /// <summary>The pose last presented.</summary>
        public PlayerPose Pose => _view == null ? PlayerPose.Idle : _view.Pose;

        /// <summary>+1 right, -1 left.</summary>
        public int Facing => _view == null ? 1 : _view.FaceDirection;

        /// <summary>True while the hurt animation is playing.</summary>
        public bool IsHurt => _view != null && _view.IsHurt;

        /// <summary>True while the floor's pause is holding the animation still.</summary>
        public bool IsPaused => _paused;

        /// <summary>True once the floor's result said the player lost.</summary>
        public bool IsDead => _dead;

        /// <summary>The speed the last frame's player entity moved at, in simulation units a second.</summary>
        public float SpeedUnitsPerSecond { get; private set; }

        /// <summary>The draw order the figure was last placed at.</summary>
        public int SortingOrder => _sortingOrder;

        /// <summary>How many times the draw order was written (a value that changed).</summary>
        public int SortingWrites => _sortingWrites;

        /// <summary>How many times the animation state's speed was written.</summary>
        public int SpeedWrites => _speedWrites;

        /// <summary>
        /// Hands the presenter the scene's figure and the pitch every billboard in the arena stands
        /// at. Called once, by <see cref="BattleViewScene"/>, before <see cref="Begin"/>.
        /// </summary>
        public void Attach(MonoBehaviour view, float billboardPitch)
        {
            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            IPlayerFigure figure = view.GetComponent<IPlayerFigure>();
            if (figure is null)
            {
                throw new ArgumentException(
                    "'…" + view.GetType().Name + "' does not implement IPlayerFigure", nameof(view));
            }

            Detach();

            _host = view;
            _view = figure;
            _billboardPitch = billboardPitch;

            if (_speed != null)
            {
                _subscriptions.Add(_speed.Multiplier.Subscribe(OnSpeedChanged));
                OnSpeedChanged(_speed.Multiplier.Value);
            }
        }

        /// <summary>
        /// Lets go of the view. The view's GameObject belongs to the scene, so nothing here destroys
        /// it; this only stops the speeds, the poses and the ticks.
        /// </summary>
        public void Detach()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();

            _view = null;
            _host = null;
            _hasPlayer = false;
            _hasPrevious = false;
            _sortingOrder = 0;
        }

        /// <summary>
        /// Builds the figure once, at the floor's start — never in the middle of one. The pitch is
        /// the camera rig's own (the sprite billboards' <c>Quaternion.Euler(pitch, 0, 0)</c>), so the
        /// figure and the ghosts face the camera at the same angle.
        /// </summary>
        /// <returns>True when there is a drawable figure.</returns>
        public bool Begin()
        {
            if (_view == null)
            {
                return false;
            }

            _host.transform.rotation = Quaternion.Euler(_billboardPitch, 0f, 0f);

            bool began = _view.Begin();
            if (!began)
            {
                // The view itself logged the code. A release build cannot lose the doctor, so the
                // pooled placeholder sprite keeps standing in for it — and says so, once, here:
                // a silent fallback is how a stage ships a screen it never actually drew.
                if (!DevelopmentBuild)
                {
                    Debug.LogWarning(
                        "[PlayerFigurePresenter] " + (_view.ErrorCode ?? "spine.player_unknown") +
                        ": the player figure could not be built, so the placeholder sprite draws the " +
                        "doctor on this floor (spineReady=false in __projectSurviveBattle).", _host);
                }

                return false;
            }

            _sortingOrder = _view.SortingOrder;
            _view.SetSpeed(_speedMultiplier);
            return true;
        }

        /// <summary>
        /// Reads one tick's frame: where the player is, how fast it moved, and which pose and facing
        /// that is. Called once per tick, before <see cref="Render"/>.
        /// </summary>
        public void AcceptSnapshot(BattleFrameSnapshot snapshot)
        {
            if (!IsReady || snapshot is null || !snapshot.HasFrame)
            {
                return;
            }

            ReadOnlySpan<RenderEntity> current = snapshot.Current;
            for (int i = 0; i < current.Length; i++)
            {
                RenderEntity entity = current[i];
                if (entity.Kind != RenderEntityKind.Player)
                {
                    continue;
                }

                _to = BattleWorldMap.ToWorld(entity);
                _hasPlayer = true;

                bool hadPrevious = snapshot.TryGetPrevious(entity.Entity, out RenderEntity previous);
                _hasPrevious = hadPrevious;

                float dx = 0f;
                if (hadPrevious)
                {
                    _from = BattleWorldMap.ToWorld(previous);
                    dx = entity.X - previous.X;

                    float dy = entity.Y - previous.Y;
                    SpeedUnitsPerSecond = Mathf.Sqrt((dx * dx) + (dy * dy)) * BattleConstants.TickRate;
                }
                else
                {
                    // The tick the floor started on: nothing moved yet, and the figure stands where
                    // it was published rather than sliding in from the arena's centre.
                    _from = _to;
                    SpeedUnitsPerSecond = 0f;
                }

                PresentPose(dx);
                return;
            }

            // No player in the frame (the floor has not published one yet, or it is over).
            _hasPlayer = false;
            SpeedUnitsPerSecond = 0f;
        }

        /// <summary>
        /// Places the figure between the last two ticks and advances its animation. Called once per
        /// rendered frame — the same place the sprite bridge draws from, with the same alpha.
        /// </summary>
        /// <param name="alpha">The frame's interpolation fraction, in <c>[0, 1)</c>.</param>
        /// <param name="scaledDeltaSeconds">
        /// The frame time the runner handed the whole view — already multiplied by the battle speed,
        /// because the damage numbers and the telegraphs animate at the speed the floor runs at. The
        /// figure must not be scaled twice: the speed is its animation state's <c>TimeScale</c>, so
        /// the fraction of it is removed here before the step.
        /// </param>
        public void Render(float alpha, float scaledDeltaSeconds)
        {
            if (!IsReady)
            {
                return;
            }

            if (_hasPlayer)
            {
                Vector3 position = BattleWorldMap.Interpolate(_from, _to, alpha);
                _host.transform.position = position;

                int order = BattleWorldMap.SortingOrder(RenderEntityKind.Player, position.z);
                if (order != _sortingOrder)
                {
                    _sortingOrder = order;
                    _sortingWrites++;
                    _view.SetSortingOrder(order);
                }
            }

            float delta = _speedMultiplier > 0f ? scaledDeltaSeconds / _speedMultiplier : scaledDeltaSeconds;
            _view.Tick(delta);
        }

        /// <summary>
        /// Holds or resumes the animation. The floor's clock and this move together — the runner
        /// stops stepping and rendering while paused, and the animation state is told separately so
        /// that a resumed floor cannot owe the pause's time to the walk cycle.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (_paused == paused)
            {
                return;
            }

            _paused = paused;

            if (_view != null)
            {
                _view.SetPaused(paused);
            }
        }

        /// <summary>
        /// The floor's result. Only a lost floor draws the death pose: a cleared or surrendered one
        /// leaves the doctor as they stood, because the run is over, not the character.
        /// </summary>
        public void End(BattleResult result)
        {
            if (!IsReady || result is null || result.Outcome != BattlePhase.Lost)
            {
                return;
            }

            _dead = true;
            _view.Present(PlayerPose.Dead, _view.FaceDirection);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The only event this reads is <see cref="BattleEventType.PlayerDamaged"/>, and it plays a
        /// one-shot. A hurt flash may not decide anything: what a hit cost is in the frame's HP, not
        /// here.
        /// </remarks>
        public void OnBattleEvent(in BattleEvent battleEvent)
        {
            if (battleEvent.Type != BattleEventType.PlayerDamaged)
            {
                return;
            }

            PlayHurt();
        }

        /// <summary>Plays the profile's hurt reaction once (no-op while dead or with no figure).</summary>
        public void PlayHurt()
        {
            if (!IsReady || _dead)
            {
                return;
            }

            _view.PlayHurt();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Detach();
        }

        /// <summary>True in the Editor and in a development build — the two places a loud log is cheap.</summary>
        private static bool DevelopmentBuild => Debug.isDebugBuild || Application.isEditor;

        private void PresentPose(float dx)
        {
            float threshold = _view.RunSpeedThreshold;

            PlayerPose pose = PlayerPoseSelector.Select(SpeedUnitsPerSecond, _view.Pose, _dead, threshold);
            int facing = PlayerPoseSelector.Facing(dx, _view.FaceDirection, FacingDeadZone);

            // Cheap by contract: Present writes nothing when the pose and the facing are the ones
            // already drawn, which is the case on nearly every tick of a floor.
            _view.Present(pose, facing);
        }

        private void OnSpeedChanged(float multiplier)
        {
            _speedMultiplier = multiplier;

            if (_view == null || !IsReady)
            {
                return;
            }

            _speedWrites++;
            _view.SetSpeed(multiplier);
        }
    }
}
