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

        /// <summary>
        /// The four state words, as plain values, so a checkpoint can carry a stream's
        /// <b>position</b> and not just its seed (Stage 12).
        /// </summary>
        public Xoshiro128State ExportState() => new Xoshiro128State(_s0, _s1, _s2, _s3);

        /// <summary>
        /// Restores the exact position a previous <see cref="ExportState"/> captured, so the
        /// next <see cref="NextUInt"/> continues the original sequence.
        /// </summary>
        /// <remarks>
        /// Importing a stream must not be a reseed: reseeding replays the sequence from its
        /// start, which would hand a reloaded run the same shop rolls it already consumed.
        /// The all-zero state is refused because xoshiro would then return zero forever.
        /// </remarks>
        /// <exception cref="ArgumentException">The state is all zero.</exception>
        public void ImportState(Xoshiro128State state)
        {
            if (state.IsZero)
            {
                throw new ArgumentException("A xoshiro128 state cannot be all zero.", nameof(state));
            }

            _s0 = state.S0;
            _s1 = state.S1;
            _s2 = state.S2;
            _s3 = state.S3;
        }

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

    /// <summary>
    /// A xoshiro128++ stream's four state words as plain values (Stage 12).
    /// </summary>
    /// <remarks>
    /// A value type with no behaviour beyond equality, so a checkpoint DTO can carry it
    /// without the serializer reaching into the generator, and so an imported state is a
    /// copy rather than a live view of the generator it came from.
    /// </remarks>
    public readonly struct Xoshiro128State : IEquatable<Xoshiro128State>
    {
        /// <summary>Captures the four words.</summary>
        public Xoshiro128State(uint s0, uint s1, uint s2, uint s3)
        {
            S0 = s0;
            S1 = s1;
            S2 = s2;
            S3 = s3;
        }

        /// <summary>First state word.</summary>
        public uint S0 { get; }

        /// <summary>Second state word.</summary>
        public uint S1 { get; }

        /// <summary>Third state word.</summary>
        public uint S2 { get; }

        /// <summary>Fourth state word.</summary>
        public uint S3 { get; }

        /// <summary>True when every word is zero — the one state xoshiro must never hold.</summary>
        public bool IsZero => S0 == 0u && S1 == 0u && S2 == 0u && S3 == 0u;

        /// <inheritdoc />
        public bool Equals(Xoshiro128State other) =>
            S0 == other.S0 && S1 == other.S1 && S2 == other.S2 && S3 == other.S3;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Xoshiro128State other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ S0) * 16777619u;
                hash = (hash ^ S1) * 16777619u;
                hash = (hash ^ S2) * 16777619u;
                hash = (hash ^ S3) * 16777619u;
                return (int)hash;
            }
        }

        public static bool operator ==(Xoshiro128State left, Xoshiro128State right) => left.Equals(right);

        public static bool operator !=(Xoshiro128State left, Xoshiro128State right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() =>
            "xoshiro(" + S0.ToString("X8") + "," + S1.ToString("X8") + ","
            + S2.ToString("X8") + "," + S3.ToString("X8") + ")";
    }
}
