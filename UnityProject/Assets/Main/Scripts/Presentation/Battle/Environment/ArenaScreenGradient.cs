using UnityEngine;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// The dark band along the top edge of the screen: the part of Dusk's "bright middle, darker
    /// edges" that a radial vignette cannot do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a screen-space overlay — one <see cref="Canvas"/> at a negative sorting order, so the
    /// HUD's own canvases stay above it — rather than a fog term. URP's fog factor comes from the
    /// clip-space depth of each vertex (verified in the locked package,
    /// <c>ShaderVariablesFunctions.hlsl</c>, <c>ComputeFogFactor</c> → <c>UNITY_Z_0_FAR_FROM_CLIPSPACE</c>),
    /// so it darkens by distance from the camera. With an orthographic camera following the player,
    /// that distance is not the screen's vertical axis, and the top rows of the screen are only a few
    /// units further away than the middle ones. The gradient therefore has to be screen-space to be a
    /// top *edge*, and as a canvas overlay it does not read the camera's projection at all: it covers
    /// the same fraction of the frame at 1920×1080 and at 1280×720 (both captured and compared in the
    /// stage report).
    /// </para>
    /// <para>
    /// The profile owns both numbers: <see cref="ArenaLightingProfile.ScreenTopGradient"/> is the
    /// strength and <see cref="ArenaLightingProfile.ScreenGradientColor"/> the colour. Turning the
    /// strength to zero disables the image rather than tinting it transparent, so a profile that does
    /// not want the band costs no fill rate.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaScreenGradient : MonoBehaviour
    {
        [Tooltip("The stretched image the builder creates. Its sprite is a vertical alpha ramp.")]
        [SerializeField] private Image _image;

        [Tooltip("The mid-field haze: a second stretched image whose sprite is a bump, not a ramp.")]
        [SerializeField] private Image _midImage;

        /// <summary>The overlay canvas's sorting order: below the HUD, above every world object.</summary>
        public const int SortingOrder = -100;

        /// <summary>True while the band is drawn.</summary>
        public bool Visible => _image is not null && _image.enabled;

        /// <summary>The strength last applied, for tests and reports.</summary>
        public float Strength { get; private set; }

        /// <summary>The colour last applied, for tests and reports.</summary>
        public Color BandColor { get; private set; }

        /// <summary>The mid-field strength last applied, for tests and reports.</summary>
        public float MidStrength { get; private set; }

        /// <summary>The mid-field colour last applied, for tests and reports.</summary>
        public Color MidColor { get; private set; }

        /// <summary>True while the mid-field band is drawn.</summary>
        public bool MidVisible => _midImage is not null && _midImage.enabled;

        /// <summary>Wires the image. Called once by the builder, and by tests.</summary>
        public void Configure(Image image) => _image = image;

        /// <summary>Wires the mid-field image. Called once by the builder, and by tests.</summary>
        public void ConfigureMid(Image image) => _midImage = image;

        /// <summary>
        /// Writes the mid-field band: the haze across the vertical middle of the frame, where the
        /// fight is read. It is a separate <see cref="Image"/> on its own sprite (a bump, zero at
        /// both ends) because the top band's ramp cannot leave the bottom of the frame alone.
        /// </summary>
        public void ApplyMid(Color colour, float strength)
        {
            MidStrength = Mathf.Clamp01(strength);
            MidColor = colour;

            if (_midImage is null)
            {
                return;
            }

            _midImage.enabled = MidStrength > 0.001f;

            colour.a = MidStrength;
            _midImage.color = colour;
        }

        /// <summary>Writes the profile's band into the overlay.</summary>
        public void Apply(Color colour, float strength)
        {
            Strength = Mathf.Clamp01(strength);
            BandColor = colour;

            if (_image is null)
            {
                return;
            }

            _image.enabled = Strength > 0.001f;

            // The sprite is the alpha ramp, so the colour's own alpha is the strength.
            colour.a = Strength;
            _image.color = colour;
        }
    }
}
