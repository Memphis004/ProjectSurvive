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
