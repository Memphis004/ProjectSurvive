using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Simulation.Battle;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// The Stage 07 acceptance benchmark: ~300 ghosts and ~500 projectiles, measured per
    /// tick, on .NET, with the machine it ran on written down next to the numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <b>measurement</b>, not a gate: a per-tick millisecond budget depends on
    /// the machine, so the only thing the test suite asserts at this load is that a tick
    /// allocates nothing (<c>EcsBattleRulesTests.The_acceptance_load_allocates_nothing_per_tick</c>).
    /// The numbers here go into <c>docs/stages/07-report.md</c> with the CPU and runtime
    /// they came from, because AGENTS.md forbids quoting a performance number that was not
    /// measured.
    /// </para>
    /// <para>
    /// The load is produced by the real pipeline: <see cref="EcsBattleOptions"/> seeds real
    /// ghosts and real projectiles, which the wave, steering, casting, movement, spatial
    /// hash, collision, damage, death and frame systems then process like anything else.
    /// What is measured is therefore the tick a player would get, carrying that much work.
    /// </para>
    /// <para>
    /// The measured window defaults to sitting <b>inside</b> a running floor (900 ticks of
    /// warm-up, 600 measured, against a 1800-tick floor), because the one thing a battle
    /// allocates is its <c>BattleResult</c> when the floor ends. A window that crosses the
    /// end of the floor says so in the report (<c>floorEndedAtTick</c>), so a non-zero
    /// allocation figure is never mistaken for a per-tick leak.
    /// </para>
    /// </remarks>
    internal static class BenchmarkMode
    {
        private const string ReportFloor = "floor.forest_1";

        /// <summary>HP for the benchmark player: high enough that the load cannot end the floor early.</summary>
        private const int BenchmarkHp = 1_000_000;

        internal static int Run(SimOptions options, IGameConfig config, TextWriter output)
        {
            FloorConfig floor = ConfigFixtureFloor(config, ReportFloor);
            SkillConfig volt = ConfigFixtureSkill(config, "skill.fire_bolt");

            var loadOptions = new EcsBattleOptions
            {
                MaintainLoadTestEntities = true,
                LoadTestEnemies = options.Enemies,
                LoadTestProjectiles = options.Projectiles,
            };

            var factory = new EcsBattleSessionFactory(config, loadOptions);
            IBattleSession session = factory.Create(Setup(config, floor, volt, options.Seed));
            var simulation = (EcsBattleSimulation)session;

            for (int i = 0; i < options.WarmupTicks; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            int enemies = simulation.Context.EnemiesAlive;
            int projectiles = simulation.Context.ProjectilesAlive;

            var samples = new double[options.Ticks];
            var tickEnemies = new int[options.Ticks];
            var tickProjectiles = new int[options.Ticks];
            long floorEndedAtTick = 0;

            // The instrument itself is allocated before the first sample is taken: a
            // measured window must not be charged for the thing measuring it.
            var clock = Stopwatch.StartNew();
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < options.Ticks; i++)
            {
                long start = clock.ElapsedTicks;
                session.Step(PlayerInputFrame.Idle);
                long elapsed = clock.ElapsedTicks - start;

                samples[i] = elapsed * 1000.0 / Stopwatch.Frequency;
                tickEnemies[i] = simulation.Context.EnemiesAlive;
                tickProjectiles[i] = simulation.Context.ProjectilesAlive;

                if (floorEndedAtTick == 0 && session.Phase != BattlePhase.Running)
                {
                    floorEndedAtTick = simulation.Context.Tick;
                }
            }

            clock.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);

            var stats = new TickStats
            {
                Ticks = options.Ticks,
                AverageMs = Mean(samples),
                P50Ms = Percentile(sorted, 0.50),
                P95Ms = Percentile(sorted, 0.95),
                P99Ms = Percentile(sorted, 0.99),
                MaxMs = sorted.Length > 0 ? sorted[sorted.Length - 1] : 0.0,
                AllocatedBytes = allocated,
                AllocatedBytesPerTick = options.Ticks > 0 ? (double)allocated / options.Ticks : 0.0,
                Enemies = enemies,
                Projectiles = projectiles,
                EnemiesAverage = Average(tickEnemies),
                EnemiesMax = Maximum(tickEnemies),
                ProjectilesAverage = Average(tickProjectiles),
                ProjectilesMax = Maximum(tickProjectiles),
                FloorEndedAtTick = floorEndedAtTick,
            };

            WriteCsv(options.OutputPath, options, stats, samples, tickEnemies, tickProjectiles);
            WriteReport(options, stats, output);
            return 0;
        }

        private static void WriteReport(SimOptions options, TickStats stats, TextWriter output)
        {
            output.WriteLine("ProjectSurvive.Sim (stage 07 benchmark)");
            output.WriteLine("seed=" + options.Seed.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("enemiesRequested=" + options.Enemies.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("projectilesRequested=" + options.Projectiles.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("enemiesAlive=" + stats.Enemies.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("projectilesAlive=" + stats.Projectiles.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("enemiesAliveAvg=" + Fixed(stats.EnemiesAverage));
            output.WriteLine("enemiesAliveMax=" + stats.EnemiesMax.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("projectilesAliveAvg=" + Fixed(stats.ProjectilesAverage));
            output.WriteLine("projectilesAliveMax=" + stats.ProjectilesMax.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("enemiesCreatedPerTick=" + options.Enemies.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("projectilesCreatedPerTick=" + options.Projectiles.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("warmupTicks=" + options.WarmupTicks.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("measuredTicks=" + stats.Ticks.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("tickMsAvg=" + Fixed(stats.AverageMs));
            output.WriteLine("tickMsP50=" + Fixed(stats.P50Ms));
            output.WriteLine("tickMsP95=" + Fixed(stats.P95Ms));
            output.WriteLine("tickMsP99=" + Fixed(stats.P99Ms));
            output.WriteLine("tickMsMax=" + Fixed(stats.MaxMs));
            output.WriteLine("allocatedBytesPerTick=" + Fixed(stats.AllocatedBytesPerTick));
            output.WriteLine("allocatedBytesTotal=" + stats.AllocatedBytes.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("floorEndedAtTick=" + stats.FloorEndedAtTick.ToString(CultureInfo.InvariantCulture));
            output.WriteLine("allocationNote=" + (stats.FloorEndedAtTick == 0
                ? "window sits inside a running floor; a battle allocates nothing per tick"
                : "window crossed the end of the floor, which allocates its BattleResult (docs/stages/07-report.md)"));
            output.WriteLine("benchmarkCsv=" + options.OutputPath);
            output.WriteLine("machine=" + MachineSummary());
        }

        private static void WriteCsv(
            string path,
            SimOptions options,
            TickStats stats,
            double[] samples,
            int[] enemies,
            int[] projectiles)
        {
            var csv = new StringBuilder();
            csv.Append("## ProjectSurvive Stage 07 load benchmark\n");
            csv.Append("## floor=").Append(ReportFloor).Append('\n');
            csv.Append("## seed=").Append(options.Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            csv.Append("## warmupTicks=").Append(options.WarmupTicks.ToString(CultureInfo.InvariantCulture)).Append('\n');
            csv.Append("## measuredTicks=").Append(stats.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\n');
            csv.Append("## tickMsAvg=").Append(Fixed(stats.AverageMs)).Append('\n');
            csv.Append("## tickMsP50=").Append(Fixed(stats.P50Ms)).Append('\n');
            csv.Append("## tickMsP95=").Append(Fixed(stats.P95Ms)).Append('\n');
            csv.Append("## tickMsP99=").Append(Fixed(stats.P99Ms)).Append('\n');
            csv.Append("## tickMsMax=").Append(Fixed(stats.MaxMs)).Append('\n');
            csv.Append("## allocatedBytesPerTick=").Append(Fixed(stats.AllocatedBytesPerTick)).Append('\n');
            csv.Append("## allocatedBytesTotal=").Append(stats.AllocatedBytes.ToString(CultureInfo.InvariantCulture)).Append('\n');
            csv.Append("## enemiesAliveAvg=").Append(Fixed(stats.EnemiesAverage)).Append('\n');
            csv.Append("## enemiesAliveMax=").Append(stats.EnemiesMax.ToString(CultureInfo.InvariantCulture)).Append('\n');
            csv.Append("## projectilesAliveAvg=").Append(Fixed(stats.ProjectilesAverage)).Append('\n');
            csv.Append("## projectilesAliveMax=").Append(stats.ProjectilesMax.ToString(CultureInfo.InvariantCulture)).Append('\n');
            csv.Append("## machine=").Append(MachineSummary()).Append('\n');
            csv.Append("tick,ms,enemies_alive,projectiles_alive\n");

            for (int i = 0; i < samples.Length; i++)
            {
                csv.Append(((i + 1) / BattleConstants.TickRate).ToString(CultureInfo.InvariantCulture)).Append('-');
                csv.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',');
                csv.Append(Fixed(samples[i])).Append(',');
                csv.Append(enemies[i].ToString(CultureInfo.InvariantCulture)).Append(',');
                csv.Append(projectiles[i].ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// The machine a number came from, in one line. AGENTS.md: a performance claim
        /// without the hardware it was measured on is not evidence.
        /// </summary>
        internal static string MachineSummary() =>
            Environment.ProcessorCount + " logical cores; "
            + RuntimeInformation.OSDescription.Trim() + "; "
            + RuntimeInformation.ProcessArchitecture + "; "
            + RuntimeInformation.FrameworkDescription + "; "
            + "cpu=" + Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");

        private static BattleSetup Setup(IGameConfig config, FloorConfig floor, SkillConfig bolt, uint seed)
        {
            var weapons = new List<WeaponLoadout>
            {
                new WeaponLoadout
                {
                    SkillId = bolt.Id,
                    Tier = 1,
                    Behavior = bolt.Behavior,
                    Element = bolt.Element,
                    CooldownSeconds = bolt.CooldownSeconds,
                    MpCost = bolt.MpCost,
                    Damage = bolt.Damage,
                    ProjectileSpeed = bolt.ProjectileSpeed,
                    Range = bolt.Range,
                    PierceCount = bolt.PierceCount,
                    ProjectileCount = bolt.ProjectileCount,
                },
            };

            return new BattleSetup
            {
                Seed = seed,
                StageId = floor.Id,
                DifficultyId = "difficulty.normal",
                FloorIndex = floor.Index,
                IsBossFloor = floor.IsBoss,
                CurrentHp = BenchmarkHp,
                DevCheatsAllowed = false,
                Loadout = new CombatLoadoutSnapshot
                {
                    Stats = new PlayerStatBlock
                    {
                        MaxHp = BenchmarkHp,
                        MaxMp = config.Constant.MaxMp,
                        MoveSpeed = config.Constant.MoveSpeed,
                        PickupRadius = config.Constant.PickupRadius,
                        FocusPerSecond = config.Constant.FocusPerSecond,
                    },
                    Weapons = weapons,
                },
            };
        }

        private static FloorConfig ConfigFixtureFloor(IGameConfig config, string id)
        {
            if (!config.TryGetFloor(id, out FloorConfig floor))
            {
                throw new InvalidOperationException("the floor table has no '" + id + "'");
            }

            return floor;
        }

        private static SkillConfig ConfigFixtureSkill(IGameConfig config, string id)
        {
            if (!config.TryGetSkill(id, out SkillConfig skill))
            {
                throw new InvalidOperationException("the skill table has no '" + id + "'");
            }

            return skill;
        }

        private static double Mean(double[] values)
        {
            if (values.Length == 0)
            {
                return 0.0;
            }

            double total = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }

            return total / values.Length;
        }

        private static double Average(int[] values)
        {
            if (values.Length == 0)
            {
                return 0.0;
            }

            long total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }

            return (double)total / values.Length;
        }

        private static int Maximum(int[] values)
        {
            int best = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > best)
                {
                    best = values[i];
                }
            }

            return best;
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 0)
            {
                return 0.0;
            }

            int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
            if (index < 0)
            {
                index = 0;
            }

            if (index >= sorted.Length)
            {
                index = sorted.Length - 1;
            }

            return sorted[index];
        }

        private static string Fixed(double value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

        private sealed class TickStats
        {
            internal int Ticks;

            internal double AverageMs;

            internal double P50Ms;

            internal double P95Ms;

            internal double P99Ms;

            internal double MaxMs;

            internal long AllocatedBytes;

            internal double AllocatedBytesPerTick;

            internal int Enemies;

            internal int Projectiles;

            internal double EnemiesAverage;

            internal int EnemiesMax;

            internal double ProjectilesAverage;

            internal int ProjectilesMax;

            internal long FloorEndedAtTick;
        }
    }
}
