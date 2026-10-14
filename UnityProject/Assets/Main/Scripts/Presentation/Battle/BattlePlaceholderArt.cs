using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The battle scene's placeholder art, generated in code, and the one place a
    /// published entity becomes a sprite and a tint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 13: placeholder art must be licensed for use and must not be copied
    /// from a game the project references. Generating it — a filled disc for the
    /// ghost-doctor, a wavy-bottomed blob for a ghost, a dot and a diamond for shots and
    /// drops, a tiled grid for the floor — sidesteps both problems: the pixels are a
    /// function of this file, there is nothing to attribute, and no binary asset enters
    /// the repository (which also keeps the WebGL payload honest).
    /// </para>
    /// <para>
    /// Elite and boss are the ghost sprite scaled and recoloured, at exactly the scale
    /// factors the simulation gives their bodies
    /// (<c>BattleArena.EliteRadiusScale</c> / <c>BossRadiusScale</c>), so the silhouette a
    /// player sees is the hitbox the simulation uses. A Unity test asserts those two pairs
    /// still agree, because Presentation may not read the simulation assembly directly.
    /// </para>
    /// <para>
    /// Textures are generated once per scene and released by <see cref="Dispose"/>. They
    /// are 1 MiB of RGBA at most (the ground tile), which is why they are pooled rather
    /// than created per frame.
    /// </para>
    /// </remarks>
    public sealed class BattlePlaceholderArt : IDisposable
    {
        // Placeholder palette. Cosmetic only: nothing here is a balance number and
        // nothing here is read back by a rule.
        private static readonly Color32 Ink = new Color32(24, 20, 28, 255);
        private static readonly Color32 Doctor = new Color32(242, 226, 168, 255);
        private static readonly Color32 Ghost = new Color32(150, 196, 176, 255);
        private static readonly Color32 Bolt = new Color32(255, 214, 120, 255);
        private static readonly Color32 Drop = new Color32(255, 246, 210, 255);
        private static readonly Color32 Floor = new Color32(46, 46, 56, 255);
        private static readonly Color32 FloorLine = new Color32(60, 60, 74, 255);

        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>(8);

        /// <summary>Generates every sprite the battle needs.</summary>
        public BattlePlaceholderArt()
        {
            Player = CreateDisc("ps-battle-player", 64, Doctor);
            Enemy = CreateGhost("ps-battle-ghost", 64, Ghost);
            Projectile = CreateDisc("ps-battle-bolt", 24, Bolt);
            Pickup = CreateDiamond("ps-battle-drop", 24, Drop);
            Ground = CreateGroundTile("ps-battle-ground", 256, Floor, FloorLine);
        }

        /// <summary>The ghost-doctor.</summary>
        public Sprite Player { get; }

        /// <summary>A ghost (elite and boss reuse it, scaled and tinted).</summary>
        public Sprite Enemy { get; }

        /// <summary>A spell in flight.</summary>
        public Sprite Projectile { get; }

        /// <summary>A drop on the ground.</summary>
        public Sprite Pickup { get; }

        /// <summary>The floor tile, drawn once across the arena.</summary>
        public Sprite Ground { get; }

        /// <summary>The sprite for a published entity.</summary>
        public Sprite SpriteFor(RenderEntityKind kind)
        {
            switch (kind)
            {
                case RenderEntityKind.Player:
                    return Player;
                case RenderEntityKind.Enemy:
                    return Enemy;
                case RenderEntityKind.Projectile:
                    return Projectile;
                case RenderEntityKind.Pickup:
                    return Pickup;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The visual scale for a published entity: elite and boss are larger by the same
        /// factors the simulation gives their bodies, so the sprite matches the hitbox.
        /// </summary>
        public static float VisualScale(RenderEntityFlags flags)
        {
            if ((flags & RenderEntityFlags.Boss) != 0)
            {
                return BattleViewCaps.BossScale;
            }

            if ((flags & RenderEntityFlags.Elite) != 0)
            {
                return BattleViewCaps.EliteScale;
            }

            return 1f;
        }

        /// <summary>
        /// The tint for a published entity. A damaged ghost darkens towards ink, so a
        /// player can read a fight without health bars over every head.
        /// </summary>
        public static Color TintFor(RenderEntityKind kind, RenderEntityFlags flags, float healthPercent)
        {
            Color baseColor;
            switch (kind)
            {
                case RenderEntityKind.Player:
                    return Color.white;
                case RenderEntityKind.Enemy:
                    baseColor = (flags & RenderEntityFlags.Boss) != 0
                        ? new Color(0.78f, 0.30f, 0.32f, 1f)
                        : (flags & RenderEntityFlags.Elite) != 0
                            ? new Color(0.95f, 0.74f, 0.35f, 1f)
                            : Color.white;
                    break;
                default:
                    return Color.white;
            }

            float health = healthPercent < 0f ? 0f : (healthPercent > 1f ? 1f : healthPercent);
            return Color.Lerp(new Color(0.45f, 0.35f, 0.40f, 1f), baseColor, health);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                DestroyObject(_owned[i]);
            }

            _owned.Clear();
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value is null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        // ------------------------------------------------------------------
        // shape generators
        // ------------------------------------------------------------------

        private Sprite CreateDisc(string name, int size, Color32 fill)
        {
            Texture2D texture = NewTexture(name, size);
            Color32[] pixels = Filled(size, new Color32(0, 0, 0, 0));

            float radius = 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = ((x + 0.5f) / size) - 0.5f;
                    float v = ((y + 0.5f) / size) - 0.5f;
                    float distance = radius - Mathf.Sqrt((u * u) + (v * v));
                    pixels[(y * size) + x] = Antialias(distance, size, fill, Ink);
                }
            }

            return Finish(texture, pixels, size);
        }

        private Sprite CreateDiamond(string name, int size, Color32 fill)
        {
            Texture2D texture = NewTexture(name, size);
            Color32[] pixels = Filled(size, new Color32(0, 0, 0, 0));

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Abs(((x + 0.5f) / size) - 0.5f);
                    float v = Mathf.Abs(((y + 0.5f) / size) - 0.5f);
                    float distance = 0.44f - (u + v);
                    pixels[(y * size) + x] = Antialias(distance, size, fill, Ink);
                }
            }

            return Finish(texture, pixels, size);
        }

        private Sprite CreateGhost(string name, int size, Color32 fill)
        {
            Texture2D texture = NewTexture(name, size);
            Color32[] pixels = Filled(size, new Color32(0, 0, 0, 0));

            float radius = 0.30f;
            float centreY = 0.60f;
            float skirtBottom = 0.20f;
            float halfWidth = 0.30f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = ((x + 0.5f) / size) - 0.5f;
                    float v = ((y + 0.5f) / size) - 0.5f;

                    // Head: a disc around (0, centreY - 0.5). Body: a rectangle below it,
                    // closed by a four-lobed skirt so a ghost reads as a ghost at a glance.
                    float headDistance = radius - Mathf.Sqrt((u * u) + ((v - (centreY - 0.5f)) * (v - (centreY - 0.5f))));
                    float bodyDistance = v <= (centreY - 0.5f)
                        ? halfWidth - Mathf.Abs(u)
                        : float.MinValue;

                    float skirt = skirtBottom - 0.5f;
                    float wave = 0.06f * Mathf.Sin(u * 4f * Mathf.PI);
                    if (v < (skirt + wave) && bodyDistance > float.MinValue)
                    {
                        bodyDistance = Mathf.Min(
                            bodyDistance,
                            (v - (skirt + wave)) * 8f);
                    }

                    float distance = Mathf.Max(headDistance, bodyDistance);
                    pixels[(y * size) + x] = Antialias(distance, size, fill, Ink);
                }
            }

            return Finish(texture, pixels, size);
        }

        private Sprite CreateGroundTile(string name, int size, Color32 fill, Color32 line)
        {
            Texture2D texture = NewTexture(name, size);
            Color32[] pixels = Filled(size, fill);

            const int cell = 32;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x % cell == 0 || y % cell == 0)
                    {
                        pixels[(y * size) + x] = line;
                    }
                }
            }

            // The floor is drawn in Tiled mode, which repeats the sprite: its texture has to
            // wrap, or Unity tiles the sprite mesh but clamps the texture inside it.
            texture.wrapMode = TextureWrapMode.Repeat;

            return Finish(texture, pixels, size, pixelsPerUnit: cell);
        }

        private static Color32[] Filled(int size, Color32 value)
        {
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = value;
            }

            return pixels;
        }

        /// <summary>
        /// One pixel of a shape: opaque well inside the edge, transparent well outside,
        /// and a dark rim for the single pixel that straddles the edge. Without the rim a
        /// generated shape looks like a colour blob at 50° with no readable outline.
        /// </summary>
        private static Color32 Antialias(float insideDistance, int size, Color32 fill, Color32 ink)
        {
            float coverage = Mathf.Clamp01((insideDistance * size) + 0.5f);
            if (coverage <= 0f)
            {
                return new Color32(0, 0, 0, 0);
            }

            Color32 colour = coverage == 1f ? fill : Color32.Lerp(ink, fill, coverage);
            colour.a = (byte)(255f * coverage);
            return colour;
        }

        private Texture2D NewTexture(string name, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            _owned.Add(texture);
            return texture;
        }

        private Sprite Finish(Texture2D texture, Color32[] pixels, int size, float pixelsPerUnit = 64f)
        {
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);

            sprite.name = texture.name;
            _owned.Add(sprite);
            return sprite;
        }
    }
}
