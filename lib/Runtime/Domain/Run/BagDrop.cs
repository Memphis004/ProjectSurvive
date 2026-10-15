using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// Where a dragged piece was released — the vocabulary the Stage 09 screen speaks
    /// when it asks what a drop would do.
    /// </summary>
    /// <remarks>
    /// A <b>zone</b> rather than a pixel position, because a rule must not know about a
    /// layout: the view owns the geometry and says which surface the pointer is over, and
    /// the run says whether that surface accepts the piece
    /// ([ADR 0002](../../../../docs/adr/0002-thin-mvp-ui-approach.md)).
    /// </remarks>
    public enum BagDropZone
    {
        /// <summary>Outside every drop surface — released over nothing.</summary>
        None = 0,

        /// <summary>The board (the 9×7 placement area).</summary>
        Board = 1,

        /// <summary>The chest (หีบไม้สัก).</summary>
        Chest = 2,

        /// <summary>The altar pot (ถวายคืนศาล).</summary>
        Altar = 3,
    }

    /// <summary>What releasing a dragged piece would do.</summary>
    public enum BagDropAction
    {
        /// <summary>Nothing: the drop is refused.</summary>
        None = 0,

        /// <summary>Place or move the piece on the board (<c>MovePiece</c>).</summary>
        Place = 1,

        /// <summary>Fold the piece into the one under it (<c>MergeItems</c>).</summary>
        Merge = 2,

        /// <summary>Park the piece in the chest (<c>TransferToChest</c>).</summary>
        Transfer = 3,

        /// <summary>Give the piece back to the altar for bia (<c>Sell</c>).</summary>
        Recycle = 4,
    }

    /// <summary>
    /// The answer to "what happens if I let go here?", computed without changing anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GDD Stage 09: "preview วางได้และวางไม่ได้ โดยไม่แก้ Domain จนผู้เล่นยืนยัน". The preview
    /// is a dry run of the very command the drop would issue — the run reaches the same
    /// <c>BackpackEditor</c> predicates the commands use, so a green preview and a refused
    /// command are the same condition seen twice, never two implementations of "can this go
    /// here?".
    /// </para>
    /// <para>
    /// A refusal always carries an <see cref="ErrorCode"/> from the same stable vocabulary as a
    /// refused command (<c>inventory.*</c>, <c>run.*</c>), which is what lets the UI show the
    /// reason through <c>error.&lt;code&gt;</c> localization instead of inventing a message.
    /// </para>
    /// </remarks>
    public readonly struct DropPreview
    {
        private DropPreview(bool isValid, BagDropAction action, PieceRef target, int bia, string errorCode)
        {
            IsValid = isValid;
            Action = action;
            Target = target;
            Bia = bia;
            ErrorCode = errorCode ?? string.Empty;
        }

        /// <summary>True when the drop would be accepted.</summary>
        public bool IsValid { get; }

        /// <summary>What would happen.</summary>
        public BagDropAction Action { get; }

        /// <summary>The piece the drop would fold into, for <see cref="BagDropAction.Merge"/>.</summary>
        public PieceRef Target { get; }

        /// <summary>Bia the altar would pay, for <see cref="BagDropAction.Recycle"/>.</summary>
        public int Bia { get; }

        /// <summary>Why the drop would be refused; empty when <see cref="IsValid"/>.</summary>
        public string ErrorCode { get; }

        /// <summary>A drop that would be accepted.</summary>
        public static DropPreview Accepted(BagDropAction action, PieceRef target = default, int bia = 0) =>
            new DropPreview(true, action, target, bia, string.Empty);

        /// <summary>A drop that would be refused, with the code a command would have returned.</summary>
        public static DropPreview Refused(string errorCode) =>
            new DropPreview(false, BagDropAction.None, default, 0, errorCode);

        /// <inheritdoc />
        public override string ToString() =>
            IsValid ? "drop " + Action + (Bia > 0 ? " +" + Bia + " bia" : string.Empty) : "drop refused: " + ErrorCode;
    }
}
