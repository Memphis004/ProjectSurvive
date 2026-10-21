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
    /// battle simulation relies on.
    /// </remarks>
    public sealed class RandomStreams
    {
        /// <summary>Creates the shop / battle / visual streams for a run seed.</summary>
        public RandomStreams(uint masterSeed)
        {
            MasterSeed = masterSeed;
            _shop = new Xoshiro128(DeriveStreamSeed(masterSeed, RandomStreamKind.Shop));
            _battle = new Xoshiro128(DeriveStreamSeed(masterSeed, RandomStreamKind.Battle));
            _visual = new Xoshiro128(DeriveStreamSeed(masterSeed, RandomStreamKind.Visual));
        }

        private readonly Xoshiro128 _shop;
        private readonly Xoshiro128 _battle;
        private readonly Xoshiro128 _visual;

        /// <summary>The run seed every stream was derived from.</summary>
        public uint MasterSeed { get; }

        /// <summary>Camp-side stream.</summary>
        public IRandomSource Shop => _shop;

        /// <summary>Combat stream.</summary>
        public IRandomSource Battle => _battle;

        /// <summary>Cosmetic stream.</summary>
        public IRandomSource Visual => _visual;

        /// <summary>
        /// Every stream's position, as plain values, for a checkpoint (Stage 12).
        /// </summary>
        /// <remarks>
        /// The <b>position</b> is what matters, not only the seed: a shop stream that has
        /// already rolled two camps must continue after the two rolls, or a reloaded run
        /// would be offered the pieces the player already saw (and the acceptance test
        /// "reload ไม่ทำให้สุ่มร้านใหม่" would be false).
        /// </remarks>
        public RandomStreamsState CaptureState() => new RandomStreamsState(
            MasterSeed, _shop.ExportState(), _battle.ExportState(), _visual.ExportState());

        /// <summary>
        /// Restores every stream to a previously captured position. The master seed must
        /// match this instance's seed: a state from another run is a corrupt checkpoint,
        /// not a stream swap.
        /// </summary>
        /// <exception cref="ArgumentException">The master seed differs, or a stream state is all zero.</exception>
        public void RestoreState(RandomStreamsState state)
        {
            if (state.MasterSeed != MasterSeed)
            {
                throw new ArgumentException(
                    "Random stream state belongs to seed " + state.MasterSeed + ", not " + MasterSeed + ".",
                    nameof(state));
            }

            if (state.Shop.IsZero || state.Battle.IsZero || state.Visual.IsZero)
            {
                throw new ArgumentException("A random stream state cannot be all zero.", nameof(state));
            }

            _shop.ImportState(state.Shop);
            _battle.ImportState(state.Battle);
            _visual.ImportState(state.Visual);
        }

        /// <remarks>
        /// There is deliberately <b>no</b> <c>Upgrade</c> stream property: the upgrade
        /// draw is <see cref="DeriveStreamSeed"/> with <see cref="RandomStreamKind.Upgrade"/>,
        /// never a stateful stream, so an offer cannot depend on how many offers came
        /// before it (ADR 0021).
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
                case RandomStreamKind.Shop: return 0x5348_4F50u;
                case RandomStreamKind.Battle: return 0x4241_5454u;
                case RandomStreamKind.Visual: return 0x5649_5355u;
                case RandomStreamKind.Upgrade: return 0x5550_4752u;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown random stream.");
            }
        }
    }

    /// <summary>
    /// The run's three random streams at one moment (Stage 12).
    /// </summary>
    /// <remarks>
    /// The upgrade draw is deliberately absent: it is a pure function of the seed, so it
    /// has no position to save (see <see cref="RandomStreams.DeriveStreamSeed"/>).
    /// </remarks>
    public readonly struct RandomStreamsState : IEquatable<RandomStreamsState>
    {
        /// <summary>Captures the master seed and all three stream positions.</summary>
        public RandomStreamsState(
            uint masterSeed, Xoshiro128State shop, Xoshiro128State battle, Xoshiro128State visual)
        {
            MasterSeed = masterSeed;
            Shop = shop;
            Battle = battle;
            Visual = visual;
        }

        /// <summary>The seed this state belongs to.</summary>
        public uint MasterSeed { get; }

        /// <summary>Camp-side stream position.</summary>
        public Xoshiro128State Shop { get; }

        /// <summary>Combat stream position.</summary>
        public Xoshiro128State Battle { get; }

        /// <summary>Cosmetic stream position.</summary>
        public Xoshiro128State Visual { get; }

        /// <inheritdoc />
        public bool Equals(RandomStreamsState other) =>
            MasterSeed == other.MasterSeed && Shop == other.Shop && Battle == other.Battle
            && Visual == other.Visual;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is RandomStreamsState other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            (int)(MasterSeed * 397u) ^ Shop.GetHashCode() ^ (Battle.GetHashCode() * 17)
            ^ (Visual.GetHashCode() * 31);

        public static bool operator ==(RandomStreamsState left, RandomStreamsState right) => left.Equals(right);

        public static bool operator !=(RandomStreamsState left, RandomStreamsState right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => "streams(" + MasterSeed + ")";
    }
}
