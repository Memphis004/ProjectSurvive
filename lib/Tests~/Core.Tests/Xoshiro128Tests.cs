using System;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The generator must be bit-for-bit reproducible: the battle simulation
    /// replays and the Stage 07 bot depend on the same seed always producing the
    /// same sequence.
    /// </summary>
    public sealed class Xoshiro128Tests
    {
        [Fact]
        public void Same_seed_produces_the_same_sequence()
        {
            var first = new Xoshiro128(12345u);
            var second = new Xoshiro128(12345u);

            for (int i = 0; i < 4096; i++)
            {
                Assert.Equal(first.NextUInt(), second.NextUInt());
            }
        }

        [Fact]
        public void Different_seeds_produce_different_sequences()
        {
            var a = new Xoshiro128(1u);
            var b = new Xoshiro128(2u);

            int differences = 0;
            for (int i = 0; i < 64; i++)
            {
                if (a.NextUInt() != b.NextUInt())
                {
                    differences++;
                }
            }

            Assert.Equal(64, differences);
        }

        [Fact]
        public void Reseed_restarts_the_same_sequence()
        {
            var generator = new Xoshiro128(777u);
            uint first = generator.NextUInt();
            generator.NextUInt();
            generator.NextUInt();

            generator.Reseed(777u);

            Assert.Equal(777u, generator.Seed);
            Assert.Equal(first, generator.NextUInt());
        }

        [Fact]
        public void Matches_independent_reference_vectors()
        {
            // Reference values produced by a separate implementation of
            // xoshiro128++ with splitmix32 seeding (see docs/stages/01-report.md).
            // They lock the sequence: any accidental change to the algorithm,
            // the seeding, or the mixing order breaks this test.
            AssertSequence(0u, new[]
            {
                0x69CEF2A4u, 0x89959840u, 0xEEB93EBEu, 0x99849937u,
                0x28EE3191u, 0xB6D970E1u, 0xD0E6A894u, 0x6C4190D3u,
            });

            AssertSequence(12345u, new[]
            {
                0x00791D8Au, 0xB3117E0Eu, 0x95635769u, 0xD030B7CBu,
                0xDA166145u, 0x2E6DE0ABu, 0xB3361CB7u, 0x1765464Eu,
            });

            AssertSequence(0xDEADBEEFu, new[]
            {
                0x9E44240Au, 0x80125291u, 0x3AC65CF5u, 0x4F1D3A99u,
                0x58B7E90Eu, 0x3551963Au, 0xECE14504u, 0x2FD48D60u,
            });
        }

        [Fact]
        public void Seed_zero_is_still_a_live_stream()
        {
            var generator = new Xoshiro128(0u);

            uint first = generator.NextUInt();
            Assert.NotEqual(0u, first);

            // A generator stuck on a zero state would return 0 forever.
            for (int i = 0; i < 256; i++)
            {
                Assert.NotEqual(0u, generator.NextUInt());
            }
        }

        [Fact]
        public void NextInt_stays_in_range_and_eventually_covers_it()
        {
            var generator = new Xoshiro128(20261013u);
            var seen = new bool[6];

            for (int i = 0; i < 100_000; i++)
            {
                int value = generator.NextInt(0, 6);
                Assert.InRange(value, 0, 5);
                seen[value] = true;
            }

            Assert.All(seen, value => Assert.True(value, "every value in [0, 6) must be reachable"));
        }

        [Fact]
        public void NextInt_matches_the_reference_vector()
        {
            var generator = new Xoshiro128(7u);
            int[] expected = { 40, 53, 79, 61, 8, 93 };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], generator.NextInt(0, 100));
            }
        }

        [Fact]
        public void NextInt_handles_a_single_value_range()
        {
            var generator = new Xoshiro128(4u);
            for (int i = 0; i < 32; i++)
            {
                Assert.Equal(-3, generator.NextInt(-3, -2));
            }
        }

        [Fact]
        public void NextInt_supports_the_full_int_range()
        {
            var generator = new Xoshiro128(99u);
            for (int i = 0; i < 1000; i++)
            {
                int value = generator.NextInt(int.MinValue, int.MaxValue);
                Assert.InRange(value, int.MinValue, int.MaxValue - 1);
            }
        }

        [Fact]
        public void NextInt_rejects_an_empty_or_inverted_range()
        {
            var generator = new Xoshiro128(1u);
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextInt(5, 4));
        }

        [Fact]
        public void NextFloat_is_in_the_unit_interval_and_matches_the_reference()
        {
            var generator = new Xoshiro128(12345u);
            float first = generator.NextFloat();
            float second = generator.NextFloat();
            float third = generator.NextFloat();

            Assert.Equal(0.0018480420f, first, 6);
            Assert.Equal(0.6994856596f, second, 6);
            Assert.Equal(0.5835470557f, third, 6);

            var other = new Xoshiro128(4242u);
            for (int i = 0; i < 10_000; i++)
            {
                float value = other.NextFloat();
                Assert.InRange(value, 0.0f, 0.99999994f);
            }
        }

        [Fact]
        public void NextInt_rejects_a_null_source()
        {
            IRandomSource? source = null;
            Assert.Throws<ArgumentNullException>(() => source!.NextInt(0, 10));
            Assert.Throws<ArgumentNullException>(() => source!.NextFloat());
        }

        private static void AssertSequence(uint seed, uint[] expected)
        {
            var generator = new Xoshiro128(seed);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], generator.NextUInt());
            }
        }
    }
}
