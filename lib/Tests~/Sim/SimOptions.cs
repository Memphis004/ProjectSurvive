using System;
using System.Globalization;

namespace ProjectSurvive.Sim
{
    /// <summary>Which harness to run.</summary>
    internal enum SimMode
    {
        /// <summary>The Stage 01 clock and stream fingerprint (the default).</summary>
        Streams = 0,

        /// <summary>The Stage 07 bot: a full run, kiting in battle and greedy at camp.</summary>
        Bot = 1,

        /// <summary>The Stage 07 load benchmark: acceptance load, per-tick timing and GC.</summary>
        Benchmark = 2,
    }

    /// <summary>
    /// The command line of <c>ProjectSurvive.Sim</c>.
    /// </summary>
    /// <remarks>
    /// Every mode keeps the argument names it had before, so the existing harness checks
    /// (<c>--seed</c>, <c>--ticks</c>, <c>--quiet</c>) behave exactly as they did in Stage
    /// 01: <c>--quiet</c> still prints one <c>determinismDigest=</c> line, which is what
    /// tools/test.ps1 compares between two processes.
    /// </remarks>
    internal sealed class SimOptions
    {
        internal const string Usage =
            "usage: ProjectSurvive.Sim [--mode streams|bot|benchmark] [options]\n"
            + "  streams (default): the Stage 01 fixed-tick / random-stream fingerprint\n"
            + "    --seed <uint>            run seed (default 20261013)\n"
            + "    --ticks <int>            ticks to produce (default 300)\n"
            + "    --tick-hz <double>       simulation rate (default 30)\n"
            + "    --frame-hz <double>      frame rate feeding the ticker (default 60)\n"
            + "    --shop-interval <int>    ticks between shop draws (default 30)\n"
            + "    --trace <int>            print the first N ticks\n"
            + "    --quiet                  print only determinismDigest=0x...\n"
            + "  bot: play complete runs headlessly\n"
            + "    --seed <uint>            first seed (default 20261013)\n"
            + "    --seeds <int>            how many consecutive seeds to play (default 1)\n"
            + "    --out <path>             balance CSV (default artifacts/balance.csv)\n"
            + "    --max-steps <int>        per-floor step guard (default 20000)\n"
            + "    --build <name>           camp plan: default|ranged|melee|familiar (default default)\n"
            + "    --quiet                  do not print the per-run progress lines\n"
            + "  benchmark: the Stage 07 acceptance load\n"
            + "    --enemies <int>          load ghosts (default 300)\n"
            + "    --projectiles <int>      load projectiles (default 500)\n"
            + "    --ticks <int>            measured ticks (default 600)\n"
            + "    --warmup <int>           warm-up ticks before measuring (default 900)\n"
            + "    --out <path>             benchmark CSV (default artifacts/benchmark.csv)\n"
            + "  common:\n"
            + "    --data <dir>             generated table folder (default lib/Tests~/data)\n"
            + "    --help                   this text";

        internal SimMode Mode { get; private set; } = SimMode.Streams;

        internal uint Seed { get; private set; } = 20261013u;

        internal int Seeds { get; private set; } = 1;

        internal int Ticks { get; private set; } = 300;

        internal double TickHz { get; private set; } = 30.0;

        internal double FrameHz { get; private set; } = 60.0;

        internal int ShopIntervalTicks { get; private set; } = 30;

        internal int Trace { get; private set; }

        internal bool Quiet { get; private set; }

        internal string DataDirectory { get; private set; } = string.Empty;

        internal string OutputPath { get; private set; } = string.Empty;

        /// <summary>What the bot buys towards (Stage 11d); <see cref="BuildPlan.Default"/> when unset.</summary>
        internal BuildPlan Build { get; private set; } = BuildPlan.Default;

        internal int MaxSteps { get; private set; } = 20_000;

        internal int Enemies { get; private set; } = 300;

