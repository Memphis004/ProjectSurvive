using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 08g's look-dev invariants, as numbers rather than opinions: an arena card carries at
    /// least as many texels as it is drawn pixels at 1080p, the textures are filtered the way the
    /// stage requires, the camera renders at 1:1 with no depth of field, and the night is not veiled
    /// by its own fog.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stage was opened because the picture looked soft. Every clause here is one of the causes
    /// that were measured on the real frame (see <c>docs/stages/08g-report.md</c> §1), so a future
    /// change that reintroduces one of them fails here instead of being noticed by eye a stage later.
    /// </para>
    /// <para>
    /// The texel test is arithmetic on authored numbers — a style's world width, the camera's
    /// orthographic half-height, 1080 px — and the texture's own imported width. It does not read a
    /// rendered frame: the point is to hold the <i>budget</i>, and the frame is measured separately by
    /// <c>AgentScripts/ArenaStage08gCapture.cs</c>.
    /// </para>
    /// </remarks>
    public sealed class ArenaSharpnessTests
    {
        /// <summary>The height the texel budget is stated at: the stage's 1080p reference.</summary>
        private const float ReferenceHeight = 1080f;

        private static ArenaTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ArenaTheme>(ArenaArtBuilder.ThemePath);
            Assert.IsNotNull(theme, "the arena theme is missing: " + ArenaArtBuilder.ThemePath);
            return theme;
        }

        /// <summary>
        /// Vertical pixels per world unit at 1080p: the orthographic half-height covers half the
        /// screen. <c>1080 / (2 × 8.889) = 60.75</c> at the stage's camera profile.
        /// </summary>
        private static float PixelsPerUnit() => ReferenceHeight / (2f * BattleCameraRig.OrthographicSize);

        private static float ScreenWidth(ArenaCardStyle style) => style.Width * PixelsPerUnit();

        /// <summary>
        /// A card is turned square-on to the camera — <c>ArenaCard.FacingCamera</c> rotates it by the
        /// camera's own pitch — so under an orthographic camera it is drawn at its own world size on
        /// <b>both</b> axes. The first version of this test discounted the height by cos(50°), which is
        /// wrong (a quad perpendicular to the view axis is not foreshortened) and 1.55× optimistic;
        /// the wall was resized against the honest number in the same stage.
        /// </summary>
        private static float ScreenHeight(ArenaCardStyle style) => style.Height * PixelsPerUnit();

        /// <summary>
        /// The stage's central sharpness rule: a standing card must not be upscaled at 1080p. The
        /// binding case is now width — the widest card the theme may place is 8.36 units, 508 px —
        /// and no card is drawn at more than 481 px tall against the 512 px sources the art builder
        /// writes.
        /// </summary>
        [Test]
        public void EveryStandingCardIsDrawnAtMostOneTexelPerPixelAt1080p()
        {
            ArenaTheme theme = LoadTheme();
            Assert.IsNotEmpty(theme.Styles, "the theme has no standing card styles");

            foreach (ArenaCardStyle style in theme.Styles)
            {
                var texture = style.Material == null ? null : style.Material.GetTexture("_BaseMap") as Texture2D;
                Assert.IsNotNull(texture, "the style '" + style.Id + "' has no _BaseMap texture");

                // The largest scale the layout may pick for this style is what a player can see.
                float widest = ScreenWidth(style) * style.MaxScale;
                float tallest = ScreenHeight(style) * style.MaxScale;

                Assert.LessOrEqual(
                    widest, texture.width,
                    "the style '" + style.Id + "' is drawn " + widest.ToString("0") + " px wide from a "
                    + texture.width + " px texture at 1080p: it would be upscaled");
                Assert.LessOrEqual(
                    tallest, texture.height,
                    "the style '" + style.Id + "' is drawn " + tallest.ToString("0") + " px tall from a "
                    + texture.height + " px texture at 1080p: it would be upscaled");
            }
        }

        /// <summary>
        /// Anisotropy of at least 4 on every texture the arena's <b>cards, decals and floor</b> draw
        /// with (the stage's floor), which is what keeps a card seen edge-on from being sampled at a
        /// mip that is far too coarse along one axis.
        /// </summary>
        /// <remarks>
        /// Scoped through the theme's own materials rather than to "everything in the texture
        /// folder": the folder also holds the two screen-band sprites, which are drawn by a UI
        /// <c>Image</c> on a canvas, where anisotropy cannot apply to anything.
        /// </remarks>
        [Test]
        public void EveryArenaWorldTextureIsFilteredAnisotropicallyAtLeastFour()
        {
            ArenaTheme theme = LoadTheme();
            var materials = new List<Material>();
            foreach (ArenaCardStyle style in theme.Styles)
            {
                materials.Add(style.Material);
            }

            foreach (ArenaCardStyle style in theme.DecalStyles)
            {
                materials.Add(style.Material);
            }

            materials.Add(theme.GroundMaterial);
            materials.Add(theme.ShadowMaterial);

            int checkedTextures = 0;
            foreach (Material material in materials)
            {
                Assert.IsNotNull(material, "the theme names a material that is missing");
                var texture = material.GetTexture("_BaseMap") as Texture2D;
                Assert.IsNotNull(texture, "the material " + material.name + " has no _BaseMap texture");

                string path = AssetDatabase.GetAssetPath(texture);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer, "no importer for " + path);
                Assert.GreaterOrEqual(
                    importer.anisoLevel, 4,
                    path + " (drawn by " + material.name + ") is imported at aniso " + importer.anisoLevel);
                checkedTextures++;
            }

            Assert.GreaterOrEqual(checkedTextures, 10, "the theme draws fewer world textures than expected");
        }

        /// <summary>
        /// The importer's own cap must not resample the source down: every arena texture's imported
        /// size equals the size of the PNG on disk.
        /// </summary>
        [Test]
        public void NoArenaTextureIsResampledByItsImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArenaArtBuilder.TextureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture, path);
                Assert.LessOrEqual(
                    Mathf.Max(texture.width, texture.height), importer.maxTextureSize,
                    path + " is larger than its own max size");
                Assert.GreaterOrEqual(
                    importer.maxTextureSize, 512,
                    path + " caps the texture at " + importer.maxTextureSize + " px, which would resample it");
            }
        }

        /// <summary>
        /// The stage's second sharpness rule: the camera renders at 1:1 and no profile this project
        /// authors turns depth of field on. Both were already true when the stage opened; the test is
        /// here so a later "make it look cinematic" pass cannot quietly undo them.
        /// </summary>
        /// <remarks>
        /// The scan is limited to <c>Assets/</c> on purpose. URP ships its own
        /// <c>Editor/Volume/DefaultVolumeProfile.asset</c> with every component — depth of field
        /// included — overridden, and that file is <b>not</b> the profile in force: the project's
        /// <c>UniversalRenderPipelineGlobalSettings</c> points <c>m_VolumeProfile</c> at
        /// <c>Assets/DefaultVolumeProfile.asset</c> (guid e1373abb…), and the package one is only the
        /// template behind <c>m_DefaultSettingsVolumeProfile</c>. Scanning the package would fail the
        /// build for a setting nothing renders with; the survey that proved no live volume carries a
        /// depth of field is in <c>docs/stages/08g-report.md</c> §1.
        /// </remarks>
        [Test]
        public void TheCameraRendersAtOneToOneAndNoProjectProfileHasDepthOfField()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline, "the project is not on URP");
            Assert.AreEqual(1f, pipeline.renderScale, 0.0001f, "the render scale is not 1.0");

            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    continue;
                }

                if (profile.TryGet<DepthOfField>(out DepthOfField depth) && depth.active)
                {
                    Assert.Fail(path + " turns depth of field on");
                }
            }
        }

        /// <summary>
        /// The night's own fog must not veil the arena. With the orthographic rig 60 units away, an
        /// exponential-squared density <c>d</c> leaves <c>1 − exp(−(d × 60)²)</c> of the fog colour on
        /// every pixel; the shipped night value was 0.015, i.e. 55 % of a flat dark blue over the whole
        /// frame — the "everything is muddy" the stage was opened for. The ceiling is 15 %.
        /// </summary>
        [Test]
        public void TheNightFogDoesNotVeilTheArena()
        {
            ArenaTheme theme = LoadTheme();
            Assert.IsNotNull(theme.NightProfile, "the theme has no night profile");
            Assert.IsNotNull(theme.DuskProfile, "the theme has no dusk profile");

            foreach (ArenaLightingProfile profile in new[] { theme.DuskProfile, theme.NightProfile })
            {
                if (!profile.FogEnabled)
                {
                    continue;
                }

                float distance = 60f;
                float veil = 1f - Mathf.Exp(-Mathf.Pow(profile.FogDensity * distance, 2f));
                Assert.LessOrEqual(
                    veil, 0.15f,
                    profile.name + " veils " + (veil * 100f).ToString("0.0") + "% of the frame at the camera's "
                    + distance + " unit distance (density " + profile.FogDensity + ")");
            }
        }

        /// <summary>
        /// The one hot colour in the arena is a pink-red, not a soft amber: the pools' hue (in
        /// <see cref="Color.RGBToHSV"/> terms) sits within 25° of 350°, i.e. red on the pink side.
        /// </summary>
        [Test]
        public void TheLightPoolsAreTheStagesPinkRed()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                ArenaArtBuilder.MaterialFolder + "/light-pool.mat");
            Assert.IsNotNull(material, "the light-pool material is missing");

            Color tint = material.GetColor("_BaseColor");
            Color.RGBToHSV(tint, out float hue, out float saturation, out _);
            float degrees = hue * 360f;

            Assert.GreaterOrEqual(saturation, 0.5f, "the pool tint is not saturated: " + tint);
            Assert.IsTrue(
                degrees >= 325f || degrees <= 15f,
                "the pool tint's hue is " + degrees.ToString("0") + "°, which is not the stage's pink-red");
        }
    }
}
