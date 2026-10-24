using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>One group's readability numbers, measured on a rendered frame.</summary>
    public readonly struct ContrastSample
    {
        public ContrastSample(float feature, float floor, int featurePixels, int floorPixels)
        {
            Feature = feature;
            Floor = floor;
            Ratio = ArenaContrast.Ratio(feature, floor);
            FeaturePixels = featurePixels;
            FloorPixels = floorPixels;
        }

        /// <summary>Relative luminance of the group (0 … 1): the bright end of its silhouette.</summary>
        public float Feature { get; }

        /// <summary>Relative luminance of the floor around it (0 … 1).</summary>
        public float Floor { get; }

        /// <summary>WCAG contrast ratio, 1 … 21.</summary>
        public float Ratio { get; }

        /// <summary>Pixels the feature was sampled from.</summary>
        public int FeaturePixels { get; }

        /// <summary>Pixels the floor was sampled from.</summary>
        public int FloorPixels { get; }
    }

    /// <summary>
    /// Measures how far a group of pixels stands out from the floor around it, on a captured frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 08f's readability rule is a number, not an opinion: "ผู้เล่น ผี กระสุน pickup telegraph
    /// ต้องแยกจากพื้น". The measurement is the WCAG relative-luminance contrast ratio
    /// <c>(Lbright + 0.05) / (Ldark + 0.05)</c> — the same formula a UI contrast check uses — taken
    /// from the pixels the renderer actually produced. The group's value is the 90th percentile of
    /// the box around it (its bright rim and body, ignoring a stray dark pixel), the floor's is the
    /// median of the ring around that box (which rejects the group's own shadow and the odd bright
    /// prop).
    /// </para>
    /// <para>
    /// Everything here is a pure function over a pixel buffer, so the arithmetic is an EditMode test
    /// and the capture tool is the only thing that needs a GPU. Percentiles come out of a histogram:
    /// no sort, no per-sample allocation, and the same code can run inside a frame if a later stage
    /// wants a live readout.
    /// </para>
    /// </remarks>
    public static class ArenaContrast
    {
        /// <summary>Histogram resolution: 1024 buckets over luminance 0 … 1.</summary>
        private const int Buckets = 1024;

        /// <summary>
        /// WCAG relative luminance of one displayed pixel: the sRGB channels are linearised first, so
        /// a mid-grey and a saturated green are compared by how bright they actually look.
        /// </summary>
        public static float RelativeLuminance(Color32 pixel)
        {
            float r = Linearise(pixel.r / 255f);
            float g = Linearise(pixel.g / 255f);
            float b = Linearise(pixel.b / 255f);
            return (0.2126f * r) + (0.7152f * g) + (0.0722f * b);
        }

        /// <summary>The WCAG contrast ratio between two relative luminances.</summary>
        public static float Ratio(float first, float second)
        {
            float bright = Mathf.Max(first, second);
            float dark = Mathf.Min(first, second);
            return (bright + 0.05f) / (dark + 0.05f);
        }

        /// <summary>
        /// The <paramref name="percentile"/> (0 … 1) of the luminances inside
        /// <paramref name="box"/>, optionally ignoring a rectangle (the feature the floor ring
        /// surrounds).
        /// </summary>
        public static float Percentile(
            Color32[] pixels, int width, int height, RectInt box, float percentile, RectInt exclude)
        {
            if (pixels is null || width <= 0 || height <= 0)
            {
                return 0f;
            }

            var histogram = new int[Buckets];
            int total = 0;

            int left = Mathf.Clamp(box.xMin, 0, width);
            int right = Mathf.Clamp(box.xMax, 0, width);
            int bottom = Mathf.Clamp(box.yMin, 0, height);
            int top = Mathf.Clamp(box.yMax, 0, height);

            for (int y = bottom; y < top; y++)
            {
                int row = y * width;
                for (int x = left; x < right; x++)
                {
                    if (exclude.width > 0 && exclude.height > 0 && exclude.Contains(new Vector2Int(x, y)))
                    {
                        continue;
                    }

                    float luminance = RelativeLuminance(pixels[row + x]);
                    int bucket = Mathf.Clamp(Mathf.RoundToInt(luminance * (Buckets - 1)), 0, Buckets - 1);
                    histogram[bucket]++;
                    total++;
                }
            }

            if (total == 0)
            {
                return 0f;
            }

            int target = Mathf.Clamp(Mathf.RoundToInt(percentile * (total - 1)), 0, total - 1);
            int seen = 0;
            for (int bucket = 0; bucket < Buckets; bucket++)
            {
                seen += histogram[bucket];
                if (seen > target)
                {
                    return bucket / (float)(Buckets - 1);
                }
            }

            return 1f;
        }

        /// <summary>How many pixels <see cref="Percentile"/> would have looked at.</summary>
        public static int Count(int width, int height, RectInt box, RectInt exclude)
        {
            int left = Mathf.Clamp(box.xMin, 0, width);
            int right = Mathf.Clamp(box.xMax, 0, width);
            int bottom = Mathf.Clamp(box.yMin, 0, height);
            int top = Mathf.Clamp(box.yMax, 0, height);

            int total = 0;
            for (int y = bottom; y < top; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (exclude.width > 0 && exclude.height > 0 && exclude.Contains(new Vector2Int(x, y)))
                    {
                        continue;
                    }

                    total++;
                }
            }

            return total;
        }

        /// <summary>
        /// Measures one group against the floor around it: the bright end of
        /// <paramref name="feature"/> against the median of <paramref name="floor"/> with the feature
        /// itself excluded.
        /// </summary>
        public static ContrastSample Measure(
            Color32[] pixels, int width, int height, RectInt feature, RectInt floor, float featurePercentile = 0.90f)
        {
            float bright = Percentile(pixels, width, height, feature, featurePercentile, default);
            float ground = Percentile(pixels, width, height, floor, 0.5f, feature);

            return new ContrastSample(
                bright,
                ground,
                Count(width, height, feature, default),
                Count(width, height, floor, feature));
        }

        /// <summary>
        /// The ring of floor a group's contrast is measured against: a box around the group, large
        /// enough to hold only floor where the arena is open, with the group's own pixels removed by
        /// <see cref="Measure"/>.
        /// </summary>
        public static RectInt FloorAround(RectInt feature, int margin)
        {
            return new RectInt(
                feature.xMin - margin,
                feature.yMin - margin,
                feature.width + (margin * 2),
                feature.height + (margin * 2));
        }

        private static float Linearise(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
