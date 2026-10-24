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

        /// <summary>The overlay canvas's sorting order: below the HUD, above every world object.</summary>
        public const int SortingOrder = -100;

        /// <summary>True while the band is drawn.</summary>
        public bool Visible => _image is not null && _image.enabled;

        /// <summary>The strength last applied, for tests and reports.</summary>
        public float Strength { get; private set; }

        /// <summary>The colour last applied, for tests and reports.</summary>
        public Color BandColor { get; private set; }

        /// <summary>Wires the image. Called once by the builder, and by tests.</summary>
        public void Configure(Image image) => _image = image;

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
