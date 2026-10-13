using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// xoshiro128++ — a 32-bit-state, 128-bit-period pseudo-random number
    /// generator. Pure integer arithmetic, allocation-free, and identical on
    /// every runtime, which is what the fixed-tick battle simulation needs.
    /// </summary>
    /// <remarks>
    /// Reference algorithm: Blackman &amp; Vigna, "Scrambled Linear
    /// Pseudorandom Number Generators" (xoshiro128++). Only 32-bit operations
    /// are used, so behaviour cannot differ between a 64-bit CPU and the
    /// 32-bit wasm heap.
    /// <para>
    /// Not thread-safe by construction: one instance belongs to one stream and
    /// one consumer (the simulation is single-threaded and WebGL2 is too).
    /// </para>
    /// </remarks>
    public sealed class Xoshiro128 : IRandomSource
    {
        private uint _s0;
        private uint _s1;
        private uint _s2;
        private uint _s3;

        /// <summary>Creates a stream from <paramref name="seed"/> and immediately reseeds it.</summary>
        public Xoshiro128(uint seed)
        {
            Seed = seed;
            SeedFrom(seed);
        }

        /// <summary>The seed this stream was created from. Reused by save/restore in a later stage.</summary>
        public uint Seed { get; private set; }

        /// <summary>Re-derives the whole state from <paramref name="seed"/>, restarting the sequence.</summary>
        public void Reseed(uint seed)
        {
            Seed = seed;
            SeedFrom(seed);
        }

        /// <inheritdoc />
        public uint NextUInt()
        {
            unchecked
            {
                uint result = RotateLeft(_s0 + _s3, 7) + _s0;

                uint t = _s1 << 9;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = RotateLeft(_s3, 11);

                return result;
            }
        }

        /// <summary>
        /// Expands the 32-bit seed into the four state words with splitmix32.
        /// A raw seed is never used directly: low seeds would start the stream
        /// with correlated, low-entropy state.
        /// </summary>
        private void SeedFrom(uint seed)
        {
            uint state = seed;
            _s0 = SplitMix32.Next(ref state);
            _s1 = SplitMix32.Next(ref state);
            _s2 = SplitMix32.Next(ref state);
            _s3 = SplitMix32.Next(ref state);

            // xoshiro requires a non-zero state. Unreachable in practice, but a
            // zero state would return 0 forever, so it is guarded rather than assumed.
            if (_s0 == 0u && _s1 == 0u && _s2 == 0u && _s3 == 0u)
            {
                _s0 = 0x9E37_79B9u;
            }
        }

        private static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));
    }
}