        internal int Projectiles { get; private set; } = 500;

        internal int WarmupTicks { get; private set; } = 900;

        internal static bool TryParse(string[] args, out SimOptions options, out bool helpRequested)
        {
            helpRequested = false;
            var parsed = new SimOptions();
            bool ticksGiven = false;

            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                switch (key)
                {
                    case "--help":
                    case "-h":
                        Console.WriteLine(Usage);
                        helpRequested = true;
                        options = parsed;
                        return false;

                    case "--quiet":
                        parsed.Quiet = true;
                        break;

                    case "--mode":
                        string mode = Next(args, ref i, key);
                        switch (mode)
                        {
                            case "streams":
                                parsed.Mode = SimMode.Streams;
                                break;
                            case "bot":
                                parsed.Mode = SimMode.Bot;
                                break;
                            case "benchmark":
                                parsed.Mode = SimMode.Benchmark;
                                break;
                            default:
                                Console.Error.WriteLine("unknown mode: " + mode);
                                Console.Error.WriteLine(Usage);
                                options = parsed;
                                return false;
                        }

                        break;

                    case "--seed":
                        parsed.Seed = uint.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--seeds":
                        parsed.Seeds = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--ticks":
                        parsed.Ticks = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        ticksGiven = true;
                        break;

                    case "--tick-hz":
                        parsed.TickHz = double.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--frame-hz":
                        parsed.FrameHz = double.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--shop-interval":
                        parsed.ShopIntervalTicks = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--trace":
                        parsed.Trace = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--data":
                        parsed.DataDirectory = Next(args, ref i, key);
                        break;

                    case "--out":
                        parsed.OutputPath = Next(args, ref i, key);
                        break;

                    case "--build":
                        string build = Next(args, ref i, key);
                        BuildPlan? plan = BuildPlan.ByName(build);
                        if (plan is null)
                        {
                            Console.Error.WriteLine("unknown build: " + build);
                            Console.Error.WriteLine(Usage);
                            options = parsed;
                            return false;
                        }

                        parsed.Build = plan;
                        break;

                    case "--max-steps":
                        parsed.MaxSteps = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--enemies":
                        parsed.Enemies = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--projectiles":
                        parsed.Projectiles = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    case "--warmup":
                        parsed.WarmupTicks = int.Parse(Next(args, ref i, key), CultureInfo.InvariantCulture);
                        break;

                    default:
                        Console.Error.WriteLine("unknown argument: " + key);
                        Console.Error.WriteLine(Usage);
                        options = parsed;
                        return false;
                }
            }

            // The two multi-tick modes have their own useful defaults; --ticks is the
            // streams harness's knob and only overrides them when it was actually given.
            if (parsed.Mode == SimMode.Benchmark && !ticksGiven)
            {
                parsed.Ticks = 600;
            }

            if (parsed.Seeds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--seeds must be at least 1");
            }

            if (parsed.Ticks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--ticks must be at least 1");
            }

            if (parsed.TickHz <= 0.0 || parsed.FrameHz <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--tick-hz and --frame-hz must be positive");
            }

            if (parsed.ShopIntervalTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--shop-interval must be at least 1");
            }

            if (parsed.Trace < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--trace cannot be negative");
            }

            if (parsed.MaxSteps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--max-steps must be at least 1");
            }

            if (parsed.Enemies < 0 || parsed.Projectiles < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--enemies and --projectiles cannot be negative");
            }

            if (parsed.WarmupTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--warmup cannot be negative");
            }

            if (parsed.OutputPath.Length == 0)
            {
                string name = parsed.Mode == SimMode.Benchmark ? "benchmark.csv" : "balance.csv";
                parsed.OutputPath = SimEnvironment.OutputPath(System.IO.Path.Combine("artifacts", name));
            }
            else
            {
                parsed.OutputPath = SimEnvironment.OutputPath(parsed.OutputPath);
            }

            options = parsed;
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
