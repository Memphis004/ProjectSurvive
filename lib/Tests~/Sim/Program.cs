using System;
using System.Globalization;
using ProjectSurvive.Core.Infrastructure.Config;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// Headless harness. Three modes share one binary:
    /// </summary>
    /// <list type="bullet">
    /// <item><description><b>streams</b> (default, Stage 01) — the fixed 30 Hz ticker over a
    /// synthetic frame source, drawing from the shop / battle / visual streams and printing
    /// a fingerprint. The same seed must always print the same fingerprint; that is the
    /// property tools/test.ps1 checks at process level.</description></item>
    /// <item><description><b>bot</b> (Stage 07) — complete runs with a kiting bot and a
    /// greedy camp policy, one row per floor in <c>artifacts/balance.csv</c>.</description></item>
    /// <item><description><b>benchmark</b> (Stage 07) — the acceptance load (~300 ghosts,
    /// ~500 projectiles) with per-tick timing and the GC allocation per tick.</description></item>
    /// </list>
    /// <remarks>
    /// The default mode, its flags and its exact output are unchanged from Stage 01: the
    /// determinism check compares two processes' <c>--quiet</c> lines byte for byte.
    /// </remarks>
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitBadArguments = 1;
        private const int ExitFailed = 2;

        private static int Main(string[] args)
        {
            SimOptions options;
            try
            {
                bool helpRequested;
                if (!SimOptions.TryParse(args, out options, out helpRequested))
                {
                    return helpRequested ? ExitOk : ExitBadArguments;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("bad arguments: " + ex.Message);
                Console.Error.WriteLine(SimOptions.Usage);
                return ExitBadArguments;
            }

            try
            {
                switch (options.Mode)
                {
                    case SimMode.Bot:
                        return BotRun.Run(options, LoadConfig(options), Console.Out);

                    case SimMode.Benchmark:
                        Console.WriteLine("machine=" + BenchmarkMode.MachineSummary());
                        return BenchmarkMode.Run(options, LoadConfig(options), Console.Out);

                    default:
                        return StreamsMode.Run(options);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("simulation failed: " + ex.Message);
                return ExitFailed;
            }
        }

        private static LubanGameConfig LoadConfig(SimOptions options)
        {
            LubanGameConfig config = SimEnvironment.LoadConfig(options.DataDirectory);
            Console.WriteLine(
                "data=" + SimEnvironment.DataDirectory(options.DataDirectory) + " tables="
                + config.Bags.Count.ToString(CultureInfo.InvariantCulture) + " bags, "
                + config.Items.Count.ToString(CultureInfo.InvariantCulture) + " items, "
                + config.Floors.Count.ToString(CultureInfo.InvariantCulture) + " floors");
            return config;
        }
    }
}
