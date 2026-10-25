using System;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// ศาลาพัก's own yard: the same <see cref="ArenaTheme"/> mechanism the arena uses (cards, ground
    /// decals, instanced card shadows, one lighting profile per look), shaped for a small clear yard
    /// around the sala instead of a 40-unit battlefield.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A second asset of the same authoring model, not a second system: the layout is
    /// <see cref="ArenaLayout.Build"/> over these numbers, the cards are the <c>ArenaCard</c> prefab,
    /// the shadows are <see cref="ArenaShadowCard"/> drawn by one instanced call, and the look is
    /// <see cref="ArenaLightingProfile"/> applied by <see cref="ArenaEnvironmentView"/> — the camp
    /// only chooses different numbers and adds the three readability floors below.
    /// </para>
    /// <para>
    /// <b>The numbers are the yard's shape.</b> <see cref="ArenaTheme.CenterRadius"/> is the open
    /// ground: it is chosen larger than the corner of the sala's walk rectangle
    /// (√(8.4² + 3.6²) = 9.14) so the layout's own rule — "only decals and low grass stand in the
    /// centre" — is also the camp's rule "nothing tall or blocking stands where the player walks".
    /// The ring and the border are the frame of scrub and trees outside it, thinner than the
    /// arena's wall because a camp is a clearing, not a battlefield.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "ProjectSurvive/Camp Theme",
        fileName = "CampTheme",
        order = 42)]
    public sealed class CampTheme : ArenaTheme
    {
        [Header("Fixed pieces (lamps and signs, placed rather than rolled)")]
        [Tooltip(
            "Styles the scene places at fixed spots (CampYard.Fixtures). They are deliberately not in " +
            "Styles: a lamp rolled into the layout's tree wall would be a lamp in the middle of a forest.")]
        public ArenaCardStyle[] FixtureStyles = Array.Empty<ArenaCardStyle>();

        [Header("Readability (Stage 09o, measured on a capture)")]
        [Tooltip(
            "Minimum WCAG contrast between a shelf, the divination cylinder or the incense pot and " +
            "the ground behind it, in both profiles. Measured from the rendered frame; if it fails, " +
            "the light pools and the rim are adjusted, never this number.")]
        public float MinPropContrast = 1.9f;

        [Tooltip(
            "Minimum WCAG contrast between the near-black ink of a world-space price board and the " +
            "cream board it is printed on, in both profiles. The boards are unlit UI, so this ratio " +
            "is made of the label's own ink, board and type size — not of the scene's lighting. Set to " +
            "WCAG AA for body text (4.5): the labels are what the player reads to decide a purchase.")]
        public float MinLabelContrast = 4.5f;

        [Tooltip(
            "The player-facing text floor this project states for a 1080p screen (Stage 09d): the " +
            "height of a label's ink, in pixels, at 1920×1080. The price boards' type is sized to " +
            "clear it and measured against it on every capture.")]
        public float LabelFloorPixels = 22f;
    }
}
