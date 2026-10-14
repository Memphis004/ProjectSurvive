using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Game.Battle
{
    /// <summary>
    /// Produces the floor the Battle scene plays, from the real tables and the real bag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 08's acceptance says the scene must use a <b>real</b>
    /// <see cref="CombatLoadoutSnapshot"/> — the stage is about the presentation, so a
    /// hand-built loadout would prove nothing about whether the screen reads the build. This
    /// class therefore plays the actual flow: a <see cref="RunSession"/> is started on the
    /// shipped tables, the camp's shop is rented from (when the scene asks for a second piece),
    /// the bag is edited through the same Application commands the Stage 09 UI will call, and
    /// <see cref="RunSession.StartFloor"/> hands back the setup — including the loadout the real
    /// resolver produced, tier and synergy bonuses included.
    /// </para>
    /// <para>
    /// It is deliberately <i>not</i> the run scope of Stage 10: the run lives only long enough to
    /// build one setup and is then dropped, which is why the HUD is handed the starting purse
    /// instead of subscribing to a wallet. When Stage 10 lands the shared run scope, this class
    /// is what collapses into it.
    /// </para>
    /// <para>
    /// Placement uses a deterministic search over board cells rather than a hard-coded spot, so
    /// it keeps working when the starting bag's footprint or the piece table changes, and every
    /// attempt goes through <c>BackpackEditor.Move</c>'s validation — the scene never writes
    /// board state itself.
    /// </para>
    /// </remarks>
    public sealed class BattleRunSource
    {
        private readonly IGameConfig _config;
        private readonly uint _seed;
        private readonly bool _equipSecondWeapon;

        /// <summary>Creates a source for one seed.</summary>
        /// <param name="config">The loaded tables.</param>
        /// <param name="seed">Run seed; also what the cosmetic stream is derived from.</param>
        /// <param name="equipSecondWeapon">
        /// Rent and place one extra piece at camp, so the floor runs on a build that is not just
        /// the starting knife. This is what makes "เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง"
        /// visible in the scene rather than only in a test.
        /// </param>
        public BattleRunSource(IGameConfig config, uint seed, bool equipSecondWeapon)
        {
            _config = config;
            _seed = seed;
            _equipSecondWeapon = equipSecondWeapon;
        }

        /// <summary>The seed every stream of this battle is derived from.</summary>
        public uint Seed => _seed;

        /// <summary>
        /// Builds the floor's setup. Returns false with a reason when the run or the departure
        /// refused, so the scene can report a data problem instead of showing an empty arena.
        /// </summary>
        public bool TryBuild(out BattleSetup setup, out int startingBia, out string error)
        {
            setup = null;
            startingBia = 0;
            error = string.Empty;

            if (_config is null)
            {
                error = "BattleRunSource has no IGameConfig.";
                return false;
            }

            var run = new RunSession(_config, new MessagePackBackpackSnapshotCodec(), _seed);
            Result started = run.StartRun();
            if (started.IsFailure)
            {
                error = "the run could not start: " + started.Error;
                return false;
            }

            if (_equipSecondWeapon)
            {
                // A best-effort edit: a shop that offers nothing affordable, or a bag that
                // cannot fit the piece, leaves the starting knife alone rather than failing the
                // floor. The build is what matters, not that this particular edit landed.
                TryEquipAnExtraPiece(run, _config);
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
        /// Preferring a fighting piece is what makes this scene's reason for existing visible.
        /// Stage 08's acceptance is "เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง", and the
        /// cheapest piece at a first-floor camp is routinely a bag: buying one would change the
        /// floor by a percent of walking speed, which is a change the player cannot see. A weapon
        /// changes what the floor does. The fallback keeps the scene honest when the camp offers
        /// nothing else — it still plays the build the bag produced.
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
