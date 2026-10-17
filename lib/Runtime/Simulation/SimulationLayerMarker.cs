namespace ProjectSurvive.Core.Simulation
{
    /// <summary>
    /// Marker for the simulation layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The template ships this assembly empty on purpose: a game that needs a fixed-step
    /// simulation puts it here, and this file is what keeps the folder non-empty, the asmdef
    /// meaningful and the layer test (ADR 0004) with something to name.
    /// </para>
    /// <para>
    /// The allowed references are exactly <c>Shared</c> and <c>Domain</c> — no UnityEngine, no
    /// R3, no VContainer, no serializer (AGENTS rule 3). The <c>.asmdef</c> is the enforcement;
    /// this comment is the explanation.
    /// </para>
    /// </remarks>
    public static class SimulationLayerMarker
    {
        /// <summary>The layer's name, for diagnostics and tests.</summary>
        public const string LayerName = "Simulation";
    }
}
