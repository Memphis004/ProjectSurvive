using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// What Presentation reads from a battle between ticks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Entities"/> span is a view over the session's own buffer and
    /// stays valid only until the next <see cref="IBattleSession.Step"/> or
    /// <see cref="IBattleSession.Submit"/> — copy anything you need to keep. The
    /// implementation is a class (not a ref struct) so it can implement this
    /// interface on C# 9 / netstandard2.1, where a ref struct cannot.
    /// </para>
    /// <para>
    /// The battle ticks at 30 Hz while rendering runs faster, so a reader that
    /// wants smooth motion interpolates <see cref="PreviousEntities"/> towards
    /// <see cref="Entities"/> with an alpha of its own (Stage 08's
    /// <c>BattleSimulationRunner</c>). Both spans come from one double-buffered
    /// implementation, <see cref="BattleFrameBuffer"/>; a second implementation is
    /// forbidden so the fake and the EcsLite simulation publish identical frame
    /// behaviour (ADR 0012).
    /// </para>
    /// </remarks>
    public interface IBattleFrame
    {
        /// <summary>The tick this frame describes.</summary>
        long Tick { get; }

        /// <summary>The HUD values at this tick (a by-value snapshot; never interpolated).</summary>
        HudState Hud { get; }

        /// <summary>
        /// The entities at this tick ("Cur"). Valid until the next Step/Submit.
        /// </summary>
        ReadOnlySpan<RenderEntity> Entities { get; }

        /// <summary>
        /// The entities at the tick before this one ("Prev"), the other end of the
        /// interpolation pair. Empty on a session's first frame. Valid until the
        /// next Step/Submit — after that the buffer reuses the slot for the tick
        /// being written.
        /// </summary>
        ReadOnlySpan<RenderEntity> PreviousEntities { get; }
    }
}
