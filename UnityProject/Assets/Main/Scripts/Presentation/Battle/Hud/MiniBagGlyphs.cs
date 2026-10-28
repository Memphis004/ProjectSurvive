using ProjectSurvive.Core.Domain.Config;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// The five category marks the mini ย่าม draws on a piece: one silhouette per
    /// <see cref="ItemCategory"/>, generated as a sprite at the floor's first build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a shape at all.</b> The box under the mark is already tinted by category
    /// (<see cref="MiniBagPalette.Category"/>), so colour alone answers "which kind of thing is this?"
    /// — for a player who can tell those five mid-tone colours apart at 1×1 cm on a moving screen.
    /// A silhouette answers the same question by <i>shape</i>, which survives a colour-blind player, a
    /// night floor and a screenshot scaled to 1280×720. The tier number that used to sit here is not
    /// the thing being read mid-fight; it stays on the bag's own cells and in the camp's จัดย่าม screen.
    /// </para>
    /// <para>
    /// <b>Generated, not imported.</b> These are masks drawn in code at 32 × 32 and tinted at runtime,
    /// so the bag needs no new texture asset and nothing has to be licensed (AGENTS rule 13). The
    /// sprites and their textures are owned by whichever <see cref="BattleMiniBagView"/> built them and
    /// destroyed with it — see <see cref="Dispose"/>.
    /// </para>
    /// <para>
    /// <b>Dark ink, not white.</b> Measured against the five category colours, a near-white glyph falls
    /// to 1.7 : 1 on the offering's gold while an ink glyph clears 3.5 : 1 on all five (WCAG, the same
    /// measure the arena's readability work uses). One tint for every category also means a piece's
    /// mark never has to be read against a second colour that changed with its category.
    /// </para>
    /// </remarks>
    public sealed class MiniBagGlyphs
    {
        /// <summary>Edge of a generated mask, in pixels. Drawn at ~30 px on a 1080p bag cell.</summary>
        public const int Size = 32;

        /// <summary>The ink every glyph is tinted with: dark enough for all five category colours.</summary>
        public static readonly Color Ink = new Color(0.10f, 0.09f, 0.12f, 1f);

        /// <summary>Height of a tier-pip strip in pixels: one 6 px dot inside a 1 px margin.</summary>
        public const int PipStripHeight = 8;

        /// <summary>Width of a tier-pip strip in pixels (three 6 px dots, 3 px gaps, 1 px margins).</summary>
        public const int PipStripWidth = 26;

        /// <summary>How many dots a tier strip can hold before it saturates on a full strip.</summary>
        public const int MaxPips = 3;

        private readonly Sprite[] _byCategory = new Sprite[7];
        private readonly Sprite[] _pips = new Sprite[4];
        private readonly Sprite _fallback;

        /// <summary>
        /// Builds every glyph once. Call from a floor's first build, never per frame.
        /// </summary>
        /// <param name="maxTier">
        /// The run's own <c>MaxTier</c> (<c>data/tables/constant.csv</c>, 3 today), so the strips cover
        /// every tier a merge can reach and a higher one can only saturate the last strip.
        /// </param>
        public MiniBagGlyphs(int maxTier)
        {
            int tiers = maxTier < 1 ? 1 : (maxTier > MaxPips ? MaxPips : maxTier);
            for (int tier = 1; tier <= tiers; tier++)
            {
                int dots = tier;
                _pips[tier] = Create("ps-minibag-tier" + tier, (x, y) => Pip(x, y, dots), PipStripHeight);
            }

            _byCategory[(int)ItemCategory.ArcaneWeapon] = Create("ps-minibag-arcane-weapon", Blade);
            _byCategory[(int)ItemCategory.Charm] = Create("ps-minibag-charm", Diamond);
            _byCategory[(int)ItemCategory.SpellTome] = Create("ps-minibag-spell-tome", Book);
            _byCategory[(int)ItemCategory.Offering] = Create("ps-minibag-offering", Bowl);
            _byCategory[(int)ItemCategory.Familiar] = Create("ps-minibag-familiar", Ember);
            _fallback = Create("ps-minibag-unknown", Frame);
        }

        /// <summary>
        /// The mark of <paramref name="category"/>, or the fallback frame for a category the tables
        /// do not name (the same case <see cref="MiniBagPalette.Unknown"/> colours).
        /// </summary>
        public Sprite For(ItemCategory category)
        {
            int index = (int)category;
            if (index >= 0 && index < _byCategory.Length && _byCategory[index] is not null)
            {
                return _byCategory[index];
            }

            return _fallback;
        }

        /// <summary>
        /// The strip of dots that shows an item's tier: one dot per tier, saturating at the highest
        /// strip this floor built.
        /// </summary>
        /// <remarks>
        /// A dot per level rather than a digit: at a 54 px box the number has to be 22 px to clear the
        /// project's type floor, and a 22 px digit in a corner either wears a light chip that hides the
        /// category mark or has nowhere to sit. Three dots are countable at a glance, take eight pixels of
        /// the corner's height, and stay clear of the mark (`data/tables/constant.csv` caps a tier at 3).
        /// </remarks>
        public Sprite TierPips(int tier)
        {
            int capped = tier < 1 ? 1 : (tier > MaxPips ? MaxPips : tier);
            return _pips[capped] ?? _pips[1] ?? _fallback;
        }

        /// <summary>How many distinct marks exist (the five categories plus the fallback).</summary>
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _byCategory.Length; i++)
                {
                    if (_byCategory[i] is not null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Destroys every generated sprite and texture. The owner calls this once.</summary>
        public void Dispose()
        {
            for (int i = 0; i < _byCategory.Length; i++)
            {
                Destroy(_byCategory[i]);
                _byCategory[i] = null;
            }

            for (int i = 0; i < _pips.Length; i++)
            {
                Destroy(_pips[i]);
                _pips[i] = null;
            }

            Destroy(_fallback);
        }

        private static void Destroy(Sprite sprite)
        {
            if (sprite is null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            if (Application.isPlaying)
            {
                Object.Destroy(sprite);
                if (texture is not null)
                {
                    Object.Destroy(texture);
                }

                return;
            }

            Object.DestroyImmediate(sprite);
            if (texture is not null)
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>One dot per tier, left to right, in a strip one dot tall.</summary>
        private static bool Pip(int x, int y, int dots)
        {
            // A 6 px dot with a 3 px gap: the strip is drawn at ~26 px on a 1080p box and at ~16 px on
            // the same box at 720p, where a 6/3 dot/gap still resolves into separate dots after the
            // bilinear minification (an 8/2 pair does not — it blurs into one bar).
            const int dot = 6;
            const int gap = 3;

            if (y < 1 || y >= PipStripHeight - 1)
            {
                return false;
            }

            for (int i = 0; i < dots; i++)
            {
                int left = 1 + (i * (dot + gap));
                if (x >= left && x < left + dot)
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite Create(string name, System.Func<int, int, bool> mask, int height = Size)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Size * Size];
            var ink = new Color32(
                (byte)Mathf.RoundToInt(Ink.r * 255f),
                (byte)Mathf.RoundToInt(Ink.g * 255f),
                (byte)Mathf.RoundToInt(Ink.b * 255f),
                255);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[(y * Size) + x] = mask(x, y) ? ink : new Color32(0, 0, 0, 0);
                }
            }

            // Kept readable on purpose: the mask is 32 × 32 (4 KB), and the bag's own test asserts that
            // each mark draws something rather than trusting the arithmetic that made it.
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return Sprite.Create(
                texture, new Rect(0f, 0f, Size, height), new Vector2(0.5f, 0.5f), 100f,
                extrude: 0, SpriteMeshType.FullRect);
        }

        // ------------------------------------------------------------------
        // The five masks. Each is drawn on a 1 … 30 grid inside the 32 × 32 texture, so no glyph
        // touches the edge and every one of them survives the bilinear filter at 30 px.
        // ------------------------------------------------------------------

        /// <summary>An arcane weapon: a dagger — pommel, grip, a short guard, and a tapering blade.</summary>
        private static bool Blade(int x, int y)
        {
            bool pommel = y >= 2 && y <= 4 && x >= 13 && x <= 18;
            bool grip = x >= 13 && x <= 18 && y >= 4 && y <= 10;

            // A guard that is wider than the blade but not a cross-bar across the whole glyph: at 30 px
            // a 16-pixel bar reads as a plus sign, not as a weapon. The blade is six pixels wide for the
            // same reason — a two-pixel edge is a hairline once the cell is drawn at 720p.
            bool guard = y >= 10 && y <= 12 && x >= 10 && x <= 21;
            bool blade = x >= 13 && x <= 18 && y >= 12 && y <= 26;

            // The point: the last four rows narrow to a two-pixel tip.
            int taper = y > 26 ? (y - 26) * 1 : 0;
            bool tip = y > 26 && y <= 30 && x >= 13 + taper && x <= 18 - taper;

            return pommel || grip || guard || blade || tip;
        }

        /// <summary>A charm: a diamond ring — the shape of a folded paper ยันต์, hollow in the middle.</summary>
        private static bool Diamond(int x, int y)
        {
            int distance = Mathf.Abs(x - 16) + Mathf.Abs(y - 16);
            return distance >= 5 && distance <= 13;
        }

        /// <summary>A spell tome: a closed book seen from the front, with its spine down the middle.</summary>
        private static bool Book(int x, int y)
        {
            bool cover = x >= 5 && x <= 26 && y >= 6 && y <= 25;
            bool spine = x >= 15 && x <= 16;
            bool edge = (x >= 5 && x <= 7) || (x >= 24 && x <= 26) || (y >= 6 && y <= 7) || (y >= 24 && y <= 25);

            // An outlined cover split by a gap down the middle: an open book. A filled slab, or an
            // outline with a solid spine, reads as a picture frame at this size.
            return cover && !edge && !spine;
        }

        /// <summary>An offering: a stemmed bowl, the shape of the tray on the sala's altar.</summary>
        private static bool Bowl(int x, int y)
        {
            bool baseBar = y >= 3 && y <= 5 && x >= 11 && x <= 20;
            bool stem = y >= 5 && y <= 11 && x >= 15 && x <= 16;

            // The bowl: widest at its rim (y = 26) and narrowing towards the stem.
            int inset = y >= 11 && y <= 26 ? (26 - y) / 3 : 99;
            bool body = y >= 11 && y <= 26 && x >= 5 + inset && x <= 26 - inset;

            return baseBar || stem || body;
        }

        /// <summary>A familiar: an ember — a round body with a tapering flame, the shape of a ภูต.</summary>
        private static bool Ember(int x, int y)
        {
            float dx = x - 16f;
            float dy = y - 12f;
            bool body = (dx * dx) + (dy * dy) <= 8f * 8f;

            // The flame: from the body's top up to a point, narrowing as it rises.
            float rise = y - 18f;
            bool flame = y > 18 && y <= 30 && Mathf.Abs(dx + (rise * 0.18f)) <= (12f - rise) * 0.55f;

            return body || flame;
        }

        /// <summary>The fallback: an empty frame, for a category the tables do not name.</summary>
        private static bool Frame(int x, int y)
        {
            bool outer = x >= 7 && x <= 24 && y >= 7 && y <= 24;
            bool inner = x >= 10 && x <= 21 && y >= 10 && y <= 21;
            return outer && !inner;
        }
    }
}
