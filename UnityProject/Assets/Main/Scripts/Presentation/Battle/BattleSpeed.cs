using System;
using R3;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The 1×/2× time control of a battle scene ("ปุ่มความเร็ว 1x/2x").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The multiplier scales the real delta the runner hands to
    /// <c>FixedTicker</c>; it never touches the simulation. Doubling therefore doubles how
    /// many fixed ticks a second of wall clock buys (<c>MaxStepsPerFrame</c> still bounds
    /// what one frame may run, so a fast-forward on a slow machine degrades to dropped
    /// ticks rather than to a long frame).
    /// </para>
    /// <para>
    /// This is UI-level state, so R3 carries it (AGENTS rule 10), and it is one of the few
    /// things a player can change mid-floor.
    /// </para>
    /// </remarks>
    public sealed class BattleSpeed : IDisposable
    {
        /// <summary>The speeds a player may pick, in order.</summary>
        public static readonly float[] Available = { 1f, 2f };

        /// <summary>The selected multiplier.</summary>
        public ReactiveProperty<float> Multiplier { get; } = new ReactiveProperty<float>(Available[0]);

        /// <summary>Advances to the next speed in <see cref="Available"/> (wrapping).</summary>
        public void Toggle()
        {
            int current = IndexOf(Multiplier.Value);
            Set(Available[(current + 1) % Available.Length]);
        }

        /// <summary>Selects <paramref name="value"/> when it is one of <see cref="Available"/>.</summary>
        public void Set(float value)
        {
            if (IndexOf(value) >= 0 && Multiplier.Value != value)
            {
                Multiplier.Value = value;
            }
        }

        /// <summary>A label for a speed, e.g. <c>1x</c>. Numbers, not text, so it needs no translation.</summary>
        public static string Label(float value) =>
            value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x";

        /// <inheritdoc />
        public void Dispose() => Multiplier.Dispose();

        private static int IndexOf(float value)
        {
            for (int i = 0; i < Available.Length; i++)
            {
                if (Available[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
