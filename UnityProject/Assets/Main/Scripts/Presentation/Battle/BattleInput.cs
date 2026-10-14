using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>Where the runner's movement input comes from.</summary>
    /// <remarks>
    /// A port rather than a direct keyboard read, because it is the one thing a test or a
    /// bot has to be able to drive without a device — the same shape the Stage 07 bot uses
    /// against a real session.
    /// </remarks>
    public interface IBattleInputSource
    {
        /// <summary>The movement for the tick about to run.</summary>
        PlayerInputFrame Current { get; }
    }

    /// <summary>
    /// Turns a keyboard into a <see cref="PlayerInputFrame"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The adapter is the boundary AGENTS rule 6 draws: outside it a movement axis is a
    /// float a device produced, inside the simulation it is an integer scaled by
    /// <see cref="BattleConstants.InputAxisScale"/>. <see cref="PlayerInputFrame.FromAxis"/>
    /// does the normalisation, so a diagonal is not faster and a replay never carries a
    /// float two runs could disagree about.
    /// </para>
    /// <para>
    /// Keys are read from the Input System package. Under Play Mode tests or a headless
    /// frame there is no keyboard, and <see cref="SetOverride"/> lets a test drive the same
    /// path the player does.
    /// </para>
    /// </remarks>
    public sealed class BattleInputAdapter : IBattleInputSource
    {
        private Vector2 _override;
        private bool _hasOverride;

        /// <summary>The movement for this tick.</summary>
        public PlayerInputFrame Current
        {
            get
            {
                if (_hasOverride)
                {
                    return PlayerInputFrame.FromAxis(_override.x, _override.y);
                }

                Vector2 axis = ReadKeyboard();
                return PlayerInputFrame.FromAxis(axis.x, axis.y);
            }
        }

        /// <summary>
        /// Pins the axis to <paramref name="x"/>,<paramref name="y"/> (both in [-1, 1]),
        /// bypassing the keyboard. Used by tests and by an automated walkthrough.
        /// </summary>
        public void SetOverride(float x, float y)
        {
            _override = new Vector2(x, y);
            _hasOverride = true;
        }

        /// <summary>Returns to reading the keyboard.</summary>
        public void ClearOverride() => _hasOverride = false;

        private static Vector2 ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard is null)
            {
                // No device: a PlayMode test, a headless frame, or a browser that never
                // focused the canvas. Standing still is the correct reading, never a guess.
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            // The simulation's +Y is "away from the camera", which is what screen-up means
            // in the 2.5D scene, so W is +Y rather than a camera-relative vector.
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                y += 1f;
            }

            return new Vector2(x, y);
        }
    }
}
