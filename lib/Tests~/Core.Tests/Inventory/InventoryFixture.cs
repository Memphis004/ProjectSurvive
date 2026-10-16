using System;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Builds backpacks for the inventory tests over the <b>real</b> generated tables.
    /// </summary>
    /// <remarks>
    /// No stub config: the point of the Stage 05 suite is that the GDD's rules hold over
    /// the data Stage 04 authored (three bags, five combo rules, the real tag vocabulary).
    /// A hand-written config would assert the tests against themselves.
    /// </remarks>
    internal static class InventoryFixture
    {
        /// <summary>A fresh, mutable config snapshot (tests may retune a constant).</summary>
        internal static IGameConfig Config() => ConfigFixture.Load();

        /// <summary>A fresh backpack plus the editor that commands it.</summary>
        internal static BackpackEditor NewEditor(IGameConfig config, out Backpack backpack) =>
            NewEditor(config, BackpackEditor.DefaultUndoDepth, out backpack);

        /// <summary>A fresh backpack plus the editor that commands it, with a chosen undo depth.</summary>
        internal static BackpackEditor NewEditor(IGameConfig config, int undoDepth, out Backpack backpack)
        {
            backpack = new Backpack(config);
            return new BackpackEditor(backpack, config, new MessagePackBackpackSnapshotCodec(), undoDepth);
        }

        /// <summary>Adds a piece by id, choosing the bag or item table from the id's prefix.</summary>
        internal static PieceRef Add(BackpackEditor editor, string id)
        {
            var result = id.StartsWith("bag.", StringComparison.Ordinal)
                ? editor.Backpack.AddBag(id)
                : editor.Backpack.AddItem(id);

            if (result.IsFailure)
            {
                throw new InvalidOperationException("fixture could not add '" + id + "': " + result.Error);
            }

            return result.Value;
        }

        /// <summary>Adds a piece and lays it out at (x, y) in one step.</summary>
        internal static PieceRef Place(BackpackEditor editor, string id, int x, int y, Rotation rotation = Rotation.Degrees0)
        {
            PieceRef piece = Add(editor, id);
            var moved = editor.Move(piece, new CellOffset(x, y), rotation);
            if (moved.IsFailure)
            {
                throw new InvalidOperationException(
                    "fixture could not place '" + id + "' at " + x + "," + y + ": " + moved.Error);
            }

            return piece;
        }

        /// <summary>Lays a bag out and returns its handle.</summary>
        internal static PieceRef PlaceBag(BackpackEditor editor, string bagId, int x, int y, Rotation rotation = Rotation.Degrees0) =>
            Place(editor, bagId, x, y, rotation);

        /// <summary>
        /// The whole state as MessagePack bytes — the cheapest way to assert "nothing
        /// changed" after a rejected command, field for field.
        /// </summary>
        internal static byte[] StateBytes(Backpack backpack) =>
            new MessagePackBackpackSnapshotCodec().Encode(backpack.CaptureSnapshot());

        /// <summary>The seed these tests pass to the upgrade draw.</summary>
        internal const uint MergeSeed = 20261016u;

        /// <summary>
        /// Folds a pair the way the game does (Stage 09f): offers the choices, confirms the
        /// first one when there is a chooser, and falls back to the immediate merge when there
        /// is not.
        /// </summary>
        /// <remarks>
        /// The merge RULE tests use this instead of picking a variant by hand, so they exercise
        /// the real two-phase command without each of them re-implementing the branch. The
        /// choice-specific behaviour has its own tests (UpgradeOfferTests).
        /// </remarks>
        internal static Result<MergeOutcome> Merge(
            BackpackEditor editor, PieceRef source, PieceRef target, uint seed = MergeSeed)
        {
            Result<MergeOffer> offered = editor.OfferMerge(source, target, seed);
            if (offered.IsFailure)
            {
                return Result.Fail<MergeOutcome>(offered.Error);
            }

            return offered.Value.HasChoices
                ? editor.ConfirmMerge(source, target, offered.Value.Options[0].Id, seed)
                : editor.Merge(source, target);
        }

        /// <summary>Checks every structural invariant and fails the test with the reason.</summary>
        internal static void AssertValid(Backpack backpack)
        {
            if (!backpack.TryValidate(out string? violation))
            {
                throw new InvalidOperationException("backpack invariant broken: " + violation);
            }
        }

        /// <summary>The bag piece behind a handle, for value assertions.</summary>
        internal static BagPieceInstance BagOf(Backpack backpack, PieceRef piece)
        {
            if (!backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is not BagPieceInstance bag)
            {
                throw new InvalidOperationException("fixture expected a bag piece: " + piece);
            }

            return bag;
        }

        /// <summary>The item instance behind a handle, for value assertions.</summary>
        internal static ItemInstance ItemOf(Backpack backpack, PieceRef piece)
        {
            if (!backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is not ItemInstance item)
            {
                throw new InvalidOperationException("fixture expected an item: " + piece);
            }

            return item;
        }

        /// <summary>
        /// Total "tier mass" of the items, <c>sum(2^tier)</c>: merging two equal tiers must
        /// leave this unchanged, which is how a lost or duplicated piece is detected.
        /// </summary>
        internal static int TierMass(Backpack backpack)
        {
            int mass = 0;
            for (int i = 0; i < backpack.Items.Count; i++)
            {
                mass += 1 << backpack.Items[i].Tier;
            }

            return mass;
        }
    }
}
