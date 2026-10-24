using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// One look for the arena: ambient, moon, fog, the fake-light intensities and the post-processing
    /// grade. Day and night are two of these; every texture and material is authored for the day one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stage's rule is "asset ทุกชิ้นเป็นสีกลางวัน กลางคืนเกิดจาก profile เท่านั้น". It is
    /// enforced by construction: the arena's shaders read one global <c>_ArenaTint</c> and one global
    /// <c>_ArenaLightPool</c>, and this asset is the only thing that writes them. Switching to night
    /// changes no material, no texture and no sprite — only the values below.
    /// </para>
    /// <para>
    /// The post-processing values become a runtime <c>VolumeProfile</c> (Color Adjustments, Bloom,
    /// Vignette, Tonemapping) and the camera is asked for post-processing; no volume asset has to be
    /// authored twice.
    /// </para>
    /// <para>
    /// Light budget, from the locked URP version's own documentation (Unity 6.3 / URP 17.3,
    /// “Light limits in URP”): the Forward path supports 1 main light + 8 additional per-pixel lights
    /// per GameObject, and the project's URP asset authors a per-object limit of 4
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
        [Header("Environment")]
        [Tooltip("Camera background / clear colour.")]
        public Color SkyColor = new Color(0.22f, 0.27f, 0.30f, 1f);

        [Tooltip("Multiplied into every arena shader's daylight colour. White by day, deep blue by night.")]
        public Color EnvironmentTint = Color.white;

        [Tooltip("RenderSettings ambient, sky band.")]
        public Color AmbientSky = new Color(0.55f, 0.60f, 0.63f, 1f);

        [Tooltip("RenderSettings ambient, horizon band.")]
        public Color AmbientEquator = new Color(0.42f, 0.45f, 0.47f, 1f);

        [Tooltip("RenderSettings ambient, ground band.")]
        public Color AmbientGround = new Color(0.25f, 0.27f, 0.28f, 1f);

        [Header("Moon (the single directional light)")]
        public Color MoonColor = new Color(1f, 0.96f, 0.88f, 1f);

        [Tooltip("Directional light intensity. The day profile's moon is the sun.")]
        public float MoonIntensity = 1.15f;

        [Tooltip("Directional light rotation, in Euler degrees.")]
        public Vector3 MoonEuler = new Vector3(52f, -32f, 0f);

        [Header("Fog")]
        public bool FogEnabled = false;
        public Color FogColor = new Color(0.58f, 0.62f, 0.62f, 1f);

        [Tooltip("Exponential-squared fog density. 0.01 is a faint night haze at arena scale.")]
        public float FogDensity = 0.006f;

        [Header("Fake light (no extra real lights)")]
        [Tooltip("The player's lantern point light. 0 by day.")]
        public float LanternIntensity = 0f;

        [Tooltip("The two static camp lights. 0 by day.")]
        public float StaticLightIntensity = 0f;

        [Tooltip("Additive light-pool decals on the floor. 0 by day.")]
        public float LightPoolIntensity = 0f;

        [Tooltip("The lantern's glow sprite. 0 by day.")]
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

        /// <summary>True when this profile lights the arena as night.</summary>
        public bool IsNight => LanternIntensity > 0.001f || LightPoolIntensity > 0.001f;

        /// <summary>How bright a fake light is at this profile, 0 when it is off.</summary>
        public float FakeLightIntensity => Mathf.Max(LightPoolIntensity, GlowIntensity);
    }
}
