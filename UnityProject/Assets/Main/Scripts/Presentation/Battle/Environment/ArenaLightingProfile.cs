using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>Which of the arena's two looks a profile is.</summary>
    public enum ArenaTimeOfDay
    {
        /// <summary>Warm late-afternoon green-yellow: the shipped look, and the default.</summary>
        Dusk = 0,

        /// <summary>The same assets under a colder, darker key: night.</summary>
        Night = 1,
    }

    /// <summary>
    /// One look for the arena: ambient, key light, the cool shadow colour and its one direction,
    /// fog, the fake-light intensities, the screen-top gradient and the post-processing grade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stage's rule is "asset ทุกชิ้นเป็นสีกลางวัน บรรยากาศมาจาก LightingProfile เท่านั้น". It is
    /// enforced by construction: the arena's shaders read a handful of globals (<c>_ArenaTint</c>,
    /// <c>_ArenaLightPool</c>, <c>_ArenaGlow</c>, <c>_ArenaShadowColor</c>,
    /// <c>_ArenaShadowStrength</c>, <c>_ArenaScreenGradient</c>) and this asset is the only thing
    /// that writes them. Switching to night changes no material, no texture and no sprite — only the
    /// values below.
    /// </para>
    /// <para>
    /// <b>The two colours.</b> A dusk jungle is a warm green-yellow key over a cool teal shadow.
    /// <see cref="KeyLightColor"/> is the warm end (the directional light and the ambient's sky
    /// band), <see cref="ShadowColor"/> the cool end that every fake shadow multiplies into the
    /// floor. The shadow tint is deliberately never black: a fully black shadow on a green floor
    /// reads as a hole, and the floor is the surface the fight is read against.
    /// </para>
    /// <para>
    /// <b>One shadow direction for the whole scene.</b> Cards do not cast real shadows (Stage 08f
    /// draws a soft decal per card instead, see <see cref="ArenaShadowCard"/>), so the direction is
    /// authored once, here, and every shadow decal is oriented from it. <see cref="ShadowDirectionDegrees"/>
    /// is measured in the ground plane: 0 points at world +X, 90 at world +Z (away from the camera).
    /// </para>
    /// <para>
    /// The post-processing values become a runtime <c>VolumeProfile</c> (Color Adjustments, Bloom,
    /// Vignette) and the camera is asked for post-processing; no volume asset has to be authored
    /// twice. The screen-top gradient is a camera-space quad rather than URP fog: fog darkens by
    /// distance from the camera, and with an orthographic camera following the player that distance
    /// is not the screen's vertical axis. The quad is sized from the camera's orthographic size and
    /// aspect (<see cref="ArenaScreenGradient.SizeFor"/>), so it covers the top edge at both
    /// captured resolutions instead of guessing. Fog is still used, at a low density, for the rows
    /// of trees further up the arena.
    /// </para>
    /// <para>
    /// Light budget, from the locked URP version's own documentation (Unity 6.3 / URP 17.3,
    /// “Light limits in URP”, read 2026-10-25): the Forward path supports 1 main light + 8 additional
    /// per-pixel lights per GameObject, 16 additional lights per camera on OpenGL ES 3.0 and earlier
    /// (which is the WebGL2 limit), and this project's URP asset authors a per-object limit of 4
    /// (<c>m_AdditionalLightsPerObjectLimit</c>). This profile therefore never asks for more than
    /// <see cref="LanternIntensity"/> (one point light) plus <see cref="StaticLightIntensity"/>
    /// (two camp lights) — three additional lights in total. Everything else is an additive decal
    /// or a glow sprite, which is the whole point of the art direction.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "ProjectSurvive/Arena Lighting Profile",
        fileName = "ArenaLighting",
        order = 41)]
    public sealed class ArenaLightingProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which of the two authored looks this asset is. Dusk is the default.")]
        public ArenaTimeOfDay TimeOfDay = ArenaTimeOfDay.Dusk;

        [Header("Environment")]
        [Tooltip("Camera background / clear colour.")]
        public Color SkyColor = new Color(0.22f, 0.27f, 0.30f, 1f);

        [Tooltip("Multiplied into every arena shader's daylight colour. Warm by dusk, blue by night.")]
        public Color EnvironmentTint = Color.white;

        [Tooltip("RenderSettings ambient, sky band.")]
        public Color AmbientSky = new Color(0.55f, 0.60f, 0.63f, 1f);

        [Tooltip("RenderSettings ambient, horizon band.")]
        public Color AmbientEquator = new Color(0.42f, 0.45f, 0.47f, 1f);

        [Tooltip("RenderSettings ambient, ground band.")]
        public Color AmbientGround = new Color(0.25f, 0.27f, 0.28f, 1f);

        [Header("Key light (the single directional light)")]
        [Tooltip("The warm end of the grade: green-yellow by dusk, cold blue by night.")]
        public Color KeyLightColor = new Color(1f, 0.96f, 0.88f, 1f);

        [Tooltip("Directional light intensity.")]
        public float KeyLightIntensity = 1.15f;

        [Tooltip("Directional light rotation, in Euler degrees. Its horizontal part is the light's own angle.")]
        public Vector3 KeyLightEuler = new Vector3(52f, -32f, 0f);

        [Header("Shadows (fake decals, never a shadow map)")]
        [Tooltip("The cool end of the grade. Multiplied into the floor, so it must never be black.")]
        public Color ShadowColor = new Color(0.34f, 0.52f, 0.66f, 1f);

        [Tooltip("How dark a shadow gets where it is fully opaque, 0 … 1.")]
        [Range(0f, 1f)]
        public float ShadowStrength = 0.55f;

        [Tooltip("The one direction every shadow in the scene falls toward. 0 = world +X, 90 = world +Z.")]
        [Range(0f, 360f)]
        public float ShadowDirectionDegrees = 245f;

        [Tooltip("Shadow length per unit of card height. A tall tree throws a longer shadow than grass.")]
        [Range(0f, 2f)]
        public float ShadowLengthPerHeight = 0.42f;

        [Tooltip("How wide the big canopy patch under a border tree is, as a fraction of the tree's width.")]
        [Range(0f, 2f)]
        public float CanopyShadowWidthFactor = 0.95f;

        [Header("Fog (distance haze for the rows further up the arena)")]
        public bool FogEnabled = false;
        public Color FogColor = new Color(0.58f, 0.62f, 0.62f, 1f);

        [Tooltip("Exponential-squared fog density. 0.01 is a faint night haze at arena scale.")]
        public float FogDensity = 0.006f;

        [Header("Screen (camera-space, works with the orthographic rig)")]
        [Tooltip("How much the top edge of the screen darkens. 0 leaves the top untouched.")]
        [Range(0f, 1f)]
        public float ScreenTopGradient = 0f;

        [Tooltip("The colour the top edge fades toward.")]
        public Color ScreenGradientColor = new Color(0.03f, 0.05f, 0.04f, 1f);

        [Tooltip("How much a soft band across the vertical middle of the frame lifts the field. 0 turns it off.")]
        [Range(0f, 1f)]
        public float ScreenMidGradient = 0f;

        [Tooltip("The colour the middle band fades toward: the haze the fight is read through.")]
        public Color ScreenMidColor = new Color(0.62f, 0.63f, 0.55f, 1f);

        [Header("Fake light (no extra real lights)")]
        [Tooltip("The player's lantern point light. 0 by day.")]
        public float LanternIntensity = 0f;

        [Tooltip("The two static camp lights. 0 by day.")]
        public float StaticLightIntensity = 0f;

        [Tooltip("Additive light-pool decals on the floor.")]
        public float LightPoolIntensity = 0f;

        [Tooltip("The lantern's glow sprite and the small floating motes.")]
        public float GlowIntensity = 0f;

        [Header("Post-processing")]
        public float PostExposure = 0f;
        public Color ColorFilter = Color.white;

        [Range(-100f, 100f)] public float Contrast = 0f;
        [Range(-100f, 100f)] public float Saturation = 0f;
        [Range(-100f, 100f)] public float WhiteBalance = 0f;

        [Tooltip("Optional colour-grading LUT. Left empty when the grade is numeric.")]
        public Texture ColorLookup;

        [Range(0f, 4f)] public float BloomIntensity = 0f;
        [Range(0f, 4f)] public float BloomThreshold = 1.1f;
        [Range(0f, 1f)] public float BloomScatter = 0.6f;

        [Range(0f, 1f)] public float VignetteIntensity = 0f;
        [Range(0.01f, 1f)] public float VignetteSmoothness = 0.4f;

        /// <summary>True when this profile is the darker of the two.</summary>
        public bool IsNight => TimeOfDay == ArenaTimeOfDay.Night;

        /// <summary>How bright a fake light is at this profile, 0 when it is off.</summary>
        public float FakeLightIntensity => Mathf.Max(LightPoolIntensity, GlowIntensity);

        /// <summary>
        /// The unit direction every shadow decal extends toward, in the ground plane. One angle for
        /// the whole scene, derived from <see cref="ShadowDirectionDegrees"/> and nothing else.
        /// </summary>
        public Vector3 ShadowDirection =>
            new Vector3(
                Mathf.Cos(ShadowDirectionDegrees * Mathf.Deg2Rad),
                0f,
                Mathf.Sin(ShadowDirectionDegrees * Mathf.Deg2Rad));
    }
}
