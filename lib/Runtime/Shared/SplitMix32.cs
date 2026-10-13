namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// splitmix32 — the state mixer used to expand a single 32-bit seed into
    /// xoshiro128 state words and into per-stream seeds.
    /// </summary>
    /// <remarks>
    /// Algorithm: the classic splitmix32 variant — add the golden-ratio odd
    /// constant, then a 32-bit avalanche mix (shift 16 / multiply, shift 15 /
    /// multiply, shift 15). Kept internal on purpose: it is an implementation
    /// detail of <see cref="Xoshiro128"/> and <see cref="RandomStreams"/>.
    /// </remarks>
    internal static class SplitMix32
    {
        private const uint GoldenRatio = 0x9E37_79B9u;

        /// <summary>Advances <paramref name="state"/> and returns the next mixed 32-bit word.</summary>
        public static uint Next(ref uint state)
        {
            unchecked
            {
                state += GoldenRatio;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x21F0_AAADu;
                z = (z ^ (z >> 15)) * 0x735A_2D97u;
                return z ^ (z >> 15);
            }
        }

        /// <summary>One-shot mix of a standalone seed value.</summary>
        public static uint Hash(uint seed) => Next(ref seed);
    }
}
