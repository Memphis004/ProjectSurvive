using System;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// AGENTS.md rule 6: randomness is separated into shop / battle / visual
    /// streams. Two properties have to hold — the same run seed replays
    /// identically, and using one stream never shifts another (so a shop roll
    /// cannot change how many attacks happen, and visual jitter cannot change
    /// either).
    /// </summary>
    public sealed class RandomStreamsTests
    {
        [Fact]
        public void Streams_are_derived_deterministically_from_the_master_seed()
        {
            Assert.Equal(0xDA9719AEu, RandomStreams.DeriveStreamSeed(12345u, RandomStreamKind.Shop));
            Assert.Equal(0x714AD7DDu, RandomStreams.DeriveStreamSeed(12345u, RandomStreamKind.Battle));
            Assert.Equal(0x80BB645Eu, RandomStreams.DeriveStreamSeed(12345u, RandomStreamKind.Visual));

            Assert.Equal(0xBEA5D8CEu, RandomStreams.DeriveStreamSeed(0u, RandomStreamKind.Shop));
            Assert.Equal(0x8C3893C6u, RandomStreams.DeriveStreamSeed(0u, RandomStreamKind.Battle));
            Assert.Equal(0xE6D55011u, RandomStreams.DeriveStreamSeed(0u, RandomStreamKind.Visual));
        }

        [Fact]
        public void Same_master_seed_reproduces_every_stream()
        {
            var first = new RandomStreams(20261013u);
            var second = new RandomStreams(20261013u);

            for (int i = 0; i < 512; i++)
            {
                Assert.Equal(first.Shop.NextUInt(), second.Shop.NextUInt());
                Assert.Equal(first.Battle.NextUInt(), second.Battle.NextUInt());
                Assert.Equal(first.Visual.NextUInt(), second.Visual.NextUInt());
            }
        }

        [Fact]
        public void Drawing_from_one_stream_does_not_affect_the_others()
        {
            var drained = new RandomStreams(4242u);
            var reference = new RandomStreams(4242u);

            // A long shop session, on one instance only.
            for (int i = 0; i < 10_000; i++)
            {
                drained.Shop.NextUInt();
            }

            // Battle and visual must be exactly where they would have been.
            for (int i = 0; i < 256; i++)
            {
                Assert.Equal(reference.Battle.NextUInt(), drained.Battle.NextUInt());
                Assert.Equal(reference.Visual.NextUInt(), drained.Visual.NextUInt());
            }

            // The shop stream is the same sequence, just consumed 10_000 draws
            // further along - draining it changed nothing about its own values.
            for (int i = 0; i < 10_000; i++)
            {
                reference.Shop.NextUInt();
            }

            for (int i = 0; i < 64; i++)
            {
                Assert.Equal(reference.Shop.NextUInt(), drained.Shop.NextUInt());
            }
        }

        [Fact]
        public void Streams_do_not_produce_the_same_values()
        {
            var streams = new RandomStreams(12345u);

            for (int i = 0; i < 32; i++)
            {
                uint shop = streams.Shop.NextUInt();
                uint battle = streams.Battle.NextUInt();
                uint visual = streams.Visual.NextUInt();

                Assert.NotEqual(shop, battle);
                Assert.NotEqual(shop, visual);
                Assert.NotEqual(battle, visual);
            }
        }

        [Fact]
        public void Different_master_seeds_produce_different_streams()
        {
            var a = new RandomStreams(1u);
            var b = new RandomStreams(2u);

            int differences = 0;
            for (int i = 0; i < 64; i++)
            {
                if (a.Battle.NextUInt() != b.Battle.NextUInt())
                {
                    differences++;
                }
            }

            Assert.Equal(64, differences);
        }

        [Fact]
        public void Stream_kinds_have_distinct_seeds_for_the_same_master_seed()
        {
            var seeds = new[]
            {
                RandomStreams.DeriveStreamSeed(7u, RandomStreamKind.Shop),
                RandomStreams.DeriveStreamSeed(7u, RandomStreamKind.Battle),
                RandomStreams.DeriveStreamSeed(7u, RandomStreamKind.Visual),
            };

            Assert.Equal(3, seeds.Length);
            Assert.NotEqual(seeds[0], seeds[1]);
            Assert.NotEqual(seeds[1], seeds[2]);
            Assert.NotEqual(seeds[0], seeds[2]);
        }

        [Fact]
        public void DeriveStreamSeed_rejects_an_unknown_kind()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RandomStreams.DeriveStreamSeed(1u, (RandomStreamKind)999));
        }

        [Fact]
        public void Captured_stream_state_resumes_every_stream_where_it_stopped()
        {
            var original = new RandomStreams(20261021u);
            for (int i = 0; i < 5; i++)
            {
                original.Shop.NextUInt();
            }

            original.Battle.NextUInt();
            original.Visual.NextUInt();

            RandomStreamsState state = original.CaptureState();

            var restored = new RandomStreams(20261021u);
            restored.RestoreState(state);

            for (int i = 0; i < 128; i++)
            {
                Assert.Equal(original.Shop.NextUInt(), restored.Shop.NextUInt());
                Assert.Equal(original.Battle.NextUInt(), restored.Battle.NextUInt());
                Assert.Equal(original.Visual.NextUInt(), restored.Visual.NextUInt());
            }

        }

        [Fact]
        public void Stream_state_from_another_seed_is_refused()
        {
            RandomStreamsState state = new RandomStreams(1u).CaptureState();
            var other = new RandomStreams(2u);

            Assert.Throws<ArgumentException>(() => other.RestoreState(state));
        }

        [Fact]
        public void Master_seed_is_kept_for_diagnostics()
        {
            var streams = new RandomStreams(0xABCDEF01u);
            Assert.Equal(0xABCDEF01u, streams.MasterSeed);
        }
    }
}
