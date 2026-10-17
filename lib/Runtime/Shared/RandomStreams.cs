using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// The separated random streams of a run, all derived from one run seed.
    /// </summary>
    /// <remarks>
    /// Derivation is deterministic and stream-independent: a stream's seed is a
    /// pure function of <c>(masterSeed, kind)</c>, so adding or reordering a
    /// stream cannot shift the others, and no stream shares state with another.
    /// Rebuilding <see cref="RandomStreams"/> with the same master seed always
    /// reproduces the same sequences — that is the determinism guarantee the
    /// simulation relies on.
    /// </remarks>
    public sealed class RandomStreams
    {
        /// <summary>Creates the run / encounter / visual streams for a run seed.</summary>
        public RandomStreams(uint masterSeed)
        {
            MasterSeed = masterSeed;
            Run = new Xoshiro128(DeriveStreamSeed(masterSeed, RandomStreamKind.Run));
            Encounter = new Xoshiro128(DeriveStreamSeed(masterSeed, RandomStreamKind.Encounter));
            Visual = new Xoshiro128(DeriveStreamSeed(masterSeed, RandomStreamKind.Visual));
        }

        /// <summary>The run seed every stream was derived from.</summary>
        public uint MasterSeed { get; }

        /// <summary>Run-level stream (loot, prices, anything outside play).</summary>
        public IRandomSource Run { get; }

        /// <summary>Play-outcome stream.</summary>
        public IRandomSource Encounter { get; }

        /// <summary>Cosmetic stream.</summary>
        public IRandomSource Visual { get; }

        /// <remarks>
        /// There is deliberately <b>no</b> <c>Reward</c> stream property: a reward draw is
        /// <see cref="DeriveStreamSeed"/> with <see cref="RandomStreamKind.Reward"/>, never a
        /// stateful stream, so an offer cannot depend on how many offers came before it
        /// (ADR 0021).
        /// </remarks>

        /// <summary>
        /// The seed of one stream. Depends only on <paramref name="masterSeed"/>
        /// and <paramref name="kind"/> — never on how much another stream was used.
        /// </summary>
        public static uint DeriveStreamSeed(uint masterSeed, RandomStreamKind kind)
        {
            // Decorrelate the streams with a fixed per-stream salt, then run the
            // result through splitmix32 so that a low-entropy run seed (e.g. 0 or
            // 1) still produces a well-mixed stream seed. An explicit switch (not
            // Enum.IsDefined) keeps reflection and boxing out of Core.
            uint state = masterSeed ^ StreamSalt(kind);
            return SplitMix32.Next(ref state);
        }

        private static uint StreamSalt(RandomStreamKind kind)
        {
            switch (kind)
            {
                case RandomStreamKind.Run: return 0x5348_4F50u;
                case RandomStreamKind.Encounter: return 0x4241_5454u;
                case RandomStreamKind.Visual: return 0x5649_5355u;
                case RandomStreamKind.Reward: return 0x5550_4752u;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown random stream.");
            }
        }
    }
}
