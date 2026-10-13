using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// What Presentation reads from a battle between ticks.
    /// </summary>
    /// <remarks>
    /// The <see cref="Entities"/> span is a view over the session's own buffer and
    /// stays valid only until the next <see cref="IBattleSession.Step"/> or
    /// <see cref="IBattleSession.Submit"/> — copy anything you need to keep. The
    /// implementation is a class (not a ref struct) so it can implement this
    /// interface on C# 9 / netstandard2.1, where a ref struct cannot.
    /// </remarks>
    public interface IBattleFrame
    {
        /// <summary>The tick this frame describes.</summary>
        long Tick { get; }

        /// <summary>The HUD values at this tick (a by-value snapshot).</summary>
        HudState Hud { get; }

        /// <summary>The entities at this tick. Valid until the next Step/Submit.</summary>
        ReadOnlySpan<RenderEntity> Entities { get; }
    }
}
