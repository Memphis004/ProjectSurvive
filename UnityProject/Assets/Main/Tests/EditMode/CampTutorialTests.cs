using System;
using System.Globalization;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Camp;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 13b acceptance for the camp's first-run checklist: rent → place → rotate → read the
    /// tooltip → depart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claims are the ones the stage makes. The checklist is <b>guidance, not a gate</b>: steps
    /// tick in any order, a step ticks only from a command that actually succeeded, and the flag the
    /// camp saves is the only thing that stops it from showing again.
    /// </para>
    /// <para>
    /// The screen half is read from <see cref="CampUiState"/>, which is exactly what the labels
    /// mirror — so a checklist that computes the right step but never prints it fails here.
    /// </para>
    /// </remarks>
    public sealed class CampTutorialTests
    {
        /// <summary>The camp scene's authored seed, the same one the other camp tests pin.</summary>
        private const uint Seed = 5u;

        private IGameConfig _config;
        private LocalizationService _text;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();
            _text = new LocalizationService(_config, Language.Th);
        }

        // -------------------------------------------------------------------
        // the bookkeeping, without a screen
        // -------------------------------------------------------------------

        [Test]
        public void Steps_tick_independently_and_in_any_order()
        {
            var tutorial = new CampTutorial();
            tutorial.Begin(false);

            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(tutorial.Current, Is.EqualTo(CampTutorialAction.Rent));

            // Out of order: the fourth step first leaves the first three exactly as they were.
            Assert.That(tutorial.Observe(CampTutorialAction.ReadTooltip), Is.False);
            Assert.That(tutorial.IsDone(CampTutorialAction.ReadTooltip), Is.True);
            Assert.That(tutorial.IsDone(CampTutorialAction.Rent), Is.False);
            Assert.That(tutorial.Current, Is.EqualTo(CampTutorialAction.Rent));
            Assert.That(tutorial.DoneCount, Is.EqualTo(1));

            // Observing the same step again is not a second step.
            Assert.That(tutorial.Observe(CampTutorialAction.ReadTooltip), Is.False);
            Assert.That(tutorial.DoneCount, Is.EqualTo(1));
        }

        [Test]
        public void The_checklist_reports_its_finish_once_and_only_on_the_last_step()
        {
            var tutorial = new CampTutorial();
            tutorial.Begin(false);

            CampTutorialAction[] order =
            {
                CampTutorialAction.Rent,
                CampTutorialAction.Place,
                CampTutorialAction.Rotate,
                CampTutorialAction.ReadTooltip,
                CampTutorialAction.Depart,
            };

            int finishes = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (tutorial.Observe(order[i]))
                {
                    finishes++;
                }
            }

            Assert.That(finishes, Is.EqualTo(1), "exactly one step may report the finish");
            Assert.That(tutorial.IsComplete, Is.True);
            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(tutorial.DoneCount, Is.EqualTo(CampTutorial.StepCount));

            // Nothing reports a second finish, and nothing un-finishes.
            Assert.That(tutorial.Observe(CampTutorialAction.Rent), Is.False);
            Assert.That(tutorial.IsComplete, Is.True);
        }

        [Test]
        public void A_persisted_flag_suppresses_the_checklist_from_the_first_frame()
        {
            var tutorial = new CampTutorial();
            tutorial.Begin(true);

            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(tutorial.IsComplete, Is.True);
            Assert.That(tutorial.DoneCount, Is.EqualTo(CampTutorial.StepCount));
            Assert.That(tutorial.Observe(CampTutorialAction.Rent), Is.False);
        }

        // -------------------------------------------------------------------
        // what the screen prints
        // -------------------------------------------------------------------

        [Test]
        public void A_new_profile_gets_the_checklist_and_the_rows_tick_as_commands_land()
        {
            RunSession run = AtFirstCamp();
            var state = new CampUiState();
            var presenter = new CampPresenter(run, _config, _text, state, tutorialCompleted: false);
            presenter.Begin();

            Assert.That(state.InstructionsTitle.Value, Is.EqualTo(_text.Get("camp.tutorial.title")));
            Assert.That(state.Instruction1.Value, Is.EqualTo(Row("current", 1)));
            Assert.That(state.Instruction2.Value, Is.EqualTo(Row("todo", 2)));
            Assert.That(state.Instruction5.Value, Is.EqualTo(Row("todo", 5)));
            Assert.That(state.HintCaption.Value, Is.EqualTo(Step(1)));

            int slot = FirstRentableSlot(run);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "seed " + Seed + " must offer something to rent");

            presenter.Rent(slot);

            Assert.That(presenter.Tutorial.IsDone(CampTutorialAction.Rent), Is.True);
            Assert.That(state.Instruction1.Value, Is.EqualTo(Row("done", 1)));
            Assert.That(state.Instruction2.Value, Is.EqualTo(Row("current", 2)));
            Assert.That(state.HintCaption.Value, Is.EqualTo(Step(2)));
            Assert.That(presenter.TakeTutorialCompletion(), Is.False, "four steps are still open");
        }

        [Test]
        public void Finishing_every_step_restores_the_static_instructions_and_asks_for_the_flag_once()
        {
            RunSession run = AtFirstCamp();
            var state = new CampUiState();
            var presenter = new CampPresenter(run, _config, _text, state, tutorialCompleted: false);
            presenter.Begin();

            // Every step through the same calls the screen makes: the checklist must not need a
            // bypass, and it must not tick from anything the player did not do.
            int slot = FirstRentableItemSlot(presenter, run);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "seed " + Seed + " must offer an item to rent");
            presenter.Rent(slot);

            PieceRef rented = FirstChestPiece(presenter);
            Assert.That(rented.IsValid, Is.True, "a rented piece is carried in the chest");
            Assert.That(PlaceOnBoard(presenter, rented), Is.True, "the bag has no cell left to place into");
            Assert.That(presenter.Tutorial.IsDone(CampTutorialAction.Place), Is.True, "a placement did not tick");

            presenter.SelectOnly(rented);
            presenter.RotateSelected();
            Assert.That(presenter.Tutorial.IsDone(CampTutorialAction.Rotate), Is.True, "a rotation did not tick");
            Assert.That(presenter.Tutorial.IsComplete, Is.False, "two steps are still open");

            presenter.Hover(rented);
            Assert.That(presenter.Tutorial.IsDone(CampTutorialAction.ReadTooltip), Is.True, "hovering a piece did not tick");

            presenter.Depart();
            Assert.That(presenter.Departed, Is.True, "the departure was refused: " + presenter.StatusCode);
            Assert.That(presenter.Tutorial.IsComplete, Is.True, "the checklist did not finish");

            Assert.That(state.InstructionsTitle.Value, Is.EqualTo(_text.Get("camp.instructions.title")));
            Assert.That(state.Instruction1.Value, Is.EqualTo(_text.Get("camp.instructions.1")));
            Assert.That(state.Instruction5.Value, Is.Empty, "the fifth row is empty outside the checklist");
            Assert.That(state.HintCaption.Value, Is.EqualTo(_text.Get("camp.world.hint")));

            Assert.That(presenter.TakeTutorialCompletion(), Is.True, "the finish must be saved");
            Assert.That(presenter.TakeTutorialCompletion(), Is.False, "a second read must not save again");
        }

        [Test]
        public void A_profile_that_finished_the_checklist_before_sees_the_static_instructions()
        {
            RunSession run = AtFirstCamp();
            var state = new CampUiState();
            var presenter = new CampPresenter(run, _config, _text, state, tutorialCompleted: true);
            presenter.Begin();

            Assert.That(state.InstructionsTitle.Value, Is.EqualTo(_text.Get("camp.instructions.title")));
            Assert.That(state.Instruction4.Value, Is.EqualTo(_text.Get("camp.instructions.4")));
            Assert.That(state.Instruction5.Value, Is.Empty);
            Assert.That(state.HintCaption.Value, Is.EqualTo(_text.Get("camp.world.hint")));
            Assert.That(presenter.TakeTutorialCompletion(), Is.False);
        }

        /// <summary>A command the checklist does not ask for must not tick anything.</summary>
        [Test]
        public void A_refused_command_ticks_no_step()
        {
            RunSession run = AtFirstCamp();
            var state = new CampUiState();
            var presenter = new CampPresenter(run, _config, _text, state, tutorialCompleted: false);
            presenter.Begin();

            // A slot the shop cannot sell twice: rent one, then ask for it again.
            int slot = FirstRentableSlot(run);
            presenter.Rent(slot);
            presenter.Rent(slot);

            Assert.That(presenter.Tutorial.DoneCount, Is.EqualTo(1), "the refused rent ticked a second step");
            Assert.That(presenter.StatusCode, Is.Not.Empty, "the refusal is reported to the player");
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        /// <summary>One checklist row, formatted from the shipped localization table.</summary>
        private string Row(string state, int step) =>
            string.Format(
                CultureInfo.InvariantCulture,
                _text.Get("camp.tutorial." + state + ".format"),
                _text.Get("camp.tutorial." + step));

        /// <summary>The status bar's "step N/5: …" line.</summary>
        private string Step(int current) =>
            string.Format(
                CultureInfo.InvariantCulture,
                _text.Get("camp.tutorial.step.format"),
                current,
                CampTutorial.StepCount,
                _text.Get("camp.tutorial." + current));

        /// <summary>
        /// Drags a chest piece onto the first board cell the screen's own preview calls a
        /// <b>placement</b> (an invalid cell or a merge is not what the checklist's step means).
        /// </summary>
        private static bool PlaceOnBoard(CampPresenter presenter, PieceRef piece)
        {
            presenter.DragStart(piece, Rotation.Degrees0);

            CampBagScreen bag = presenter.Bag;
            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    presenter.DragMove(BagDropZone.Board, new CellOffset(column, row));

                    if (presenter.Drag.IsValid && presenter.Drag.Action == BagDropAction.Place)
                    {
                        presenter.Drop();
                        return presenter.Tutorial.IsDone(CampTutorialAction.Place);
                    }
                }
            }

            presenter.CancelDrag();
            return false;
        }

        private static PieceRef FirstChestPiece(CampPresenter presenter)
        {
            CampPieceModel[] pieces = presenter.Bag.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Zone == PieceZone.Chest)
                {
                    return pieces[i].Piece;
                }
            }

            return PieceRef.None;
        }

        /// <summary>
        /// The first shelf that offers an <b>item the run does not already hold</b>: a duplicate would
        /// fold into the piece on the board (a merge), and the checklist's second step is a
        /// placement, not a merge.
        /// </summary>
        private static int FirstRentableItemSlot(CampPresenter presenter, RunSession run)
        {
            CampPieceModel[] held = presenter.Bag.Pieces;

            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                string offered = run.Stock[i].OfferedId ?? string.Empty;
                if (run.Stock[i].IsPurchased || !offered.StartsWith("item.", StringComparison.Ordinal)
                    || run.Stock[i].Price > run.Wallet.Bia)
                {
                    continue;
                }

                bool alreadyHeld = false;
                for (int p = 0; p < held.Length; p++)
                {
                    if (held[p].DefId == offered)
                    {
                        alreadyHeld = true;
                        break;
                    }
                }

                if (!alreadyHeld)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FirstRentableSlot(RunSession run)
        {
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                if (!run.Stock[i].IsPurchased && run.Stock[i].Price <= run.Wallet.Bia)
                {
                    return i;
                }
            }

            return -1;
        }

        private RunSession AtFirstCamp()
        {
            var run = new RunSession(_config, new MessagePackBackpackSnapshotCodec(), Seed);
            Assert.That(run.StartRun().IsSuccess, Is.True, "the run must start on the shipped tables");
            return run;
        }
    }
}
