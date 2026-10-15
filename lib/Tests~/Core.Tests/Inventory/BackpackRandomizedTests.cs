using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05's property test: 10,000 randomized commands against one backpack, with
    /// every invariant checked after each one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk only uses the command surface (<see cref="BackpackEditor"/>), so it cannot
    /// reach around a validation into the state. After every command it asserts:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Backpack.TryValidate"/> — bags never overlap, every item stands on an
    /// ActiveSlot that names it, chest slots and piece zones agree, no id is duplicated,
    /// nothing reaches outside the board.</item>
    /// <item>a rejected command did not move <see cref="Backpack.Revision"/> and an accepted
    /// one did (AGENTS rule 7: a failure changes nothing).</item>
    /// <item>a successful merge conserved tier mass <c>sum(2^tier)</c>, so no piece was lost
    /// or duplicated on the way through a merge.</item>
    /// <item>the loadout resolver keeps returning a snapshot consistent with the board.</item>
    /// </list>
    /// <para>
    /// The seed is fixed and the RNG is the project's own <see cref="Xoshiro128"/>, so a
    /// failure reproduces exactly (AGENTS rule 6 forbids <c>UnityEngine.Random</c> and
    /// <c>System.Random</c> in Core, and a flaky property test would be worse than none).
    /// </para>
    /// </remarks>
    public sealed class BackpackRandomizedTests
    {
        private const int OperationCount = 10000;

        private static readonly string[] AddableIds =
        {
            "bag.raw_cotton_bag", "bag.yant_cloth_pouch", "bag.wicker_basket_l",
            "item.kali_flame_yant", "item.takrut_tone", "item.mor_knife", "item.sai_sin_thread",
            "item.phra_amulet", "item.waist_yant", "item.tiger_fang", "item.spell_tome",
            "item.red_soda", "item.boiled_egg", "item.kuman_thong",
        };

        [Fact]
        public void Ten_thousand_random_commands_keep_every_invariant()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            var rng = new Xoshiro128(0x5EEB2026u);
            var resolver = new LoadoutResolver(config, new SynergyEvaluator(config));

            for (int i = 0; i < 3; i++)
            {
                Spawn(editor, rng);
            }

            int accepted = 0;
            int rejected = 0;
            int merges = 0;
            var failureCodes = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int step = 0; step < OperationCount; step++)
            {
                // Reopen the camp now and then, so Reset has a fresh target too.
                if (step > 0 && step % 997 == 0)
                {
                    editor.MarkCampEntry();
                }

                int tierMassBefore = InventoryFixture.TierMass(backpack);
                int revisionBefore = backpack.Revision;
                List<PieceInstance> pieces = PiecesOf(backpack);

                bool acceptedStep;
                bool mergeStep = false;
                string errorCode = string.Empty;
                int operation = rng.NextInt(0, 9);

                switch (operation)
                {
                    case 0:
                    case 1:
                        acceptedStep = Spawn(editor, rng);
                        break;

                    case 2:
                    case 3:
                        acceptedStep = Record(editor.Move(Choice(rng, pieces), RandomOrigin(rng), RandomRotation(rng)));
                        break;

                    case 4:
                        acceptedStep = Record(editor.Rotate(Choice(rng, pieces)));
                        break;

                    case 5:
                        acceptedStep = Record(editor.TransferToChest(Choice(rng, pieces)));
                        break;

                    case 6:
                        // Aim at a genuine pair (two equal items) when one exists, so the walk
                        // reaches tier changes instead of only mismatches.
                        PieceRef mergeSource = Choice(rng, pieces);
                        PieceRef mergeTarget = MatchingTarget(rng, pieces, mergeSource);
                        Result<MergeOutcome> merge = editor.Merge(mergeSource, mergeTarget);
                        acceptedStep = RecordValue(merge);
                        mergeStep = merge.IsSuccess;
                        break;

                    case 7:
                        acceptedStep = RecordValue(editor.Recycle(Choice(rng, pieces)));
                        break;

                    default:
                        acceptedStep = Record(rng.NextInt(0, 3) == 0 ? editor.Reset() : editor.Undo());
                        break;
                }

                Assert.True(
                    backpack.TryValidate(out string? violation),
                    "step " + step + " (op " + operation + "): " + violation);

                if (acceptedStep)
                {
                    accepted++;
                    Assert.True(
                        backpack.Revision > revisionBefore,
                        "step " + step + ": a command was accepted but the revision did not move");
                }
                else
                {
                    rejected++;
                    Assert.True(
                        backpack.Revision == revisionBefore,
                        "step " + step + ": '" + errorCode + "' was rejected but the revision moved");
                    failureCodes[errorCode] = failureCodes.TryGetValue(errorCode, out int current) ? current + 1 : 1;
                }

                if (mergeStep)
                {
                    merges++;
                    Assert.Equal(tierMassBefore, InventoryFixture.TierMass(backpack));
                }

                Assert.Equal(config.Constant.BoardWidth, backpack.Board.Width);
                Assert.True(backpack.OrderedBoardPieces().Count <= backpack.Bags.Count + backpack.Items.Count);
                Assert.InRange(backpack.Chest.OccupiedCount, 0, backpack.Chest.SlotCount);

                Probe(resolver, backpack, step);

                bool Record(Result result)
                {
                    errorCode = result.IsSuccess ? string.Empty : result.Error.Code;
                    return result.IsSuccess;
                }

                bool RecordValue<T>(Result<T> result)
                {
                    errorCode = result.IsSuccess ? string.Empty : result.Error.Code;
                    return result.IsSuccess;
                }
            }

            Assert.Equal(OperationCount, accepted + rejected);
            Assert.True(accepted > 2000, "the random walk should mostly be accepted, was " + accepted);
            Assert.True(rejected > 500, "the random walk should also exercise rejections, was " + rejected);
            Assert.True(merges > 100, "the random walk should reach merges, was " + merges);
            Assert.True(failureCodes.Count >= 4,
                "too few distinct rejection reasons (" + failureCodes.Count + "): "
                + string.Join(", ", failureCodes.Keys));
        }

        [Fact]
        public void A_second_walk_hammers_the_no_room_paths()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            var rng = new Xoshiro128(0xC0FFEEu);

            for (int step = 0; step < 4000; step++)
            {
                int revisionBefore = backpack.Revision;

                if (rng.NextInt(0, 4) == 0)
                {
                    // Adding is the cheapest way to hit "chest full"; it must be all or
                    // nothing just like the other commands.
                    var added = rng.NextInt(0, 2) == 0
                        ? backpack.AddItem("item.tiger_fang")
                        : backpack.AddBag("bag.raw_cotton_bag");

                    Assert.Equal(added.IsSuccess, backpack.Revision > revisionBefore);
                    Assert.True(backpack.TryValidate(out string? violation), "step " + step + ": " + violation);
                    continue;
                }

                List<PieceInstance> pieces = PiecesOf(backpack);
                if (pieces.Count == 0)
                {
                    continue;
                }

                PieceInstance chosen = pieces[rng.NextInt(0, pieces.Count)];
                switch (rng.NextInt(0, 4))
                {
                    case 0:
                        editor.Move(chosen.Ref, RandomOrigin(rng), RandomRotation(rng));
                        break;
                    case 1:
                        editor.Rotate(chosen.Ref);
                        break;
                    case 2:
                        editor.TransferToChest(chosen.Ref);
                        break;
                    default:
                        if (rng.NextInt(0, 2) == 0)
                        {
                            editor.Undo();
                        }
                        else
                        {
                            editor.Reset();
                        }

                        break;
                }

                Assert.True(backpack.TryValidate(out string? violation2), "step " + step + ": " + violation2);
                Assert.InRange(backpack.Chest.OccupiedCount, 0, backpack.Chest.SlotCount);
            }
        }

        /// <summary>
        /// Stage 09f's property test: 10,000 commands that lean on the two bag gestures — move and turn —
        /// with the structural invariants checked after every one of them and the carrying rule's own
        /// invariant checked on every accepted one (ADR 0020).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The invariant is stated from observations only, never by re-running the rule: for every item
        /// that was lying wholly on the bag before the gesture, after an accepted gesture (a) every one of
        /// its cells still belongs to that same bag — so it went where the bag went — and (b) its
        /// orientation <i>relative</i> to the bag is the one it had before. A move that turns nothing adds
        /// (c) the offset inside the bag's frame, which is what "keeps its relative position" means for a
        /// pure move.
        /// </para>
        /// <para>
        /// Alongside it, every step checks the GDD invariants — no item outside a bag's area, no two
        /// pieces on one cell, nothing reaching off the board — and that the walk neither loses nor
        /// duplicates a piece (the piece count and the total tier mass are conserved, and a rejected
        /// command leaves the revision alone).
        /// </para>
        /// </remarks>
        [Fact]
        public void Ten_thousand_bag_gestures_carry_the_items_that_ride_and_keep_every_invariant()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            var rng = new Xoshiro128(0x09E5BA6u);

            // A bag on the board with two items lying on it, so the walk always has a bag to gesture
            // with and something for it to carry: an item can only stand where a bag already covers a
            // cell, so a walk that starts with an empty board can never reach the rule being tested (it
            // found exactly that — 10 items in a full chest and no bag on the board at all).
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 1, 1);
            InventoryFixture.Place(editor, "item.tiger_fang", 0, 0);

            int accepted = 0;
            int rejected = 0;
            int gestures = 0;
            int carried = 0;
            var failureCodes = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int step = 0; step < OperationCount; step++)
            {
                // Reopen the camp now and then, so Reset has a fresh target too.
                if (step > 0 && step % 613 == 0)
                {
                    editor.MarkCampEntry();
                }

                int revisionBefore = backpack.Revision;
                int piecesBefore = backpack.Bags.Count + backpack.Items.Count;
                int tierMassBefore = InventoryFixture.TierMass(backpack);

                var board = new List<BagPieceInstance>();
                for (int i = 0; i < backpack.Bags.Count; i++)
                {
                    if (backpack.Bags[i].Zone == PieceZone.Board)
                    {
                        board.Add(backpack.Bags[i]);
                    }
                }

                bool acceptedStep;
                bool gestureStep = false;
                string errorCode = string.Empty;

                if (board.Count == 0 || rng.NextInt(0, 4) == 0)
                {
                    // Keep the walk stocked: the bag gestures are what this test is about, and a
                    // board with no bag on it has none to try.
                    acceptedStep = Spawn(editor, rng);
                }
                else
                {
                    gestureStep = true;
                    BagPieceInstance bag = board[rng.NextInt(0, board.Count)];
                    List<Rider> riders = RidersOf(backpack, bag);
                    CellOffset target = RandomOrigin(rng);
                    Rotation rotation = RandomRotation(rng);
                    bool turn = rng.NextInt(0, 2) == 0;

                    // How far the gesture turns the bag: a drag carries a rotation of its own, so a
                    // "move" is only a pure move when the rotation it asks for is the one the bag is
                    // already standing in.
                    int delta = turn ? 1 : (((int)rotation - (int)bag.Rotation) & 3);

                    Result result = turn
                        ? editor.Rotate(bag.Ref)
                        : editor.Move(bag.Ref, target, rotation);

                    acceptedStep = result.IsSuccess;
                    errorCode = result.IsSuccess ? string.Empty : result.Error.Code;

                    if (result.IsSuccess)
                    {
                        AssertCarried(backpack, bag, riders, delta == 0, step);

                        if (riders.Count > 0)
                        {
                            carried++;
                        }
                    }
                }

                Assert.True(
                    backpack.TryValidate(out string? violation),
                    "step " + step + ": " + violation);

                if (gestureStep)
                {
                    // A gesture may not lose or duplicate a piece; spawning one is the other branch,
                    // where the count is expected to go up.
                    Assert.Equal(
                        piecesBefore, backpack.Bags.Count + backpack.Items.Count);
                    Assert.Equal(tierMassBefore, InventoryFixture.TierMass(backpack));
                }

                if (acceptedStep)
                {
                    accepted++;
                    Assert.True(
                        backpack.Revision > revisionBefore,
                        "step " + step + ": a command was accepted but the revision did not move");
                }
                else
                {
                    rejected++;
                    Assert.True(
                        backpack.Revision == revisionBefore,
                        "step " + step + ": '" + errorCode + "' was rejected but the revision moved");
                    failureCodes[errorCode] = failureCodes.TryGetValue(errorCode, out int current) ? current + 1 : 1;
                }

                if (gestureStep)
                {
                    gestures++;
                }
            }

            Assert.Equal(OperationCount, accepted + rejected);
            Assert.True(
                gestures > 5000,
                "the walk should mostly be bag gestures, was " + gestures
                + " (bags on the board " + backpack.OrderedBoardBags().Count
                + ", bags " + backpack.Bags.Count + ", items " + backpack.Items.Count
                + ", accepted " + accepted + ", rejected " + rejected + ")");
            Assert.True(accepted > 1000, "the bag gestures should often be accepted, was " + accepted);
            Assert.True(rejected > 500, "the walk should also exercise rejections, was " + rejected);
            Assert.True(carried > 200, "the walk should often carry items, was " + carried);
        }

        /// <summary>One item that was lying wholly on a bag, and the facts the rule preserves.</summary>
        private readonly struct Rider
        {
            internal Rider(int itemInstanceId, int offsetX, int offsetY, int relativeTurns)
            {
                ItemInstanceId = itemInstanceId;
                OffsetX = offsetX;
                OffsetY = offsetY;
                RelativeTurns = relativeTurns;
            }

            internal int ItemInstanceId { get; }

            /// <summary>The item's top-left cell measured from the bag's own origin.</summary>
            internal int OffsetX { get; }

            internal int OffsetY { get; }

            /// <summary>Quarter turns from the bag's orientation to the item's (0..3).</summary>
            internal int RelativeTurns { get; }
        }

        /// <summary>Every item whose every cell lies on that one bag.</summary>
        private static List<Rider> RidersOf(Backpack backpack, BagPieceInstance bag)
        {
            var riders = new List<Rider>();

            for (int i = 0; i < backpack.Items.Count; i++)
            {
                ItemInstance item = backpack.Items[i];
                if (item.Zone != PieceZone.Board || !WhollyOn(backpack, item, bag.InstanceId))
                {
                    continue;
                }

                riders.Add(new Rider(
                    item.InstanceId,
                    item.Origin.X - bag.Origin.X,
                    item.Origin.Y - bag.Origin.Y,
                    ((item.Rotation.QuarterTurns() - bag.Rotation.QuarterTurns()) + 4) & 3));
            }

            return riders;
        }

        /// <summary>Checks that each rider went where its bag went, keeping its relation to it.</summary>
        private static void AssertCarried(
            Backpack backpack, BagPieceInstance bag, List<Rider> riders, bool pureMove, int step)
        {
            for (int i = 0; i < riders.Count; i++)
            {
                Rider rider = riders[i];
                Assert.True(
                    backpack.TryGetItem(rider.ItemInstanceId, out ItemInstance? item) && item is not null,
                    "step " + step + ": a riding item disappeared");

                Assert.True(
                    WhollyOn(backpack, item!, bag.InstanceId),
                    "step " + step + ": item " + rider.ItemInstanceId + " rode on bag " + bag.InstanceId
                    + " but is no longer on it (" + item!.Origin + ")");

                Assert.Equal(
                    rider.RelativeTurns,
                    ((item.Rotation.QuarterTurns() - bag.Rotation.QuarterTurns()) + 4) & 3);

                if (pureMove)
                {
                    Assert.Equal(rider.OffsetX, item.Origin.X - bag.Origin.X);
                    Assert.Equal(rider.OffsetY, item.Origin.Y - bag.Origin.Y);
                }
            }
        }

        /// <summary>True when every cell of an item is covered by that one bag.</summary>
        private static bool WhollyOn(Backpack backpack, ItemInstance item, int bagInstanceId)
        {
            for (int cell = 0; cell < item.CellCount; cell++)
            {
                if (backpack.Board.BagOwnerAt(item.CellAt(cell)) != bagInstanceId)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Spawn(BackpackEditor editor, IRandomSource rng)
        {
            // Half the time, duplicate a piece already owned: two equal pieces are what a
            // merge needs, and a uniformly random id rarely produces a pair.
            string id;
            List<PieceInstance> owned = PiecesOf(editor.Backpack);
            if (owned.Count > 0 && rng.NextInt(0, 2) == 0)
            {
                id = owned[rng.NextInt(0, owned.Count)].DefId;
            }
            else
            {
                id = AddableIds[rng.NextInt(0, AddableIds.Length)];
            }

            var added = id.StartsWith("bag.", StringComparison.Ordinal)
                ? editor.Backpack.AddBag(id)
                : editor.Backpack.AddItem(id);

            if (added.IsFailure)
            {
                return false;
            }

            // A piece that cannot be laid out stays in the chest, which is a legitimate
            // state the invariants have to accept.
            if (rng.NextInt(0, 10) >= 7)
            {
                return true;
            }

            // Either way the step is "accepted": the add itself moved the revision, and the
            // placement is only a bonus attempt.
            editor.Move(added.Value, RandomOrigin(rng), RandomRotation(rng));
            return true;
        }

        private static List<PieceInstance> PiecesOf(Backpack backpack)
        {
            var all = new List<PieceInstance>(backpack.Bags.Count + backpack.Items.Count);
            for (int i = 0; i < backpack.Bags.Count; i++)
            {
                all.Add(backpack.Bags[i]);
            }

            for (int i = 0; i < backpack.Items.Count; i++)
            {
                all.Add(backpack.Items[i]);
            }

            return all;
        }

        private static PieceRef Choice(IRandomSource rng, List<PieceInstance> pieces) =>
            pieces.Count == 0
                ? new PieceRef(PieceKind.Item, 1 + rng.NextInt(0, 4))
                : pieces[rng.NextInt(0, pieces.Count)].Ref;

        /// <summary>
        /// A random piece that could legally merge with <paramref name="source"/> (same item
        /// id and tier), or another random piece when no such partner exists.
        /// </summary>
        private static PieceRef MatchingTarget(IRandomSource rng, List<PieceInstance> pieces, PieceRef source)
        {
            var candidates = new List<ItemInstance>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] is ItemInstance item
                    && item.InstanceId != source.InstanceId
                    && item.DefId == DefIdOf(pieces, source)
                    && item.Tier == TierOf(pieces, source))
                {
                    candidates.Add(item);
                }
            }

            return candidates.Count > 0
                ? candidates[rng.NextInt(0, candidates.Count)].Ref
                : Choice(rng, pieces);
        }

        private static string DefIdOf(List<PieceInstance> pieces, PieceRef source)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].InstanceId == source.InstanceId && pieces[i].Kind == source.Kind)
                {
                    return pieces[i].DefId;
                }
            }

            return "";
        }

        private static int TierOf(List<PieceInstance> pieces, PieceRef source)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].InstanceId == source.InstanceId && pieces[i].Kind == source.Kind)
                {
                    return pieces[i].Tier;
                }
            }

            return -1;
        }

        private static CellOffset RandomOrigin(IRandomSource rng) =>
            new CellOffset(rng.NextInt(-2, 11), rng.NextInt(-2, 9));

        private static Rotation RandomRotation(IRandomSource rng) => RotationOps.FromQuarterTurns(rng.NextInt(0, 4));

        /// <summary>Resolves the loadout and checks the snapshot still describes a legal board.</summary>
        private static void Probe(LoadoutResolver resolver, Backpack backpack, int step)
        {
            CombatLoadoutSnapshot snapshot = resolver.Resolve(backpack);

            Assert.NotNull(snapshot.Stats);
            Assert.InRange(snapshot.Stats.SpeedPenaltyPercent, 0, 100);
            Assert.NotNull(snapshot.Weapons);
            Assert.NotNull(snapshot.Summons);
            Assert.NotNull(snapshot.Consumables);

            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                Assert.True(snapshot.Weapons[i].Damage >= 0, "step " + step + ": negative weapon damage");
                Assert.True(snapshot.Weapons[i].CooldownSeconds > 0f, "step " + step + ": non-positive cooldown");
            }

            for (int i = 0; i < snapshot.Summons.Count; i++)
            {
                Assert.True(snapshot.Summons[i].CooldownSeconds > 0f, "step " + step + ": non-positive summon cooldown");
            }

            for (int i = 0; i < snapshot.Consumables.Count; i++)
            {
                Assert.True(snapshot.Consumables[i].Count > 0, "step " + step + ": a consumable with no uses");
            }
        }
    }
}
