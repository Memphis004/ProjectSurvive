using System;
using System.Globalization;
using ProjectSurvive.Core.Infrastructure;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// Headless deterministic harness (Stage 01).
    /// </summary>
    /// <remarks>
    /// Runs the fixed 30 Hz ticker over a synthetic frame source and draws from
    /// the shop / battle / visual streams, then prints a fingerprint of the
    /// sequence. The same seed must always print the same fingerprint; that is
    /// the property tools/test.ps1 checks at process level, and the base the
    /// Stage 07 bot simulation builds on.
    /// </remarks>
    internal static class Program
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int ExitOk = 0;
        private const int ExitBadArguments = 1;
        private const int ExitFailed = 2;

        private static int Main(string[] args)
        {
            Options options;
            try
            {
                bool helpRequested;
                if (!Options.TryParse(args, out options, out helpRequested))
                {
                    return helpRequested ? ExitOk : ExitBadArguments;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("bad arguments: " + ex.Message);
                Console.Error.WriteLine(Options.Usage);
                return ExitBadArguments;
            }

            try
            {
                return Run(options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("simulation failed: " + ex.Message);
                return ExitFailed;
            }
        }

        private static int Run(Options options)
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
                return ExitOk;
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
                return ExitFailed;
            }

            return ExitOk;
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

        private sealed class Options
        {
            public const string Usage =
                "usage: ProjectSurvive.Sim [--seed <uint>] [--ticks <int>] [--tick-hz <double>] "
                + "[--frame-hz <double>] [--shop-interval <int>] [--trace <int>] [--quiet] [--help]";

            public uint Seed { get; private set; }

            public int Ticks { get; private set; }

            public double TickHz { get; private set; }

            public double FrameHz { get; private set; }

            public int ShopIntervalTicks { get; private set; }

            public int Trace { get; private set; }

            public bool Quiet { get; private set; }

            public static bool TryParse(string[] args, out Options options, out bool helpRequested)
            {
                helpRequested = false;
                options = new Options
                {
                    Seed = 20261013u,
                    Ticks = 300,
                    TickHz = FixedTicker.DefaultTickHz,
                    FrameHz = 60.0,
                    ShopIntervalTicks = 30,
                    Trace = 0,
                    Quiet = false,
                };

                for (int i = 0; i < args.Length; i++)
                {
                    string key = args[i];
                    switch (key)
                    {
                        case "--help":
                        case "-h":
                            Console.WriteLine(Usage);
                            helpRequested = true;
                            return false;

                        case "--quiet":
                            options.Quiet = true;
                            break;

                        case "--seed":
                            options.Seed = uint.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                            break;

                        case "--ticks":
                            options.Ticks = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                            break;

                        case "--tick-hz":
                            options.TickHz = double.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                            break;

                        case "--frame-hz":
                            options.FrameHz = double.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                            break;

                        case "--shop-interval":
                            options.ShopIntervalTicks = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                            break;

                        case "--trace":
                            options.Trace = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                            break;

                        default:
                            Console.Error.WriteLine("unknown argument: " + key);
                            Console.Error.WriteLine(Usage);
                            return false;
                    }
                }

                if (options.Ticks < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(args), "--ticks must be at least 1");
                }

                if (options.TickHz <= 0.0 || options.FrameHz <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(args), "--tick-hz and --frame-hz must be positive");
                }

                if (options.ShopIntervalTicks < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(args), "--shop-interval must be at least 1");
                }

                if (options.Trace < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(args), "--trace cannot be negative");
                }

                return true;
            }

            private static string Next(string[] args, ref int index, string key)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("missing value for " + key);
                }

                index++;
                return args[index];
            }
        }
    }
}
