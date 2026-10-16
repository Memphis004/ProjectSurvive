using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// The battle HUD's placeholder art, generated in code: the figure in the bottom-left status
    /// panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 13: placeholder art must be licensed for use and must not be copied from a game
    /// the project references. Generating it — a bust drawn from primitives — sidesteps both
    /// problems: the pixels are a function of this file, there is nothing to attribute, and no binary
    /// asset enters the repository (which also keeps the WebGL payload honest). Same approach, and the
    /// same reasons, as <c>BattlePlaceholderArt</c> and <c>CampPlaceholderArt</c>.
    /// </para>
    /// <para>
    /// The figure is a <b>silhouette</b> of the same shape language the world already uses for the
    /// ghost-doctor (a covered head over a cloth body), not a portrait of anyone: nothing here claims
    /// to depict a real person or a real rite.
    /// </para>
    /// </remarks>
    public sealed class BattleHudArt : IDisposable
    {
        // Cosmetic palette, the camp's own cloth and shawl colours so the HUD reads as one world.
        private static readonly Color32 Cloth = new Color32(140, 62, 68, 255);
        private static readonly Color32 Shawl = new Color32(214, 200, 176, 255);
        private static readonly Color32 Ink = new Color32(24, 20, 28, 255);

        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>(4);

        /// <summary>Generates the HUD's sprites.</summary>
        public BattleHudArt()
        {
            Bust = CreateBust("ps-hud-bust", 128, Cloth, Shawl);
        }

        /// <summary>The figure in the status panel, head and shoulders.</summary>
        public Sprite Bust { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] is Texture2D texture)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                else if (_owned[i] is Sprite sprite)
                {
                    UnityEngine.Object.Destroy(sprite);
                }
            }

            _owned.Clear();
        }

        /// <summary>
        /// A head-and-shoulders silhouette: shawl over the head, cloth below, on a transparent
        /// ground.
        /// </summary>
        private Sprite CreateBust(string name, int size, Color32 cloth, Color32 shawl)
        {
            Texture2D texture = NewTexture(name, size, size);
            var pixels = new Color32[size * size];
            var clear = new Color32(0, 0, 0, 0);

            float centreX = size * 0.5f;
            float headTop = size * 0.94f;
            float headRadius = size * 0.19f;
            float shoulderTop = size * 0.56f;
            float shoulderHalf = size * 0.40f;
            float hem = 0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - centreX;
                    float dy = y + 0.5f;
                    Color32 colour = clear;

                    float headDy = dy - (headTop - headRadius);
                    if ((dx * dx) + (headDy * headDy) <= headRadius * headRadius)
                    {
                        // The shawl covers the head; the face is a shallow cloth opening.
                        bool face = Mathf.Abs(dx) < headRadius * 0.5f && headDy < headRadius * 0.25f;
                        colour = face ? cloth : shawl;
                    }
                    else if (dy <= shoulderTop && dy >= hem)
                    {
                        float t = (dy - hem) / (shoulderTop - hem);
                        float half = Mathf.Lerp(shoulderHalf, shoulderHalf * 0.86f, t);
                        if (Mathf.Abs(dx) <= half)
                        {
                            colour = dy > shoulderTop * 0.66f ? shawl : cloth;
                        }
                    }

                    pixels[(y * size) + x] = colour;
                }
            }

            Reticulate(pixels, size, size, clear, Ink);
            return Finish(texture, pixels, size, size);
        }

        /// <summary>An outline around every opaque pixel that touches a transparent one.</summary>
        private static void Reticulate(Color32[] pixels, int width, int height, Color32 clear, Color32 ink)
        {
            var edges = new List<int>(width);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (pixels[index].a == 0)
                    {
                        continue;
                    }

                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1
                        || pixels[index - 1].a == 0 || pixels[index + 1].a == 0
                        || pixels[index - width].a == 0 || pixels[index + width].a == 0;

                    if (border)
                    {
                        edges.Add(index);
                    }
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                pixels[edges[i]] = ink;
            }
        }

        private Texture2D NewTexture(string name, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            _owned.Add(texture);
            return texture;
        }

        private Sprite Finish(Texture2D texture, Color32[] pixels, int width, int height)
        {
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f,
                0u,
                SpriteMeshType.FullRect);

            sprite.name = texture.name;
            _owned.Add(sprite);
            return sprite;
        }
    }
}
