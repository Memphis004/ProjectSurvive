using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// One tick of player movement input, in deterministic integer space.
    /// </summary>
    /// <remarks>
    /// A raw float axis is turned into an integer scaled by
    /// <see cref="BattleConstants.InputAxisScale"/> at the boundary, so the
    /// simulation never carries a float that two runs of the same seed could
    /// disagree on, and the simulation is not surprised by an out-of-range axis.
    /// Use <see cref="FromAxis"/> to build one; the raw constructor is for
    /// already-scaled values (a replay log, a test).
    /// </remarks>
    public readonly struct PlayerInputFrame
    {
        /// <summary>No movement.</summary>
        public static readonly PlayerInputFrame Idle = new PlayerInputFrame(0, 0);

        /// <summary>Creates a frame from already-scaled axis values.</summary>
        public PlayerInputFrame(int moveX, int moveY)
        {
            MoveX = moveX;
            MoveY = moveY;
        }

        /// <summary>Horizontal axis in <c>[-InputAxisScale, InputAxisScale]</c>.</summary>
        public int MoveX { get; }

        /// <summary>Vertical axis in <c>[-InputAxisScale, InputAxisScale]</c>.</summary>
        public int MoveY { get; }

        /// <summary>The horizontal axis as a float in <c>[-1, 1]</c>.</summary>
        public float AxisX => MoveX / (float)BattleConstants.InputAxisScale;

        /// <summary>The vertical axis as a float in <c>[-1, 1]</c>.</summary>
        public float AxisY => MoveY / (float)BattleConstants.InputAxisScale;

        /// <summary>True when the frame asks for no movement at all.</summary>
        public bool IsIdle => MoveX == 0 && MoveY == 0;

        /// <summary>
        /// Normalises <paramref name="x"/>,<paramref name="y"/> to a unit vector
        /// when longer than 1 (so a diagonal is not faster), clamps each axis into
        /// range, scales both by <see cref="BattleConstants.InputAxisScale"/>, and
        /// rounds to the nearest integer. A non-finite axis yields
        /// <see cref="Idle"/>.
        /// </summary>
        public static PlayerInputFrame FromAxis(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
            {
                return Idle;
            }

            float lengthSquared = (x * x) + (y * y);
            if (lengthSquared > 1f)
            {
                float inverseLength = 1f / (float)Math.Sqrt(lengthSquared);
                x *= inverseLength;
                y *= inverseLength;
            }

            int moveX = (int)Math.Round(x * BattleConstants.InputAxisScale);
            int moveY = (int)Math.Round(y * BattleConstants.InputAxisScale);

            int limit = BattleConstants.InputAxisScale;
            if (moveX > limit)
            {
                moveX = limit;
            }
            else if (moveX < -limit)
            {
                moveX = -limit;
            }

            if (moveY > limit)
            {
                moveY = limit;
            }
            else if (moveY < -limit)
            {
                moveY = -limit;
            }

            return new PlayerInputFrame(moveX, moveY);
        }

        /// <inheritdoc />
        public override string ToString() => "(" + AxisX.ToString("0.###") + ", " + AxisY.ToString("0.###") + ")";
    }
}
