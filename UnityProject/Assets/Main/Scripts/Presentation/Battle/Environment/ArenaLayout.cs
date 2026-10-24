using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>One piece the arena places: a standing card or a flat ground decal.</summary>
    /// <remarks>
    /// Plain data. <c>ArenaBuilder</c> turns it into a prefab instance, the tests turn it into
    /// assertions, and nothing at runtime reads it — the scene the builder saved is what the game
    /// shows. That split is what lets "same theme + same seed = same arena" be a unit test instead
    /// of a screenshot comparison.
    /// </remarks>
    public readonly struct ArenaPlacement
    {
        public ArenaPlacement(
            ArenaCardZone zone, int styleIndex, string styleId, float x, float z,
            float width, float height, float scale, float yaw)
        {
            Zone = zone;
            StyleIndex = styleIndex;
            StyleId = styleId;
            X = x;
            Z = z;
            Width = width;
            Height = height;
            Scale = scale;
            Yaw = yaw;
        }

        /// <summary>Which band of the arena this piece belongs to.</summary>
        public ArenaCardZone Zone { get; }

        /// <summary>Index into the theme's <c>Styles</c> (or <c>DecalStyles</c> for a decal).</summary>
        public int StyleIndex { get; }

        /// <summary>The style's stable id.</summary>
        public string StyleId { get; }

        /// <summary>World X.</summary>
        public float X { get; }

        /// <summary>World Z (the simulation's Y).</summary>
        public float Z { get; }

        /// <summary>World width at the chosen scale.</summary>
        public float Width { get; }

        /// <summary>World height at the chosen scale; a decal's footprint depth.</summary>
        public float Height { get; }

        /// <summary>The scale the layout chose.</summary>
        public float Scale { get; }

        /// <summary>Turn around the vertical axis, in degrees.</summary>
        public float Yaw { get; }

        /// <summary>Distance from the arena centre.</summary>
        public float Radius => Mathf.Sqrt((X * X) + (Z * Z));

        /// <summary>True for a flat ground decal.</summary>
        public bool IsDecal => Zone == ArenaCardZone.Decal;

        /// <summary>A stable, culture-invariant line, used by the determinism test.</summary>
        public string Signature =>
            Zone + ":" + StyleIndex + ":" + StyleId + ":" +
            X.ToString("0.0000", CultureInfo.InvariantCulture) + "," +
            Z.ToString("0.0000", CultureInfo.InvariantCulture) + "," +
            Scale.ToString("0.0000", CultureInfo.InvariantCulture) + "," +
            Yaw.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The one deterministic function from (theme, seed) to a list of arena pieces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deterministic in the strict sense the acceptance asks for: the same theme and the same seed
    /// produce the same list, in the same order, to four decimals, on any run. It uses its own
    /// 32-bit LCG rather than <c>UnityEngine.Random</c>, whose state is global and is advanced by
    /// anything else in the Editor; seeding that would make the arena depend on how many other
    /// tools ran first.
    /// </para>
    /// <para>
    /// The three zones are the design:
    /// <list type="bullet">
    /// <item><b>Centre</b> — only decals and low grass. The tallest piece is held to
    /// <see cref="ArenaTheme.CenterMaxHeight"/> and the origin keeps a small clear circle, because a
    /// survivor-like has to be readable where the player starts.</item>
    /// <item><b>Ring</b> — sparse bushes with a minimum spacing, so there are lanes through the
    /// foliage rather than a hedge.</item>
    /// <item><b>Border</b> — a wall of trees in two or three concentric layers, dense enough that the
    /// arena has an edge and cheap enough to stay inside the count cap.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static class ArenaLayout
    {
        /// <summary>How far from the exact centre a standing card has to stay, in world units.</summary>
        public const float CenterClearRadius = 2.4f;

        /// <summary>The largest number of tries a spacing rule may spend on one piece.</summary>
        private const int PlacementAttempts = 64;

        /// <summary>Builds the arena for <paramref name="seed"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
        public static ArenaPlacement[] Build(ArenaTheme theme, uint seed)
        {
            if (theme is null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            var placements = new List<ArenaPlacement>(256);
            var random = new ArenaRandom(seed);

            AddCenter(theme, ref random, placements);
            AddRing(theme, ref random, placements);
            AddBorder(theme, ref random, placements);
            AddDecals(theme, ref random, placements);

            return placements.ToArray();
        }

        /// <summary>
        /// Every rule the layout is supposed to obey. An empty list means the arena is legal.
        /// </summary>
        /// <remarks>
        /// Kept next to <see cref="Build"/> rather than in the builder so the rules are testable
        /// without a scene, a prefab or an Editor: the centre's height limit is the acceptance line
        /// “โซนกลางไม่มีของสูงเกินค่าที่กำหนด” and it is one loop over the placements.
        /// </remarks>
        public static List<string> Validate(ArenaTheme theme, IReadOnlyList<ArenaPlacement> placements)
        {
            if (theme is null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            var errors = new List<string>();
            if (placements is null)
            {
                errors.Add("the layout is null");
                return errors;
            }

            int centerGrass = 0, ringBushes = 0, borderTrees = 0;

            for (int i = 0; i < placements.Count; i++)
            {
                ArenaPlacement p = placements[i];

                if (p.Radius > theme.ArenaRadius + 0.001f)
                {
                    errors.Add(p.StyleId + " stands outside the arena at radius " + p.Radius.ToString("0.00"));
                }

                if (!p.IsDecal)
                {
                    ArenaCardStyle[] styles = theme.Styles;
                    if (p.StyleIndex < 0 || p.StyleIndex >= styles.Length)
                    {
                        errors.Add("a placement names style " + p.StyleIndex + ", which the theme does not have");
                        continue;
                    }

                    if (styles[p.StyleIndex].Id != p.StyleId)
                    {
                        errors.Add("a placement's id " + p.StyleId + " does not match its style row");
                    }

                    if (styles[p.StyleIndex].Zone == ArenaCardZone.Decal)
                    {
                        errors.Add(p.StyleId + " is a decal style placed as a standing card");
                    }

                    // Only the centre's own low grass may stand in the open middle: a bush or a tree
                    // there is exactly the thing the fight is supposed to be read against.
                    if (p.Zone != ArenaCardZone.Center && p.Radius < theme.CenterRadius - 0.001f)
                    {
                        errors.Add(
                            p.StyleId + " is a standing card inside the " + theme.CenterRadius.ToString("0.00")
                            + " unit centre, at radius " + p.Radius.ToString("0.00"));
                    }
                }

                switch (p.Zone)
                {
                    case ArenaCardZone.Center:
                        centerGrass++;
                        if (p.Height > theme.CenterMaxHeight + 0.001f)
                        {
                            errors.Add(
                                p.StyleId + " is " + p.Height.ToString("0.00")
                                + " tall inside the centre, above the " + theme.CenterMaxHeight.ToString("0.00")
                                + " ceiling");
                        }

                        break;

                    case ArenaCardZone.Ring:
                        ringBushes++;
                        break;

                    case ArenaCardZone.Border:
                        borderTrees++;
                        if (p.Radius < theme.RingOuterRadius - 0.001f)
                        {
                            errors.Add(p.StyleId + " is a border tree inside the ring at radius " + p.Radius.ToString("0.00"));
                        }

                        break;
                }

                if (p.IsDecal && p.Height > 0.05f)
                {
                    errors.Add(p.StyleId + " is a decal more than 5 cm above the floor");
                }
            }

            if (centerGrass > theme.CenterGrassMax)
            {
                errors.Add("the centre has " + centerGrass + " pieces, over its cap of " + theme.CenterGrassMax);
            }

            if (ringBushes > theme.RingBushMax)
            {
                errors.Add("the ring has " + ringBushes + " bushes, over its cap of " + theme.RingBushMax);
            }

            if (borderTrees > theme.BorderTreeMax)
            {
                errors.Add("the border has " + borderTrees + " trees, over its cap of " + theme.BorderTreeMax);
            }

            // Spacing: a bush is checked against every other bush, a tree against every other tree.
            // The centre's grass is deliberately exempt — low tufts may grow together.
            for (int i = 0; i < placements.Count; i++)
            {
                ArenaPlacement a = placements[i];
                if (a.Zone != ArenaCardZone.Ring && a.Zone != ArenaCardZone.Border)
                {
                    continue;
                }

                float minimum = a.Zone == ArenaCardZone.Ring ? theme.BushMinSpacing : theme.TreeMinSpacing;
                for (int j = i + 1; j < placements.Count; j++)
                {
                    ArenaPlacement b = placements[j];
                    if (b.Zone != a.Zone)
                    {
                        continue;
                    }

                    float dx = a.X - b.X;
                    float dz = a.Z - b.Z;
                    float distance = Mathf.Sqrt((dx * dx) + (dz * dz));
                    if (distance < minimum - 0.001f)
                    {
                        errors.Add(
                            a.StyleId + " and " + b.StyleId + " are " + distance.ToString("0.00")
                            + " apart, below the " + minimum.ToString("0.00") + " minimum");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// The centre: low grass on a disc that keeps a clear circle around the origin, plus the
        /// big dirt patch the player starts on (a decal, added by <see cref="AddDecals"/>).
        /// </summary>
        private static void AddCenter(ArenaTheme theme, ref ArenaRandom random, List<ArenaPlacement> into)
        {
            ArenaCardStyle[] styles = StylesIn(theme.Styles, ArenaCardZone.Center);
            if (styles.Length == 0)
            {
                return;
            }

            int count = random.NextInt(theme.CenterGrassMin, theme.CenterGrassMax);
            for (int i = 0; i < count; i++)
            {
                ArenaCardStyle style = styles[random.NextInt(0, styles.Length - 1)];
                float scale = random.Range(style.MinScale, style.MaxScale);

                // The ceiling is the point of this zone: if a style would poke through it at this
                // scale, shrink it to fit — a taller style simply is not a centre style.
                if (style.WorldHeight(scale) > theme.CenterMaxHeight)
                {
                    scale = style.Height <= 0.0001f ? scale : theme.CenterMaxHeight / style.Height;
                }

                if (style.WorldHeight(scale) > theme.CenterMaxHeight + 0.001f)
                {
                    continue;
                }

                float angle = random.Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(random.Range(0.15f, 1f)) * theme.CenterRadius;
                radius = Mathf.Max(radius, CenterClearRadius + 0.5f);
                if (radius >= theme.CenterRadius)
                {
                    continue;
                }

                into.Add(Placement(
                    ArenaCardZone.Center, theme.Styles, style, style.Width * scale, style.WorldHeight(scale),
                    scale,
                    Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                    random.Range(-theme.MaxYawDegrees, theme.MaxYawDegrees)));
            }
        }

        /// <summary>The ring: sparse bushes with a minimum spacing, so the middle keeps its lanes.</summary>
        private static void AddRing(ArenaTheme theme, ref ArenaRandom random, List<ArenaPlacement> into)
        {
            ArenaCardStyle[] styles = StylesIn(theme.Styles, ArenaCardZone.Ring);
            if (styles.Length == 0)
            {
                return;
            }

            int target = random.NextInt(theme.RingBushMin, theme.RingBushMax);
            int placed = 0;

            for (int i = 0; i < target; i++)
            {
                ArenaCardStyle style = styles[random.NextInt(0, styles.Length - 1)];
                float scale = random.Range(style.MinScale, style.MaxScale);

                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    float angle = random.Range(0f, Mathf.PI * 2f);
                    float radius = random.Range(theme.CenterRadius, theme.RingOuterRadius);
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;

                    if (!HasRoom(into, x, z, theme.BushMinSpacing, ArenaCardZone.Ring))
                    {
                        continue;
                    }

                    into.Add(Placement(
                        ArenaCardZone.Ring, theme.Styles, style, style.Width * scale, style.WorldHeight(scale),
                        scale, x, z,
                        random.Range(-theme.MaxYawDegrees, theme.MaxYawDegrees)));
                    placed++;
                    break;
                }
            }

            // A dense theme may fail to find room; the cap is a ceiling, not a promise. What must
            // hold is that the ring is never over the cap, which it cannot be: `target` is ≤ it.
            _ = placed;
        }

        /// <summary>
        /// The border: two or three concentric layers of trees, dense enough to be a wall.
        /// </summary>
        /// <remarks>
        /// A layer's slot count comes from the width of the cards it is made of, not from a spacing
        /// number: one slot per <c>averageWidth × BorderSlotPitchFactor</c> of circumference. That is
        /// the difference between a row of isolated trees and a wall — at a pitch below the card width
        /// the neighbours overlap and the layer reads as foliage with no gaps. The number of layers,
        /// the pitch factor and the count cap are all the theme's, and the wall is still a ring of
        /// slots rather than a rejection loop that could fail to converge.
        /// </remarks>
        private static void AddBorder(ArenaTheme theme, ref ArenaRandom random, List<ArenaPlacement> into)
        {
            ArenaCardStyle[] styles = StylesIn(theme.Styles, ArenaCardZone.Border);
            if (styles.Length == 0)
            {
                return;
            }

            int target = random.NextInt(theme.BorderTreeMin, theme.BorderTreeMax);
            int layers = Mathf.Clamp(theme.BorderLayers, 2, 3);
            float band = Mathf.Max(0.1f, theme.ArenaRadius - theme.RingOuterRadius);

            var layerRadius = new float[layers];
            var layerSlots = new int[layers];
            int totalSlots = 0;
            float pitch = Mathf.Max(1f, AverageWidth(styles) * Mathf.Max(0.5f, theme.BorderSlotPitchFactor));

            for (int layer = 0; layer < layers; layer++)
            {
                layerRadius[layer] = theme.RingOuterRadius + ((layer + 0.5f) * band / layers);
                int slots = Mathf.Max(4, Mathf.FloorToInt((Mathf.PI * 2f * layerRadius[layer]) / pitch));
                layerSlots[layer] = slots;
                totalSlots += slots;
            }

            // Trim the wall to the theme's cap by evenly taking slots away, never below one per slot
            // layer (a layer is only skipped if the cap is so low that it cannot hold all of them).
            int budget = Mathf.Min(target, totalSlots);
            int allocated = 0;
            for (int layer = 0; layer < layers; layer++)
            {
                int share = Mathf.Max(1, Mathf.RoundToInt((float)layerSlots[layer] / totalSlots * budget));
                layerSlots[layer] = Mathf.Min(layerSlots[layer], share);
                allocated += layerSlots[layer];
            }

            for (int layer = 0; layer < layers && allocated > target; layer++)
            {
                int cut = Mathf.Min(layerSlots[layer] - 1, allocated - target);
                layerSlots[layer] -= cut;
                allocated -= cut;
            }

            for (int layer = 0; layer < layers; layer++)
            {
                int slots = layerSlots[layer];
                float step = (Mathf.PI * 2f) / Mathf.Max(1, slots);
                float angleOffset = random.Range(0f, Mathf.PI * 2f);

                for (int slot = 0; slot < slots; slot++)
                {
                    ArenaCardStyle style = styles[random.NextInt(0, styles.Length - 1)];
                    float scale = random.Range(style.MinScale, style.MaxScale);
                    float angle = angleOffset + (slot * step) + random.Range(-0.22f * step, 0.22f * step);
                    float radius = layerRadius[layer] + random.Range(-0.35f, 0.35f) * (band / layers);
                    radius = Mathf.Min(radius, theme.ArenaRadius - 0.2f);

                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;

                    if (!HasRoom(into, x, z, theme.TreeMinSpacing, ArenaCardZone.Border))
                    {
                        continue;
                    }

                    into.Add(Placement(
                        ArenaCardZone.Border, theme.Styles, style, style.Width * scale, style.WorldHeight(scale),
                        scale, x, z,
                        random.Range(-theme.MaxYawDegrees, theme.MaxYawDegrees)));
                }
            }
        }

        /// <summary>Decals: a few large dirt patches, then sparse flowers and leaves.</summary>
        private static void AddDecals(ArenaTheme theme, ref ArenaRandom random, List<ArenaPlacement> into)
        {
            ArenaCardStyle[] dirt = StylesIn(theme.DecalStyles, ArenaCardZone.Decal);
            if (dirt.Length == 0)
            {
                return;
            }

            // The patch at the origin is deliberate: it is the mark the player starts on and the
            // one reference that tells a new player which way the arena faces. It is a *patch*, not
            // a floor covering — a decal that spans the whole view would change the colour every
            // fight is read against.
            AddDecalAt(theme, ref random, into, dirt[0], 0f, 0f, 1.6f);

            for (int i = 0; i < theme.DirtDecalCount; i++)
            {
                float angle = random.Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(random.Range(0.05f, 1f)) * theme.RingOuterRadius;
                AddDecalAt(
                    theme, ref random, into, dirt[random.NextInt(0, dirt.Length - 1)],
                    Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                    random.Range(0.60f, 1.10f));
            }

            AddScatter(theme, ref random, into, ArenaCardZone.Decal, theme.FlowerDecalCount);
            AddScatter(theme, ref random, into, ArenaCardZone.Decal, theme.LeafDecalCount);
        }

        private static void AddScatter(
            ArenaTheme theme, ref ArenaRandom random, List<ArenaPlacement> into,
            ArenaCardZone zone, int count)
        {
            ArenaCardStyle[] styles = StylesIn(theme.DecalStyles, zone);
            if (styles.Length == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ArenaCardStyle style = styles[random.NextInt(0, styles.Length - 1)];
                float angle = random.Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(random.Range(0.02f, 1f)) * theme.RingOuterRadius;
                float scale = random.Range(style.MinScale, style.MaxScale);

                into.Add(Placement(
                    ArenaCardZone.Decal, theme.DecalStyles, style, style.Width * scale, 0.02f,
                    scale, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                    random.Range(0f, 360f)));
            }
        }

        private static void AddDecalAt(
            ArenaTheme theme, ref ArenaRandom random, List<ArenaPlacement> into,
            ArenaCardStyle style, float x, float z, float scale)
        {
            into.Add(Placement(
                ArenaCardZone.Decal, theme.DecalStyles, style, style.Width * scale, 0.02f,
                scale, x, z, random.Range(0f, 360f)));
        }

        private static ArenaPlacement Placement(
            ArenaCardZone zone, ArenaCardStyle[] styles, ArenaCardStyle style,
            float width, float height, float scale, float x, float z, float yaw)
        {
            return new ArenaPlacement(zone, Array.IndexOf(styles, style), style.Id, x, z, width, height, scale, yaw);
        }

        /// <summary>The average world width of a style set, over each style's own scale range.</summary>
        private static float AverageWidth(ArenaCardStyle[] styles)
        {
            float total = 0f;
            for (int i = 0; i < styles.Length; i++)
            {
                total += styles[i].Width * 0.5f * (styles[i].MinScale + styles[i].MaxScale);
            }

            return styles.Length == 0 ? 0f : total / styles.Length;
        }

        private static ArenaCardStyle[] StylesIn(ArenaCardStyle[] styles, ArenaCardZone zone)
        {
            if (styles is null || styles.Length == 0)
            {
                return Array.Empty<ArenaCardStyle>();
            }

            var result = new List<ArenaCardStyle>(styles.Length);
            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] is not null && styles[i].Zone == zone && styles[i].Material is not null)
                {
                    result.Add(styles[i]);
                }
            }

            return result.ToArray();
        }

        private static bool HasRoom(
            List<ArenaPlacement> placements, float x, float z, float minimum, ArenaCardZone zone)
        {
            float squared = minimum * minimum;
            for (int i = 0; i < placements.Count; i++)
            {
                ArenaPlacement other = placements[i];
                if (other.Zone != zone)
                {
                    continue;
                }

                float dx = placements[i].X - x;
                float dz = placements[i].Z - z;
                if ((dx * dx) + (dz * dz) < squared)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A tiny deterministic LCG (Numerical Recipes' constants). Not cryptographic, not shared
        /// with the game: the arena's layout must not depend on the shop's or the battle's streams,
        /// and rebuilding it must not advance them.
        /// </summary>
        private struct ArenaRandom
        {
            private uint _state;

            public ArenaRandom(uint seed)
            {
                // Zero is a fixed point for this LCG; a zero seed would otherwise produce one
                // repeated value for the whole arena.
                _state = seed == 0u ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                _state = unchecked((_state * 1664525u) + 1013904223u);
                return (_state >> 8) * (1f / 16777216f);
            }

            public float Range(float minimum, float maximum) => minimum + ((maximum - minimum) * Next());

            public int NextInt(int minimum, int maximumInclusive)
            {
                if (maximumInclusive <= minimum)
                {
                    return minimum;
                }

                return minimum + (int)(Next() * (maximumInclusive - minimum + 1));
            }
        }
    }
}
