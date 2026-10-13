using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// The only randomness entry point allowed in Core code (AGENTS.md rule 6).
    /// Streams are separated (shop / battle / visual) and each stream is an
    /// independent instance, so consuming one stream can never shift another.
    /// </summary>
    /// <remarks>
    /// Core code must never touch <c>UnityEngine.Random</c>, <c>System.Random</c>,
    /// <c>Time</c>, or <c>DateTime.Now</c>. Determinism is only guaranteed inside a
    /// single runtime — never across browsers or CPUs.
    /// </remarks>
    public interface IRandomSource
    {
        /// <summary>Returns the next value in the stream, uniformly over the whole <see cref="uint"/> range.</summary>
        uint NextUInt();
    }

    /// <summary>Range helpers over <see cref="IRandomSource"/>. No allocation, no closures.</summary>
    public static class RandomSourceExtensions
    {
        /// <summary>
        /// Uniform integer in <c>[minInclusive, maxExclusive)</c>, free of modulo bias
        /// (Lemire's multiply-shift bounded generation with rejection).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is not greater than <paramref name="minInclusive"/>.</exception>
        public static int NextInt(this IRandomSource source, int minInclusive, int maxExclusive)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive), maxExclusive, "maxExclusive must be greater than minInclusive.");
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            ulong product = (ulong)source.NextUInt() * range;
            uint low = (uint)product;
            if (low < range)
            {
                uint threshold = (uint)(0x1_0000_0000UL % range);
                while (low < threshold)
                {
                    product = (ulong)source.NextUInt() * range;
                    low = (uint)product;
                }
            }

            // Computed in long so the full int range (range == 2^32 - 1) cannot
            // wrap around before the final, always-in-range conversion.
            return (int)(minInclusive + (long)(product >> 32));
        }

        /// <summary>Uniform float in <c>[0, 1)</c> with 24-bit resolution.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        public static float NextFloat(this IRandomSource source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return (source.NextUInt() >> 8) * (1.0f / 16777216.0f);
        }
    }
}
