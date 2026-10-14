using System;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Tests.Run
{
    /// <summary>
    /// Builds runs and scripted battle factories for the Stage 06 suite.
    /// </summary>
    /// <remarks>
    /// Everything runs over the <b>real</b> generated tables, exactly like the Stage 05
    /// suite: the shop's prices, the pool's floor bands, the heal percentage and the
    /// wallet's start are all read from <c>data/</c>, so retuning them cannot make a
    /// correct rule look broken — and an assertion that hard-codes a balance number
    /// would be the thing that breaks.
    /// </remarks>
    internal static class RunFixture
    {
        /// <summary>A seed that produces four distinct floor-1 offers.</summary>
        internal const uint DefaultSeed = 20261014u;

        /// <summary>A fresh, mutable config snapshot.</summary>
        internal static IGameConfig Config() => ConfigFixture.Load();

        /// <summary>A run that has not started yet (still at Title).</summary>
        internal static RunSession NewRun(IGameConfig config, uint seed) =>
            new RunSession(config, new MessagePackBackpackSnapshotCodec(), seed);

        /// <summary>A run that has started and is sitting in the first Camp.</summary>
        internal static RunSession StartedRun(IGameConfig config, uint seed = DefaultSeed)
        {
            RunSession run = NewRun(config, seed);
            Result started = run.StartRun();
            if (started.IsFailure)
            {
                throw new InvalidOperationException("fixture could not start the run: " + started.Error);
            }

            return run;
        }

        /// <summary>The offered id of every shop slot, in panel order.</summary>
        internal static string[] StockIds(RunSession run)
        {
            var ids = new string[run.Stock.SlotCount];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = run.Stock[i].OfferedId;
            }

            return ids;
        }

        /// <summary>
        /// The still-available slot with the lowest price — what a player with 15 bia
        /// would rent first. Throws when nothing is available.
        /// </summary>
        internal static int CheapestAvailableSlot(RunSession run)
        {
            int best = -1;
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                if (run.Stock[i].IsEmpty || run.Stock[i].IsPurchased)
                {
                    continue;
                }

                if (best < 0 || run.Stock[i].Price < run.Stock[best].Price)
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                throw new InvalidOperationException("the shop has nothing available to rent.");
            }

            return best;
        }

        /// <summary>The run's starting weapon (<c>item.kali_flame_yant</c>) by handle.</summary>
        internal static PieceRef StartingWeapon(RunSession run)
        {
            for (int i = 0; i < run.Backpack.Items.Count; i++)
            {
                if (run.Backpack.Items[i].DefId == RunDefaults.InitialWeaponId)
                {
                    return run.Backpack.Items[i].Ref;
                }
            }

            throw new InvalidOperationException("the run has no starting weapon.");
        }

        /// <summary>The run's starting bag by handle.</summary>
        internal static PieceRef StartingBag(RunSession run)
        {
            for (int i = 0; i < run.Backpack.Bags.Count; i++)
            {
                if (run.Backpack.Bags[i].DefId == RunDefaults.InitialBagId)
                {
                    return run.Backpack.Bags[i].Ref;
                }
            }

            throw new InvalidOperationException("the run has no starting bag.");
        }
    }

    /// <summary>
    /// A fake factory that can be told what happens on a normal floor and what happens
    /// on the boss floor.
    /// </summary>
    /// <remarks>
    /// The test's own implementation of the port, on purpose: it proves the run only
    /// needs <c>IBattleSessionFactory</c> — the same seam the EcsLite simulation will
    /// fill at Stage 07 — and lets one scripted run win four floors and then time out
    /// against the boss.
    /// </remarks>
    internal sealed class ScriptedBattleSessionFactory : IBattleSessionFactory
    {
        private readonly FakeBattleSessionFactory _normal;
        private readonly FakeBattleSessionFactory _boss;

        /// <summary>Creates a factory. Both scripts default to \"win on the first tick\".</summary>
        internal ScriptedBattleSessionFactory(
            IGameConfig config, FakeBattleOptions? normal = null, FakeBattleOptions? boss = null)
        {
            _normal = new FakeBattleSessionFactory(config, normal ?? WinOnFirstTick());
            _boss = new FakeBattleSessionFactory(config, boss ?? WinOnFirstTick());
        }

        /// <inheritdoc />
        public IBattleSession Create(BattleSetup setup) =>
            (setup.IsBossFloor ? _boss : _normal).Create(setup);

        /// <summary>An option set that ends the floor on its first tick with a win.</summary>
        internal static FakeBattleOptions WinOnFirstTick(int dummyEntities = 4) =>
            new FakeBattleOptions { WinAtTick = 1, DummyEntityCount = dummyEntities, EntityLifetimeTicks = 8 };

        /// <summary>An option set that ends the floor on its first tick with a timeout loss.</summary>
        internal static FakeBattleOptions TimeoutOnFirstTick() =>
            new FakeBattleOptions { TimeoutAtTick = 1, DummyEntityCount = 0 };
    }
}
