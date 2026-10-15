using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// Where the piece tooltip goes for a given pointer position: beside the cursor, flipped when it
    /// would leave the canvas, and never off it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pure function of three vectors, so the rule is testable without a scene, a canvas or a
    /// pointer. That matters because the two failures it prevents — a panel half outside the screen
    /// and a panel that runs under the cursor it belongs to — only appear at the corners of the two
    /// sizes the GDD names.
    /// </para>
    /// <para>
    /// Everything is in canvas units with the origin at the canvas' <b>bottom-left</b>, which is the
    /// same convention <c>RectTransform.anchoredPosition</c> uses for a child anchored to
    /// <c>(0, 0)</c>. The caller converts the screen point once and does nothing else.
    /// </para>
    /// </remarks>
    public static class CampTooltipPlacement
    {
        /// <summary>How far from the cursor the panel sits, in canvas units.</summary>
        public const float CursorOffset = 18f;

        /// <summary>The smallest gap left between the panel and the canvas edge.</summary>
        public const float Margin = 8f;

        /// <summary>
        /// The panel's bottom-left corner for a pointer at <paramref name="pointer"/>.
        /// </summary>
        /// <remarks>
        /// Right and up by default; to the left when it would pass the right edge, and downwards when
        /// it would pass the top. A panel wider or taller than the canvas is pinned to the margin
        /// rather than pushed off it, so a small window still shows the whole tooltip.
        /// </remarks>
        public static Vector2 Place(Vector2 pointer, Vector2 panelSize, Vector2 canvasSize)
        {
            float x = pointer.x + CursorOffset;
            if (x + panelSize.x > canvasSize.x - Margin)
            {
                x = pointer.x - CursorOffset - panelSize.x;
            }

            float y = pointer.y + CursorOffset;
            if (y + panelSize.y > canvasSize.y - Margin)
            {
                y = pointer.y - CursorOffset - panelSize.y;
            }

            return new Vector2(
                Limit(x, canvasSize.x - panelSize.x),
                Limit(y, canvasSize.y - panelSize.y));
        }

        /// <summary>
        /// Keeps one coordinate between the margin and <paramref name="far"/>, the last position at
        /// which the panel is still fully on the canvas.
        /// </summary>
        private static float Limit(float value, float far)
        {
            float max = far - Margin;
            if (max < Margin)
            {
                // The panel does not fit at all: the top-left margin is the only place that shows
                // any of it, and pinning there keeps the origin of the text stable.
                return Margin;
            }

            if (value < Margin)
            {
                return Margin;
            }

            return value > max ? max : value;
        }
    }
}
