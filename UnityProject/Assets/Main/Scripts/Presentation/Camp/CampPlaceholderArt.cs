using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The camp scene's placeholder art, generated in code: the ground, the stall, its
    /// price boards, ยายจันทร์ herself and the incense pot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 13: placeholder art must be licensed for use and must not be copied from a game
    /// the project references. Generating it — flat rectangles with a lit top edge for the stall,
    /// a cloth-and-shawl silhouette for the grandmother, a rimmed disc for the pot — sidesteps both
    /// problems: the pixels are a function of this file, there is nothing to attribute, and no
    /// binary asset enters the repository (which also keeps the WebGL payload honest). Same
    /// approach, and the same reasons, as <c>BattlePlaceholderArt</c>.
    /// </para>
    /// <para>
    /// The grandmother is a <b>silhouette</b>, not a portrait: a fictional fortune-teller shape
    /// (shawl, sitting posture) drawn from primitives. Nothing here claims to depict a real
    /// ceremony or a real person.
    /// </para>
    /// </remarks>
    public sealed class CampPlaceholderArt : IDisposable
    {
        // Cosmetic palette. Nothing here is a balance number and nothing reads it back.
        // Stage 09o lit the sala: the yard is a clearing at dusk, so the packed earth is a warm
        // brown rather than the near-black it was when this scene had no environment at all. The
        // stipple stays a stipple; nothing here is a balance number.
        private static readonly Color32 Dusk = new Color32(64, 51, 45, 255);
        private static readonly Color32 DuskLine = new Color32(88, 72, 60, 255);
        private static readonly Color32 Wood = new Color32(96, 66, 44, 255);
        private static readonly Color32 WoodTop = new Color32(132, 96, 62, 255);
        private static readonly Color32 Board = new Color32(206, 186, 144, 255);
        private static readonly Color32 BoardEdge = new Color32(96, 78, 54, 255);
        private static readonly Color32 Cloth = new Color32(140, 62, 68, 255);
        private static readonly Color32 Shawl = new Color32(214, 200, 176, 255);
        private static readonly Color32 Brass = new Color32(196, 158, 84, 255);

        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>(8);

        /// <summary>Generates every sprite the camp needs.</summary>
        public CampPlaceholderArt()
        {
            Ground = CreateGroundTile("ps-camp-ground", 256, Dusk, DuskLine);
            Stall = CreateBoard("ps-camp-stall", 512, 256, Wood, WoodTop);
            Sign = CreateBoard("ps-camp-sign", 256, 192, Board, BoardEdge);
            Grandmother = CreateGrandmother("ps-camp-grandmother");
            Pot = CreatePot("ps-camp-pot", 128, Brass);
            Player = CreateStandingFigure("ps-camp-player", 128, Cloth, Shawl);
            Shelf = CreateShelf("ps-camp-shelf", 256, 192, Wood, WoodTop, BoardEdge);
            ShelfItem = CreateBoard("ps-camp-shelf-item", 128, 128, Board, BoardEdge);
            Drum = CreateDrum("ps-camp-drum", 192, 128, Cloth, Board);
        }

        /// <summary>The dirt around the pavilion, tiled.</summary>
        public Sprite Ground { get; }

        /// <summary>The stall's table.</summary>
        public Sprite Stall { get; }

        /// <summary>A price board.</summary>
        public Sprite Sign { get; }

        /// <summary>ยายจันทร์, in silhouette.</summary>
        public Sprite Grandmother { get; }

        /// <summary>The incense pot the altar drop zone sits on.</summary>
        public Sprite Pot { get; }

        /// <summary>The player, in silhouette: the same shape language as ยายจันทร์, standing.</summary>
        public Sprite Player { get; }

        /// <summary>One shelf: a rack with two lit boards.</summary>
        public Sprite Shelf { get; }

        /// <summary>The plate a shelf's offered piece sits on.</summary>
        public Sprite ShelfItem { get; }

        /// <summary>The เซียมซี cylinder the chest's re-roll is drawn from.</summary>
        public Sprite Drum { get; }

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

        // ------------------------------------------------------------------
        // generators
        // ------------------------------------------------------------------

        private Sprite CreateGroundTile(string name, int size, Color32 fill, Color32 line)
        {
            Texture2D texture = NewTexture(name, size, size);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // A sparse stipple rather than a grid: the pavilion floor is packed earth, and a
                    // grid would read as tiles from the tilted camera.
                    bool stipple = ((x * 73) ^ (y * 151)) % 97 == 0;
                    pixels[(y * size) + x] = stipple ? line : fill;
                }
            }

            return Finish(texture, pixels, size, size, pixelsPerUnit: 64f);
        }

        /// <summary>
        /// A slab: a bordered rectangle with one lit edge. The texture is exactly the slab, so the
        /// sprite's own aspect ratio is the slab's — a renderer can then scale it by its real size
        /// in world units without a transparent margin to compensate for.
        /// </summary>
        private Sprite CreateBoard(string name, int width, int height, Color32 fill, Color32 edge)
        {
            Texture2D texture = NewTexture(name, width, height);
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = x < 4 || y < 4 || x >= width - 4 || y >= height - 4;
                    bool lit = y >= height - 14;
                    pixels[(y * width) + x] = border ? edge : (lit ? Lighten(fill, 24) : fill);
                }
            }

            return Finish(texture, pixels, width, height, pixelsPerUnit: 64f);
        }

        /// <summary>
        /// ยายจันทร์: a seated silhouette — shawl over the head and shoulders, cloth below.
        /// </summary>
        private Sprite CreateGrandmother(string name)
        {
            const int size = 128;
            Texture2D texture = NewTexture(name, size, size);
            var pixels = new Color32[size * size];
            var clear = new Color32(0, 0, 0, 0);

            float centerX = size * 0.5f;
            float headTop = size * 0.92f;
            float headRadius = size * 0.15f;
            float shoulderTop = size * 0.60f;
            float shoulderHalf = size * 0.26f;
            float hem = size * 0.06f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centerX;
                    float dy = y + 0.5f;
                    Color32 colour = clear;

                    float headDy = dy - (headTop - headRadius);
                    if ((dx * dx) + (headDy * headDy) <= headRadius * headRadius)
                    {
                        // The head is covered by the shawl; the face opening is a small cloth gap.
                        colour = Mathf.Abs(dx) < headRadius * 0.45f && headDy < headRadius * 0.2f
                            ? Cloth
                            : Shawl;
                    }
                    else if (dy <= shoulderTop && dy >= hem)
                    {
                        // Shoulders taper out and the cloth falls straight to the hem.
                        float t = (dy - hem) / (shoulderTop - hem);
                        float half = Mathf.Lerp(shoulderHalf * 0.8f, shoulderHalf, t);
                        if (Mathf.Abs(dx) <= half)
                        {
                            colour = dy > shoulderTop * 0.62f ? Shawl : Cloth;
                        }
                    }

                    pixels[(y * size) + x] = colour;
                }
            }

            return Finish(texture, pixels, size, size, pixelsPerUnit: 64f);
        }

        /// <summary>
        /// A standing figure: cloth body, shawl over the shoulders and a covered head.
        /// </summary>
        /// <remarks>
        /// Deliberately the same construction as ยายจันทร์ (a pose drawn from primitives) so the two
        /// read as the same world. The player is the taller, narrower one, which is what tells them
        /// apart at the camera's distance — the colour is secondary.
        /// </remarks>
        private Sprite CreateStandingFigure(string name, int size, Color32 cloth, Color32 shawl)
        {
            Texture2D texture = NewTexture(name, size, size);
            var pixels = new Color32[size * size];
            var clear = new Color32(0, 0, 0, 0);

            float centerX = size * 0.5f;
            float headTop = size * 0.94f;
            float headRadius = size * 0.13f;
            float shoulderTop = size * 0.66f;
            float shoulderHalf = size * 0.22f;
            float hem = size * 0.04f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centerX;
                    float dy = y + 0.5f;
                    Color32 colour = clear;

                    float headDy = dy - (headTop - headRadius);
                    if ((dx * dx) + (headDy * headDy) <= headRadius * headRadius)
                    {
                        colour = headDy < headRadius * 0.15f ? cloth : shawl;
                    }
                    else if (dy <= shoulderTop && dy >= hem)
                    {
                        float t = (dy - hem) / (shoulderTop - hem);
                        float half = Mathf.Lerp(shoulderHalf * 0.62f, shoulderHalf, t);
                        if (Mathf.Abs(dx) <= half)
                        {
                            colour = dy > shoulderTop * 0.74f ? shawl : cloth;
                        }
                    }

                    pixels[(y * size) + x] = colour;
                }
            }

            return Finish(texture, pixels, size, size, pixelsPerUnit: 64f);
        }

        /// <summary>
        /// A shelf: two lit boards on posts, so a station reads as furniture rather than a sign.
        /// </summary>
        private Sprite CreateShelf(string name, int width, int height, Color32 frame, Color32 lit, Color32 post)
        {
            Texture2D texture = NewTexture(name, width, height);
            var pixels = new Color32[width * height];
            int postWidth = Mathf.Max(6, width / 14);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool leftPost = x < postWidth;
                    bool rightPost = x >= width - postWidth;
                    bool board = (y >= height * 0.30f && y < height * 0.40f)
                        || (y >= height * 0.62f && y < height * 0.72f);
                    bool topEdge = y >= height - 5;

                    pixels[(y * width) + x] = (leftPost || rightPost) ? post : (board ? lit : (topEdge ? lit : frame));
                }
            }

            return Finish(texture, pixels, width, height, pixelsPerUnit: 64f);
        }

        /// <summary>
        /// The เซียมซี cylinder: a body of sticks with a rim, drawn from primitives.
        /// </summary>
        /// <remarks>
        /// A fictional fortune-drawing prop (AGENTS rule 13): a tube of lot sticks, nothing that
        /// claims to depict a real ritual object.
        /// </remarks>
        private Sprite CreateDrum(string name, int width, int height, Color32 body, Color32 sticks)
        {
            Texture2D texture = NewTexture(name, width, height);
            var pixels = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            float centerX = width * 0.5f;
            float halfWidth = width * 0.30f;
            int bodyTop = Mathf.RoundToInt(height * 0.72f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - centerX);
                    Color32 colour = clear;

                    if (y < bodyTop && dx <= halfWidth)
                    {
                        bool rim = y >= bodyTop - 8 || y < 8;
                        colour = rim ? sticks : body;
                    }
                    else if (y >= bodyTop)
                    {
                        // The sticks: a few narrow columns rising out of the mouth.
                        int column = Mathf.RoundToInt((x + 0.5f - centerX) / (width * 0.075f));
                        bool isStick = dx < halfWidth * 0.85f && (column & 1) == 0;
                        if (isStick)
                        {
                            colour = sticks;
                        }
                    }

                    pixels[(y * width) + x] = colour;
                }
            }

            return Finish(texture, pixels, width, height, pixelsPerUnit: 64f);
        }

        private Sprite CreatePot(string name, int size, Color32 rim)
        {
            const int inner = 16;
            Texture2D texture = NewTexture(name, size, size);
            var pixels = new Color32[size * size];
            var clear = new Color32(0, 0, 0, 0);
            float radius = (size * 0.5f) - inner;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - (size * 0.5f);
                    float dy = y + 0.5f - (size * 0.5f);
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                    if (distance > radius)
                    {
                        pixels[(y * size) + x] = clear;
                    }
                    else
                    {
                        // A brass rim around a hollow mouth, so the drop zone reads as a vessel.
                        pixels[(y * size) + x] = distance > radius * 0.72f
                            ? rim
                            : new Color32(58, 40, 30, 255);
                    }
                }
            }

            return Finish(texture, pixels, size, size, pixelsPerUnit: 64f);
        }

        private static Color32 Lighten(Color32 colour, int amount) => new Color32(
            (byte)Mathf.Min(255, colour.r + amount),
            (byte)Mathf.Min(255, colour.g + amount),
            (byte)Mathf.Min(255, colour.b + amount),
            colour.a);

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

        private Sprite Finish(Texture2D texture, Color32[] pixels, int width, int height, float pixelsPerUnit)
        {
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
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
