using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Game.Battle
{
    /// <summary>
    /// Starts a floor from the real tables and the real bag, for a Battle scene that was opened
    /// <b>without</b> a camp: <c>?scene=Battle</c> in a browser, the Editor's scene list, a
    /// direct-load PlayMode test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 08 used a class of this name to build a whole throwaway run per scene visit, because
    /// nothing owned a run yet. Stage 10a does own one — the Run scope of the App scope — so the
    /// normal path no longer comes through here at all: the camp performs the departure and the
    /// Battle scene starts the floor the run already holds in
    /// <see cref="RunSession.PendingSetup"/>. What is left is the path where there is no camp, and
    /// it plays the actual flow on the run it is handed: start it, rent the cheapest piece that can
    /// fight, place it through the same Application commands the จัดย่าม screen calls, and depart.
    /// </para>
    /// <para>
    /// The rental is what makes "เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง" (Stage 08's
    /// acceptance) visible in a scene that never saw a camp. It is best-effort: a shop that offers
    /// nothing affordable, or a bag that cannot fit the piece, leaves the starting knife alone
    /// rather than failing the floor.
    /// </para>
    /// </remarks>
    public static class BattleRunSource
    {
        /// <summary>
        /// Starts <paramref name="run"/>'s next floor: starts the run when it is still at the
        /// title, optionally equips one extra piece, and departs.
        /// </summary>
        /// <param name="run">The run to play. It is mutated exactly as the Application commands say.</param>
        /// <param name="config">The loaded tables.</param>
        /// <param name="equipSecondWeapon">
        /// Rent and place one extra piece first, so a floor opened on its own is not just the
        /// starting knife.
        /// </param>
        /// <param name="setup">The floor's setup, or null on failure.</param>
        /// <param name="startingBia">The purse entering the floor.</param>
        /// <param name="error">Why it refused, or an empty string.</param>
        /// <returns>True when the run departed.</returns>
        public static bool TryStartFrom(
            RunSession run, IGameConfig config, bool equipSecondWeapon,
            out BattleSetup setup, out int startingBia, out string error)
        {
            setup = null;
            startingBia = 0;
            error = string.Empty;

            if (run is null)
            {
                error = "BattleRunSource has no run.";
                return false;
            }

            if (config is null)
            {
                error = "BattleRunSource has no IGameConfig.";
                return false;
            }

            if (run.FlowState == RunFlowState.Title)
            {
                Result started = run.StartRun();
                if (started.IsFailure)
                {
                    error = "the run could not start: " + started.Error;
                    return false;
                }
            }

            if (run.FlowState != RunFlowState.Camp)
            {
                error = "the run is in " + run.FlowState + ", so no floor can depart.";
                return false;
            }

            if (equipSecondWeapon)
            {
                TryEquipAnExtraPiece(run, config);
            }

            Result<BattleSetup> departed = run.StartFloor();
            if (departed.IsFailure)
            {
                error = "departing was refused: " + departed.Error;
                return false;
            }

            setup = departed.Value;
            startingBia = run.Wallet.Bia;
            return true;
        }

        private static void TryEquipAnExtraPiece(RunSession run, IGameConfig config)
        {
            int slot = CheapestAvailableSlot(run, config);
            if (slot < 0)
            {
                return;
            }

            Result<PurchaseOutcome> rented = run.Purchase(slot);
            if (rented.IsFailure)
            {
                return;
            }

            // Whatever the camp offered goes on the board. The loadout resolver decides what it
            // is worth; the scene never guesses at a piece's category.
            TryPlace(run, rented.Value.Piece);
        }

        private static void TryPlace(RunSession run, PieceRef piece)
        {
            int width = run.Backpack.Board.Width;
            int height = run.Backpack.Board.Height;

            // Row-major and deterministic: the same seed and the same tables always place the
            // piece in the same cell, so two runs of one seed build identical loadouts.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (run.MovePiece(piece, new CellOffset(x, y), Rotation.Degrees0).IsSuccess)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// The slot to rent from: the cheapest affordable piece that can fight (an arcane weapon
        /// or a familiar), or — when the camp has nothing like that on offer — simply the cheapest
        /// affordable piece.
        /// </summary>
        /// <remarks>
        /// Preferring a fighting piece is what makes a standalone scene worth opening. The cheapest
        /// piece at a first-floor camp is routinely a bag: buying one would change the floor by a
        /// percent of walking speed, which is a change the player cannot see. A weapon changes what
        /// the floor does. The fallback keeps the scene honest when the camp offers nothing else —
        /// it still plays the build the bag produced.
        /// </remarks>
        private static int CheapestAvailableSlot(RunSession run, IGameConfig config)
        {
            int cheapest = -1;
            int cheapestFighting = -1;

            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                ShopSlot slot = run.Stock[i];
                if (slot.IsEmpty || slot.IsPurchased || !run.Wallet.CanAfford(slot.Price))
                {
                    continue;
                }

                if (cheapest < 0 || slot.Price < run.Stock[cheapest].Price)
                {
                    cheapest = i;
                }

                if (!IsFightingPiece(config, slot))
                {
                    continue;
                }

                if (cheapestFighting < 0 || slot.Price < run.Stock[cheapestFighting].Price)
                {
                    cheapestFighting = i;
                }
            }

            return cheapestFighting >= 0 ? cheapestFighting : cheapest;
        }

        /// <summary>True for a piece the loadout will turn into a weapon or a summon.</summary>
        private static bool IsFightingPiece(IGameConfig config, ShopSlot slot)
        {
            if (slot.Kind != PieceKind.Item || !config.TryGetItem(slot.OfferedId, out ItemConfig item))
            {
                return false;
            }

            return item.Category == ItemCategory.ArcaneWeapon || item.Category == ItemCategory.Familiar;
        }
    }
}
