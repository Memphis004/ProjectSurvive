using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// The arena's presentation: which <see cref="ArenaLightingProfile"/> is in force, the fake
    /// lights that profile turns on, the lantern that follows the player, and the instanced shadow
    /// layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the whole runtime half of Stage 08e. The cards, the decals and the tree wall are
    /// authored into the Battle scene by <c>ArenaBuilder</c> and marked static, so at runtime they
    /// cost nothing to build and almost nothing to draw. What is left is the part that has to move
    /// or change: the lighting profile, the player's lantern and the shadows under the actors.
    /// </para>
    /// <para>
    /// <b>Day and night are one call.</b> <see cref="SetNight"/> selects the theme's night profile
    /// and <see cref="ApplyProfile"/> writes it into the engine: ambient, the one directional light,
    /// fog, the camera's clear colour, the global tint the arena shaders read, the fake-light
    /// intensities, and a runtime post-processing volume (Color Adjustments, optional LUT, Bloom,
    /// Vignette). No material, texture or sprite is touched, which is what “สลับ Day/Night ได้โดยไม่
    /// เปลี่ยน asset” means in practice.
    /// </para>
    /// <para>
    /// <b>Why almost no real lights.</b> The locked URP version's documentation (Unity 6.3 /
    /// URP 17.3, “Light limits in URP”) caps the Forward path at 1 main + 8 additional per-pixel
    /// lights per GameObject, and this project's URP asset authors a per-object limit of 4. The
    /// arena uses three additional lights in total (the player's lantern and two static camp lights)
    /// and paints the rest with additive ground pools and glow sprites, so the light count cannot
    /// grow with the number of decorated spots.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaEnvironmentView : MonoBehaviour
    {
        /// <summary>Global the arena shaders read for their daylight-to-night tint.</summary>
        public const string ArenaTintGlobal = "_ArenaTint";

        /// <summary>Global that turns the card sway on and off.</summary>
        public const string ArenaSwayGlobal = "_ArenaSway";

        /// <summary>Global intensity of the additive ground pools and glows.</summary>
        public const string ArenaLightPoolGlobal = "_ArenaLightPool";

        [Tooltip("The theme the scene was built from. Its Day/Night profiles are what switch.")]
        [SerializeField] private ArenaTheme _theme;

        [Tooltip("The one directional light: the sun by day, the moon by night.")]
        [SerializeField] private Light _moon;

        [Tooltip("The player's lantern point light. Off by day.")]
        [SerializeField] private Light _lantern;

        [Tooltip("The lantern light's own transform, moved to the player every frame.")]
        [SerializeField] private Transform _lanternRoot;

        [Tooltip("The lantern's glow quad, shown only while the profile gives it light.")]
        [SerializeField] private Renderer _lanternGlow;

        [Tooltip("The two static camp lights. Off by day.")]
        [SerializeField] private Light[] _staticLights = Array.Empty<Light>();

        [Tooltip("The battle camera. Defaults to Camera.main.")]
        [SerializeField] private Camera _camera;

        [Tooltip("The volume the runtime grade is written into.")]
        [SerializeField] private Volume _volume;

        [Tooltip("Blob shadows under the actors (Stage 08e).")]
        [SerializeField] private ArenaBlobShadows _shadows;

        [Tooltip("Start the floor at night. The gallery/capture tool drives both.")]
        [SerializeField] private bool _night;

        [Tooltip("Leaf sway. A settings switch: the shader reads it as a global, no material changes.")]
        [SerializeField] private bool _leafSway = true;

        private VolumeProfile _runtimeProfile;
        private bool _applied;

        /// <summary>The profile in force (null before <see cref="Awake"/>).</summary>
        public ArenaLightingProfile CurrentProfile { get; private set; }

        /// <summary>True while the night profile is the one in force.</summary>
        public bool IsNight { get; private set; }

        /// <summary>True while the leaf sway is on.</summary>
        public bool LeafSway => _leafSway;

        /// <summary>The theme the scene was built from.</summary>
        public ArenaTheme Theme => _theme;

        /// <summary>The shadow layer.</summary>
        public ArenaBlobShadows Shadows => _shadows;

        /// <summary>The additional lights the environment owns, for the light-budget test.</summary>
        public Light[] AdditionalLights
        {
            get
            {
                int extra = _lantern is null ? 0 : 1;
                var lights = new Light[(_staticLights?.Length ?? 0) + extra];
                int index = 0;
                for (int i = 0; i < (_staticLights?.Length ?? 0); i++)
                {
                    lights[index++] = _staticLights[i];
                }

                if (_lantern is not null)
                {
                    lights[index] = _lantern;
                }

                return lights;
            }
        }

        private void Awake()
        {
            if (_camera is null)
            {
                _camera = Camera.main;
            }

            // The scene owns the shadow mesh and material; the component is wired by the builder.
            Apply(_night);
        }

        private void OnDestroy() => ReleaseProfile();

        /// <summary>Switches to day or night. The one command the acceptance asks for.</summary>
        public void SetNight(bool night) => Apply(night);

        /// <summary>Switches to the other profile.</summary>
        public void ToggleNight() => Apply(!IsNight);

        /// <summary>Turns the card sway on or off (a settings switch, not an asset change).</summary>
        public void SetLeafSway(bool enabled)
        {
            _leafSway = enabled;
            Shader.SetGlobalFloat(ArenaSwayGlobal, enabled ? 1f : 0f);
        }

        /// <summary>Applies a specific profile, whichever of the theme's two it is.</summary>
        public void ApplyProfile(ArenaLightingProfile profile)
        {
            if (profile is null)
            {
                return;
            }

            Apply(ReferenceEquals(profile, _theme?.NightProfile));
        }

        /// <summary>
        /// Writes a profile into the engine. Public so a test can call it without entering play mode;
        /// <see cref="Awake"/> applies the authored one on the first frame.
        /// </summary>
        public void Apply(bool night)
        {
            if (_theme is null)
            {
                return;
            }

            ArenaLightingProfile profile = night ? _theme.NightProfile : _theme.DayProfile;
            if (profile is null)
            {
                return;
            }

            IsNight = night;
            CurrentProfile = profile;

            Shader.SetGlobalColor(ArenaTintGlobal, profile.EnvironmentTint);
            Shader.SetGlobalFloat(ArenaLightPoolGlobal, profile.LightPoolIntensity);
            Shader.SetGlobalFloat(ArenaSwayGlobal, _leafSway ? 1f : 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.AmbientSky;
            RenderSettings.ambientEquatorColor = profile.AmbientEquator;
            RenderSettings.ambientGroundColor = profile.AmbientGround;
            RenderSettings.fog = profile.FogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = profile.FogColor;
            RenderSettings.fogDensity = profile.FogDensity;

            if (_moon is not null)
            {
                _moon.color = profile.MoonColor;
                _moon.intensity = profile.MoonIntensity;
                _moon.transform.rotation = Quaternion.Euler(profile.MoonEuler);
            }

            ApplyLight(_lantern, profile.LanternIntensity);

            if (_staticLights is not null)
            {
                for (int i = 0; i < _staticLights.Length; i++)
                {
                    ApplyLight(_staticLights[i], profile.StaticLightIntensity);
                }
            }

            if (_lanternGlow is not null)
            {
                bool lit = profile.GlowIntensity > 0.001f;
                _lanternGlow.enabled = lit;
                var block = new MaterialPropertyBlock();
                _lanternGlow.GetPropertyBlock(block);

                Color glow = Color.white;
                glow.a = Mathf.Clamp01(profile.GlowIntensity);
                block.SetColor("_BaseColor", glow);
                _lanternGlow.SetPropertyBlock(block);
            }

            if (_camera is not null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = profile.SkyColor;

                UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
                if (data is not null)
                {
                    data.renderPostProcessing = true;
                }
            }

            BuildVolume(profile);
            _applied = true;
        }

        /// <summary>
        /// Follows the player with the lantern and draws the frame's shadows. Called once per
        /// rendered frame by <see cref="BattleViewScene"/>.
        /// </summary>
        public void Tick(Vector3 playerWorldPosition, ReadOnlySpan<RenderEntity> entities)
        {
            if (_lanternRoot is not null)
            {
                _lanternRoot.position = playerWorldPosition + new Vector3(0f, 1.15f, 0f);
            }

            if (_shadows is not null)
            {
                _shadows.Draw(entities);
            }
        }

        /// <summary>True once <see cref="Apply"/> has written a profile.</summary>
        public bool Applied => _applied;

        private static void ApplyLight(Light light, float intensity)
        {
            if (light is null)
            {
                return;
            }

            light.intensity = Mathf.Max(0f, intensity);
            light.enabled = intensity > 0.001f;
        }

        /// <summary>
        /// Builds the runtime post-processing profile the stage's lighting asset describes. A fresh
        /// profile is created per switch and the previous one released, so the asset on disk is never
        /// written and toggling back and forth cannot accumulate overrides.
        /// </summary>
        private void BuildVolume(ArenaLightingProfile profile)
        {
            if (_volume is null)
            {
                return;
            }

            ReleaseProfile();

            var runtime = ScriptableObject.CreateInstance<VolumeProfile>();
            runtime.name = "Arena " + profile.name + " (runtime)";
            runtime.hideFlags = HideFlags.DontSave;

            ColorAdjustments adjustments = runtime.Add<ColorAdjustments>(true);
            adjustments.postExposure.Override(profile.PostExposure);
            adjustments.colorFilter.Override(profile.ColorFilter);
            adjustments.contrast.Override(profile.Contrast);
            adjustments.saturation.Override(profile.Saturation);

            if (profile.WhiteBalance != 0f)
            {
                WhiteBalance balance = runtime.Add<WhiteBalance>(true);
                balance.temperature.Override(profile.WhiteBalance);
            }

            if (profile.ColorLookup is not null)
            {
                ColorLookup lookup = runtime.Add<ColorLookup>(true);
                lookup.texture.Override(profile.ColorLookup);
                lookup.contribution.Override(1f);
            }

            Bloom bloom = runtime.Add<Bloom>(true);
            bloom.intensity.Override(profile.BloomIntensity);
            bloom.threshold.Override(profile.BloomThreshold);
            bloom.scatter.Override(profile.BloomScatter);

            Vignette vignette = runtime.Add<Vignette>(true);
            vignette.intensity.Override(profile.VignetteIntensity);
            vignette.smoothness.Override(profile.VignetteSmoothness);

            _runtimeProfile = runtime;
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;
            _volume.sharedProfile = runtime;
        }

        private void ReleaseProfile()
        {
            if (_runtimeProfile is null)
            {
                return;
            }

            if (_volume is not null && ReferenceEquals(_volume.sharedProfile, _runtimeProfile))
            {
                _volume.sharedProfile = null;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeProfile);
            }
            else
            {
                DestroyImmediate(_runtimeProfile);
            }

            _runtimeProfile = null;
        }
    }
}
