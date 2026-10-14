namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The run-level events a UI subscribes to (GDD: <c>WalletChanged</c>,
    /// <c>InventoryChanged</c>, <c>ShopStockChanged</c>, <c>LoadoutChanged</c>,
    /// <c>FloorCompleted</c>, <c>RunCompleted</c>).
    /// </summary>
    /// <remarks>
    /// Events are for presentation only, exactly like the battle's event ring: a
    /// rule reads state, never an event stream. They exist so a Presenter can
    /// refresh a label without polling the whole run every frame.
    /// </remarks>
    public enum RunEventKind
    {
        /// <summary>The wallet balance moved; <see cref="RunEvent.Value"/> is the new balance.</summary>
        WalletChanged = 0,

        /// <summary>The bag changed (a purchase landed, a piece was sold); <see cref="RunEvent.Value"/> is the new revision.</summary>
        InventoryChanged = 1,

        /// <summary>The shop stock changed; <see cref="RunEvent.Value"/> is the slot index, or -1 when the whole stock was re-rolled.</summary>
        ShopStockChanged = 2,

        /// <summary>The resolved combat loadout changed; <see cref="RunEvent.Value"/> is the backpack revision it was built from.</summary>
        LoadoutChanged = 3,

        /// <summary>A floor was settled; <see cref="RunEvent.Value"/> is the settlement id.</summary>
        FloorCompleted = 4,

        /// <summary>The run ended; <see cref="RunEvent.Value"/> is 1 for a victory, 0 for a defeat.</summary>
        RunCompleted = 5,

        /// <summary>The flow phase changed; <see cref="RunEvent.Value"/> is the new <see cref="RunFlowState"/>.</summary>
        FlowChanged = 6,
    }

    /// <summary>One run event: a kind plus the two small payload slots the UI needs.</summary>
    public readonly struct RunEvent
    {
        /// <summary>Creates an event.</summary>
        public RunEvent(RunEventKind kind, int value = 0, string id = "")
        {
            Kind = kind;
            Value = value;
            Id = id ?? string.Empty;
        }

        /// <summary>What happened.</summary>
        public RunEventKind Kind { get; }

        /// <summary>A kind-specific number (see each <see cref="RunEventKind"/>).</summary>
        public int Value { get; }

        /// <summary>A kind-specific id, or the empty string. Never a player-facing sentence.</summary>
        public string Id { get; }

        /// <inheritdoc />
        public override string ToString() => Id.Length == 0
            ? Kind + "(" + Value + ")"
            : Kind + "(" + Value + "," + Id + ")";
    }
}
