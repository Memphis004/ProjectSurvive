using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The footprint a telegraphed attack warns about (Stage 11b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Travels as <see cref="BattleEvent.Value"/> on a
    /// <see cref="BattleEventType.TelegraphStarted"/>, the way a shape travels to the
    /// floor decal that draws it. Values are stable and append-only: a new shape is a new
    /// number at the end, never a renumbering, because a recorded event stream outlives
    /// the build that wrote it.
    /// </para>
    /// <para>
    /// The rest of the warning is beside it on the event:
    /// <see cref="BattleEvent.X"/>/<see cref="BattleEvent.Y"/> are the origin (the centre
    /// of a circle, or the start of a line), <see cref="BattleEvent.Radius"/> is the
    /// circle's radius or the line's half-width,
    /// <see cref="BattleEvent.Length"/> the line's length (0 for a circle) and
    /// <see cref="BattleEvent.Angle"/> its direction in radians.
    /// </para>
    /// </remarks>
    public enum TelegraphShape
    {
        /// <summary>A disc centred on the event position; damages everything inside it.</summary>
        Circle = 0,

        /// <summary>A line from the event position, <see cref="BattleEvent.Angle"/> radians from +X.</summary>
        Line = 1,
    }
}
