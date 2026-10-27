using System;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>Which band of the arena a card style is allowed to occupy.</summary>
    public enum ArenaCardZone
    {
        /// <summary>The open middle: low grass and decals only, nothing that hides a ghost.</summary>
        Center = 0,

        /// <summary>The sparse bush ring between the centre and the tree wall.</summary>
        Ring = 1,

        /// <summary>The dense tree wall at the arena's edge, two or three layers deep.</summary>
        Border = 2,

        /// <summary>A flat ground decal; its height is not a world height.</summary>
        Decal = 3,

        /// <summary>
        /// The tall, thin trunks that stand at the bottom edge of the frame (Stage 08g).
        /// </summary>
        /// <remarks>
        /// A card that stands between the camera and the player is drawn in front of them — the
        /// sorting order is the ground row, exactly as it is for a border tree — so these are the
        /// pieces the occlusion dither exists for: they rise from the near edge of the frame and thin
        /// out when the player walks behind one.
        /// </remarks>
        Foreground = 4,
    }

    /// <summary>One kind of card the arena can place.</summary>
    /// <remarks>
    /// A style is a texture, a size in world units and a scale range — never a collider and never a
    /// rule. The material is authored in daylight colours; the lighting profile darkens it at night
    /// through the global <c>_ArenaTint</c>, so a night arena keeps the same assets.
    /// </remarks>
    [Serializable]
    public sealed class ArenaCardStyle
    {
        [Tooltip("Stable name, used by the layout report and by the builder's determinism check.")]
        public string Id = "card";

        [Tooltip("The zone this style may be placed in.")]
        public ArenaCardZone Zone = ArenaCardZone.Ring;

        [Tooltip("World width of the quad at scale 1.")]
        public float Width = 3f;

        [Tooltip("World height of the quad at scale 1, measured from its base.")]
        public float Height = 4f;

        [Tooltip("Smallest vertical scale the layout may choose.")]
        public float MinScale = 0.85f;

        [Tooltip("Largest vertical scale the layout may choose.")]
        public float MaxScale = 1.15f;

        [Tooltip("The material (texture + daylight tint). One material per style keeps batching cheap.")]
        public Material Material;

        /// <summary>True for a ground decal, whose height is a footprint rather than a wall.</summary>
        public bool IsDecal => Zone == ArenaCardZone.Decal;

        /// <summary>Height in world units at <paramref name="scale"/>.</summary>
        public float WorldHeight(float scale) => Height * scale;
    }

    /// <summary>
    /// The arena's authored description: how big each band is, how much may stand in it, and what
    /// the pieces look like. <see cref="ArenaLayout"/> turns it and a seed into placements;
    /// <c>ArenaBuilder</c> turns those placements into a scene. The camp's yard is the same thing
    /// with a different shape (<c>ProjectSurvive.Game.Presentation.Camp.CampTheme</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is presentation. The playable field is the simulation's own structural bound
    /// (<c>BattleArena.Radius</c> = 40 world units, mirrored in <see cref="BattleViewCaps.ArenaRadius"/>),
    /// and <see cref="ArenaBuilder"/> refuses a theme whose border would reach past it. No card has a
    /// collider: ghosts, the player and pickups move through the trees because the trees are scenery.
    /// </para>
    /// <para>
    /// The default values are the prototype's: a 12-unit centre that stays empty enough to read a
    /// fight in, a 12–26 unit bush ring with a minimum spacing so gaps exist, and a 26–40 unit wall
    /// of trees three layers deep. All distances are world units; all counts are caps per zone.
    /// </para>
    /// <para>
    /// Not sealed: the camp's yard is the same mechanism with a different shape and its own
    /// readability floors (<c>CampTheme</c>, Stage 09o). A subclass writes no layout rule of its own.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "ProjectSurvive/Arena Theme",
        fileName = "ArenaTheme",
        order = 40)]
    public class ArenaTheme : ScriptableObject
    {
        [Header("Zones (world units, measured from the arena centre)")]
        [Tooltip("Radius of the open middle. Only decals and low grass may stand inside it.")]
        public float CenterRadius = 12f;

        [Tooltip("Outer radius of the sparse bush ring.")]
        public float RingOuterRadius = 26f;

        [Tooltip("Radius of the playable field. Must not exceed the simulation's BattleArena.Radius.")]
        public float ArenaRadius = 40f;

        [Tooltip("The tallest thing the centre may contain, in world units. The layout and a test enforce it.")]
        public float CenterMaxHeight = 0.75f;

        [Header("Player (the yardstick the camera and the wall are measured against)")]
        [Tooltip(
            "Height of the player figure in world units. 1.6 is what the character profiles publish, " +
            "and the camera profile derives the frame from it.")]
        public float PlayerHeight = 1.6f;

        [Header("The wall's height, in multiples of the player (Stage 08g)")]
        [Tooltip("The shortest a border or foreground tree may stand, as a multiple of PlayerHeight.")]
        public float WallMinHeightFactor = 3f;

        [Tooltip("The tallest a border or foreground tree may stand, as a multiple of PlayerHeight.")]
        public float WallMaxHeightFactor = 5f;

        [Header("Counts (per zone, inclusive)")]
        public int CenterGrassMin = 16;
        public int CenterGrassMax = 26;
        public int RingBushMin = 18;
        public int RingBushMax = 28;
        public int BorderTreeMin = 42;
        public int BorderTreeMax = 56;

        [Header("Decals")]
        [Tooltip("Large dirt patches. Few, low contrast, never a pattern.")]
        public int DirtDecalCount = 6;

        [Tooltip("Small flower decals scattered over the centre and the ring.")]
        public int FlowerDecalCount = 20;

        [Tooltip("Small leaf decals scattered over the centre and the ring.")]
        public int LeafDecalCount = 18;

        [Header("Spacing")]
        [Tooltip("Minimum distance between two bushes, in world units. Gaps are what keep the ring readable.")]
        public float BushMinSpacing = 3.2f;

        [Tooltip("Minimum distance between two border trees, in world units.")]
        public float TreeMinSpacing = 2.1f;

        [Tooltip("How many concentric layers the tree wall is built from (2–3).")]
        [Range(2, 3)]
        public int BorderLayers = 3;

        [Header("Foreground trunks (the bottom edge of the frame)")]
        [Tooltip(
            "Inner radius of the trunk belt, in world units from the arena centre. It has to clear the " +
            "centre's own ceiling, so it is the first radius outside CenterRadius where a trunk may stand.")]
        public float ForegroundInnerRadius = 13f;

        [Tooltip("Outer radius of the trunk belt. The camera's near edge stands ForeCameraRow units in front.")]
        public float ForegroundOuterRadius = 16f;

        [Tooltip("Fewest trunks in the belt.")]
        public int ForegroundTrunkMin = 8;

        [Tooltip("Most trunks in the belt.")]
        public int ForegroundTrunkMax = 13;

        [Tooltip(
            "Ring pitch of one border layer, as a fraction of the average border card width. " +
            "Below 1 makes neighbouring trees overlap, which is what turns a row of cards into a wall.")]
        [Range(0.5f, 2f)]
        public float BorderSlotPitchFactor = 0.85f;

        [Tooltip("How far a card may turn away from square-on to the camera, in degrees either way.")]
        public float MaxYawDegrees = 10f;

        [Header("Cards")]
        [Tooltip("Every standing card style. Each style is one material and therefore one batch bucket.")]
        public ArenaCardStyle[] Styles = Array.Empty<ArenaCardStyle>();

        [Header("Decal styles")]
        [Tooltip("Flat ground decal styles (dirt, flower, leaf).")]
        public ArenaCardStyle[] DecalStyles = Array.Empty<ArenaCardStyle>();

        [Header("Materials")]
        [Tooltip("The opaque floor material (flat colour + faint macro noise).")]
        public Material GroundMaterial;

        [Tooltip("The material the blob shadows are drawn with.")]
        public Material ShadowMaterial;

        [Header("Lighting")]
        [Tooltip("The dusk profile, which the scene starts in. Every texture and material is authored for it.")]
        public ArenaLightingProfile DuskProfile;

        [Tooltip("The night profile: the same assets, a different ambient, key light, fog and grade.")]
        public ArenaLightingProfile NightProfile;

        [Header("Readability (WCAG luminance contrast, measured on a capture)")]
        [Tooltip(
            "Minimum contrast ratio between the ghost-doctor and the floor around them, in both " +
            "profiles. Stage 08f's capture tool measures it from the rendered frame; if it fails, " +
            "the lighting is adjusted, never this number.")]
        public float MinPlayerContrast = 2.2f;

        [Tooltip("Minimum contrast ratio between a ghost and the floor around it, in both profiles.")]
        public float MinGhostContrast = 1.9f;

        [Tooltip("Minimum contrast ratio between a pickup and the floor around it, in both profiles.")]
        public float MinPickupContrast = 2.4f;

        [Tooltip("Minimum contrast ratio between a telegraphed warning and the floor, in both profiles.")]
        public float MinTelegraphContrast = 1.3f;

        /// <summary>
        /// The style a placed card uses, by <see cref="ArenaCardStyle.Zone"/>.
        /// </summary>
        public ArenaCardStyle[] StylesFor(ArenaCardZone zone)
        {
            switch (zone)
            {
                case ArenaCardZone.Decal:
                    return DecalStyles;
                default:
                    return Styles;
            }
        }
    }
}
