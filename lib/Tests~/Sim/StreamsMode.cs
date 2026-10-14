using System;
using System.Globalization;
using ProjectSurvive.Core.Infrastructure;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// The Stage 01 headless determinism harness: the fixed 30 Hz ticker over a synthetic
    /// frame source, drawing from the shop / battle / visual streams and printing a
    /// fingerprint of the sequence.
    /// </summary>
    /// <remarks>
    /// Output is byte-for-byte what it was before the bot mode existed, because
    /// tools/test.ps1 compares two processes' <c>--quiet</c> output and greps
    /// <c>ticksProduced=</c> from the full one. The Stage 07 bot grew <i>next to</i> this,
    /// not on top of it.
    /// </remarks>
    internal static class StreamsMode
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        internal static int Run(SimOptions options)
        {
            var streams = new RandomStreams(options.Seed);
            var ticker = new FixedTicker(options.TickHz);
            var clock = new SystemClock();

            uint battleDigest = FnvOffsetBasis;
            uint visualDigest = FnvOffsetBasis;
            uint shopDigest = FnvOffsetBasis;
            uint sequenceDigest = FnvOffsetBasis;
            long shopDraws = 0;
            long battleDraws = 0;
            long visualDraws = 0;
            long frames = 0;

            double frameSeconds = 1.0 / options.FrameHz;
            long targetTicks = options.Ticks;
            long producedTicks = 0;
            long maxFrames = targetTicks * (long)Math.Ceiling(options.FrameHz / options.TickHz) + 4;

            while (producedTicks < targetTicks && frames < maxFrames)
            {
                frames++;
                int due = ticker.Advance(frameSeconds);
                for (int i = 0; i < due && producedTicks < targetTicks; i++)
                {
                    // Battle stream: drawn every tick, drives combat outcomes.
                    uint battleValue = streams.Battle.NextUInt();
                    battleDigest = Fnv1a(battleDigest, battleValue);
                    battleDraws++;

                    // Visual stream: drawn every tick, must never be read back by rules.
                    uint visualValue = streams.Visual.NextUInt();
                    visualDigest = Fnv1a(visualDigest, visualValue);
                    visualDraws++;

                    // Shop stream: drawn on its own schedule, independent of the two above.
                    if (producedTicks % options.ShopIntervalTicks == 0)
                    {
                        uint shopValue = streams.Shop.NextUInt();
                        shopDigest = Fnv1a(shopDigest, shopValue);
                        shopDraws++;
                    }

                    sequenceDigest = Fnv1a(sequenceDigest, battleValue);
                    sequenceDigest = Fnv1a(sequenceDigest, visualValue);

                    if (options.Trace > 0 && producedTicks < options.Trace)
                    {
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "trace tick={0} battle={1} visual={2}",
                            producedTicks, battleValue, visualValue));
                    }

                    producedTicks++;
                }
            }

            double elapsedMilliseconds = clock.NowSeconds * 1000.0;

            if (options.Quiet)
            {
                Console.WriteLine("determinismDigest=" + Hex(sequenceDigest));
                return 0;
            }

            Console.WriteLine("ProjectSurvive.Sim (stage 01)");
            Console.WriteLine("seed=" + options.Seed.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("tickHz=" + Format(options.TickHz));
            Console.WriteLine("frameHz=" + Format(options.FrameHz));
            Console.WriteLine("ticksRequested=" + targetTicks.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("ticksProduced=" + producedTicks.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("frames=" + frames.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("droppedTicks=" + ticker.DroppedTicks.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("battleDraws=" + battleDraws.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("visualDraws=" + visualDraws.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("shopDraws=" + shopDraws.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("battleDigest=" + Hex(battleDigest));
            Console.WriteLine("visualDigest=" + Hex(visualDigest));
            Console.WriteLine("shopDigest=" + Hex(shopDigest));
            Console.WriteLine("determinismDigest=" + Hex(sequenceDigest));
            Console.WriteLine("elapsedMs=" + Format(elapsedMilliseconds) + " (not part of determinism)");

            if (producedTicks < targetTicks)
            {
                Console.WriteLine("note=produced fewer ticks than requested; raise --max-frames or check --frame-hz");
                return 2;
            }

            return 0;
        }

        private static uint Fnv1a(uint hash, uint value)
        {
            unchecked
            {
                hash = (hash ^ (value & 0xFFu)) * FnvPrime;
                hash = (hash ^ ((value >> 8) & 0xFFu)) * FnvPrime;
                hash = (hash ^ ((value >> 16) & 0xFFu)) * FnvPrime;
                hash = (hash ^ ((value >> 24) & 0xFFu)) * FnvPrime;
                return hash;
            }
        }

        private static string Hex(uint value) => "0x" + value.ToString("x8", CultureInfo.InvariantCulture);

        private static string Format(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
