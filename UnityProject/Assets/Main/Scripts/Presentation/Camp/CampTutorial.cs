using System;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>One thing a first-time player has to do before the game has explained itself.</summary>
    /// <remarks>
    /// The order of the enum is the order of the checklist, and it is the order the camp's screen
    /// prints. A step is remembered when the <i>command</i> behind it succeeded, never when a
    /// pointer moved over a control.
    /// </remarks>
    public enum CampTutorialAction
    {
        /// <summary>Rent a piece from a shelf (Stage 09's shop).</summary>
        Rent = 0,

        /// <summary>Put an item on a bag cell on the board.</summary>
        Place = 1,

        /// <summary>Turn a piece a quarter turn (R, right-click, or a drop with a different rotation).</summary>
        Rotate = 2,

        /// <summary>Point at a piece so the tooltip prints its stats and combos.</summary>
        ReadTooltip = 3,

        /// <summary>Leave for the floor ("ออกเดินทาง").</summary>
        Depart = 4,
    }

    /// <summary>
    /// The first-run checklist: rent → place → rotate → read the tooltip → depart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>guidance, not a gate</b>. Nothing here refuses a command, and a player who does the
    /// steps out of order — or buys three pieces before placing one — has those steps ticked off:
    /// each observation marks its own step and only its own. The checklist is finished when all five
    /// are, and the caller persists that fact in the settings file, so the next run does not repeat
    /// it.
    /// </para>
    /// <para>
    /// No UnityEngine type and no command logic: the presenter calls <see cref="Observe"/> after a
    /// command it already made, and this class decides only what is left to show. That keeps the
    /// step order testable in EditMode without a scene.
    /// </para>
    /// </remarks>
    public sealed class CampTutorial
    {
        /// <summary>How many steps the checklist has.</summary>
        public const int StepCount = 5;

        private readonly bool[] _done = new bool[StepCount];

        /// <summary>True when the checklist was finished on an earlier run (read from settings).</summary>
        public bool IsComplete { get; private set; }

        /// <summary>True while the camp should show the checklist.</summary>
        public bool IsActive => !IsComplete;

        /// <summary>How many steps have been ticked off.</summary>
        public int DoneCount { get; private set; }

        /// <summary>
        /// The next step to show, or <see cref="CampTutorialAction.Depart"/> when everything is done
        /// (callers check <see cref="IsComplete"/> first).
        /// </summary>
        public CampTutorialAction Current
        {
            get
            {
                for (int i = 0; i < StepCount; i++)
                {
                    if (!_done[i])
                    {
                        return (CampTutorialAction)i;
                    }
                }

                return CampTutorialAction.Depart;
            }
        }

        /// <summary>
        /// Reads the persisted flag. Called once when the camp opens; a completed checklist stays
        /// completed for the session even if a save later fails.
        /// </summary>
        public void Begin(bool completed)
        {
            IsComplete = completed;
            if (!completed)
            {
                return;
            }

            Array.Fill(_done, true);
            DoneCount = StepCount;
        }

        /// <summary>True when this step has been done.</summary>
        public bool IsDone(CampTutorialAction action) => _done[Index(action)];

        /// <summary>
        /// Marks one step done. Idempotent, and order-independent by design. Returns true when this
        /// call finished the checklist, so the caller knows it is the one that should persist it.
        /// </summary>
        public bool Observe(CampTutorialAction action)
        {
            if (IsComplete)
            {
                return false;
            }

            int index = Index(action);
            if (_done[index])
            {
                return false;
            }

            _done[index] = true;
            DoneCount++;

            if (DoneCount < StepCount)
            {
                return false;
            }

            IsComplete = true;
            return true;
        }

        private static int Index(CampTutorialAction action)
        {
            int index = (int)action;
            if (index < 0 || index >= StepCount)
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            return index;
        }
    }
}
