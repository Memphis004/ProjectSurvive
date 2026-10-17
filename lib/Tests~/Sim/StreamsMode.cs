using System;
using System.Globalization;
using ProjectSurvive.Core.Infrastructure;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// The headless determinism harness: the fixed ticker over a synthetic frame source, drawing
    /// from the run / encounter / visual streams and printing a fingerprint of each sequence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Output is what tools/test.ps1 compares at process level: two runs of one seed must print
    /// the same <c>determinismDigest</c>, a different seed must print a different one, and the
    /// three per-stream digests must differ from each other — otherwise they are one stream
    /// wearing three names (AGENTS rule 6).
    /// </para>
    /// <para>
    /// This mode allocates nothing per tick beyond the strings it prints at the end; it exists to
    /// be a small, readable reference for what a fixed tick looks like, not a benchmark.
    /// </para>
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

            uint encounterDigest = FnvOffsetBasis;
            uint visualDigest = FnvOffsetBasis;
            uint runDigest = FnvOffsetBasis;
            uint sequenceDigest = FnvOffsetBasis;
            long runDraws = 0;
            long encounterDraws = 0;
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
                    // Encounter stream: drawn every tick, drives play outcomes.
                    uint encounterValue = streams.Encounter.NextUInt();
                    encounterDigest = Fnv1a(encounterDigest, encounterValue);
                    encounterDraws++;

                    // Visual stream: drawn every tick, must never be read back by rules.
                    uint visualValue = streams.Visual.NextUInt();
                    visualDigest = Fnv1a(visualDigest, visualValue);
                    visualDraws++;

                    // Run stream: drawn on its own schedule, independent of the two above.
                    if (producedTicks % options.RunIntervalTicks == 0)
                    {
                        uint runValue = streams.Run.NextUInt();
                        runDigest = Fnv1a(runDigest, runValue);
                        runDraws++;
                    }

                    sequenceDigest = Fnv1a(sequenceDigest, encounterValue);
                    sequenceDigest = Fnv1a(sequenceDigest, visualValue);

                    if (options.Trace > 0 && producedTicks < options.Trace)
                    {
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "trace tick={0} encounter={1} visual={2}",
                            producedTicks, encounterValue, visualValue));
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

            Console.WriteLine("ProjectSurvive.Sim (template)");
            Console.WriteLine("seed=" + options.Seed.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("tickHz=" + Format(options.TickHz));
            Console.WriteLine("frameHz=" + Format(options.FrameHz));
            Console.WriteLine("ticksRequested=" + targetTicks.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("ticksProduced=" + producedTicks.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("frames=" + frames.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("droppedTicks=" + ticker.DroppedTicks.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("encounterDraws=" + encounterDraws.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("visualDraws=" + visualDraws.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("runDraws=" + runDraws.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("encounterDigest=" + Hex(encounterDigest));
            Console.WriteLine("visualDigest=" + Hex(visualDigest));
            Console.WriteLine("runDigest=" + Hex(runDigest));
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
