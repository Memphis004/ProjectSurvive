using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The sala's draw order, in one place: the ground, the flat decals, the card shadows, the
    /// additive light pools, the yard's cards and the figures that stand in the sala.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camp draws with <see cref="SpriteRenderer.sortingOrder"/>, not with depth, exactly like
    /// the arena draws with <c>BattleWorldMap.SortingOrder</c>: the camera is orthographic and tilted,
    /// so "who hides whom" has to be a number and not a z-fight. The furniture's orders (10 … 18)
    /// were authored by Stage 09c; this class keeps them and gives the yard's cards their own ladder.
    /// </para>
    /// <para>
    /// A card's order comes from the ground row it stands on, with the same idea the arena uses —
    /// further away draws behind, nearer draws in front — scaled so that a card at the player's own
    /// row ties with the player and a card in front of them draws over. The scale is not the arena's
    /// 16 per unit: the sala's authored ladder is 8 orders across 6.8 units (the stall's 10 at
    /// z = 4.6 and the player's 18 at z = −2.2), which is 1.18 — rounded here to 1.2 so the arithmetic
    /// is readable and no visible pair changes side.
    /// </para>
    /// </remarks>
    public static class CampSorting
    {
        /// <summary>The packed-earth ground.</summary>
        public const int GroundOrder = 1;

        /// <summary>Flat decals lying on the ground, above it.</summary>
        public const int DecalOrder = 2;

        /// <summary>Fake card shadows: over the decals, under every light pool and card.</summary>
        public const int CardShadowOrder = 3;

        /// <summary>Additive warm pools on the ground.</summary>
        public const int LightPoolOrder = 4;

        /// <summary>The lowest order a standing card may take (it must still clear the pools).</summary>
        public const int FirstCardOrder = 5;

        /// <summary>ยายจันทร์'s stall.</summary>
        public const int StallOrder = 10;

        /// <summary>The player's order in the sala, and the row it was authored at.</summary>
        public const int PlayerOrder = 18;

        /// <summary>The player's spawn row, which the card ladder is anchored on.</summary>
        public const float PlayerRowZ = -2.2f;

        /// <summary>Sorting steps per world unit of Z (see the remarks).</summary>
        public const float RowsPerUnit = 1.2f;

        /// <summary>The highest order a card may take, below the screen-space band.</summary>
        public const int LastCardOrder = 60;

        /// <summary>The order a standing card at <paramref name="z"/> draws at.</summary>
        public static int CardOrder(float z) => Mathf.Clamp(
            Mathf.RoundToInt(PlayerOrder - ((z - PlayerRowZ) * RowsPerUnit)),
            FirstCardOrder,
            LastCardOrder);
    }
}
