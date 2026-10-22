using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Web
{
    /// <summary>
    /// Measures every label a scene has drawn and publishes the result, so a browser test can
    /// prove Thai text is drawn with the shipped font and fits the rectangle it was authored into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 13's text question cannot be answered from the Editor: a WebGL player draws with the
    /// IL2CPP build of the font asset, at the canvas scale the browser actually gets, and the
    /// glyph table is the one that survived stripping. This report is the player's own answer —
    /// per label, the text it is showing, the rectangle it must fit, the size it really needs, and
    /// the characters its font cannot draw — and Playwright asserts on that document.
    /// </para>
    /// <para>
    /// <b>What counts as overflow.</b> TMP reports vertical overflow (<c>isTextOverflowing</c>)
    /// when wrapping pushed the text past the container's height; that misses the case Thai
    /// exposes, an unbroken run of characters wider than the label, so the rendered
    /// <c>textBounds</c> are compared against the rectangle too. A tolerance absorbs the
    /// sub-pixel rounding of glyph advance and descenders.
    /// </para>
    /// <para>
    /// <b>Cheap enough to call from a publish point.</b> The scan is skipped when the scene and
    /// canvas size are the same as the last publish and less than half a second has passed, so a
    /// caller on a per-second report path costs one pass per second at most. It is not on a frame
    /// path, and it allocates strings by design: this is diagnostics, not gameplay.
    /// </para>
    /// </remarks>
    public static class UiTextFitReport
    {
        /// <summary>Page global carrying the report.</summary>
        public const string Global = "__projectSurviveTextFit";

        /// <summary>Sub-pixel slack allowed between the needed and the authored size.</summary>
        private const float Tolerance = 2f;

        /// <summary>At most one publish this often when nothing about the screen changed.</summary>
        private const float MinimumIntervalSeconds = 0.5f;

        private static float _lastPublishTime = float.NegativeInfinity;
        private static string _lastScene = string.Empty;
        private static int _lastWidth;
        private static int _lastHeight;

        /// <summary>The most recent report, in-process. The tests read this.</summary>
        public static string LastJson { get; private set; } = string.Empty;

        /// <summary>How many reports have been published.</summary>
        public static int PublishCount { get; private set; }

        /// <summary>Labels measured by the most recent publish.</summary>
        public static int LastLabelCount { get; private set; }

        /// <summary>Labels in the most recent publish that did not fit, for a readable failure.</summary>
        public static int LastOverflowCount { get; private set; }

        /// <summary>Labels in the most recent publish with at least one missing glyph.</summary>
        public static int LastMissingGlyphCount { get; private set; }

        /// <summary>Clears the mirror, so a test starts from a known empty state.</summary>
        public static void Reset()
        {
            LastJson = string.Empty;
            PublishCount = 0;
            LastLabelCount = 0;
            LastOverflowCount = 0;
            LastMissingGlyphCount = 0;
            _lastScene = string.Empty;
            _lastWidth = 0;
            _lastHeight = 0;
            _lastPublishTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Measures every label under the scene's canvases and publishes the document. Repeated
        /// calls for the same scene and size are spaced by <see cref="MinimumIntervalSeconds"/>.
        /// </summary>
        /// <param name="sceneName">The scene on screen, written into the document.</param>
        /// <returns>The JSON it published, or the previous document when this call was skipped.</returns>
        public static string Publish(string sceneName)
        {
            bool sameScreen = SceneName(sceneName) == _lastScene
                && Screen.width == _lastWidth
                && Screen.height == _lastHeight;
            if (sameScreen && Time.unscaledTime - _lastPublishTime < MinimumIntervalSeconds)
            {
                return LastJson;
            }

            var writer = new JsonReportWriter();
            writer.BeginObject();
            writer.Write("scene", SceneName(sceneName));
            writer.Write("canvasWidth", Screen.width);
            writer.Write("canvasHeight", Screen.height);

            int labels = 0;
            int overflowing = 0;
            int missingGlyphs = 0;

            writer.BeginArrayProperty("labels");

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(canvases, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

            var seen = new HashSet<TMP_Text>();
            for (int c = 0; c < canvases.Length; c++)
            {
                TMP_Text[] texts = canvases[c].GetComponentsInChildren<TMP_Text>(includeInactive: true);
                for (int t = 0; t < texts.Length; t++)
                {
                    TMP_Text label = texts[t];
                    if (label == null || !seen.Add(label))
                    {
                        continue;
                    }

                    string text = label.text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    label.ForceMeshUpdate();
                    Rect rect = label.rectTransform.rect;
                    Vector2 needed = label.GetPreferredValues(text);
                    Bounds bounds = label.textBounds;

                    bool overflows = label.isTextOverflowing
                        || bounds.size.x > rect.width + Tolerance
                        || bounds.size.y > rect.height + Tolerance;

                    string missing = MissingCharacters(label, text);

                    labels++;
                    if (overflows)
                    {
                        overflowing++;
                    }

                    if (missing.Length > 0)
                    {
                        missingGlyphs++;
                    }

                    writer.BeginObject();
                    writer.Write("name", PathOf(label.transform) + " (" + label.fontSize + " px)");
                    writer.Write("text", text);
                    writer.Write("rectWidth", rect.width);
                    writer.Write("rectHeight", rect.height);
                    writer.Write("neededWidth", needed.x);
                    writer.Write("neededHeight", needed.y);
                    writer.Write("renderedWidth", bounds.size.x);
                    writer.Write("renderedHeight", bounds.size.y);
                    writer.Write("overflows", overflows);
                    writer.Write("missingGlyphs", missing);
                    writer.EndObject();
                }
            }

            writer.EndArray();
            writer.Write("labelCount", labels);
            writer.Write("overflowCount", overflowing);
            writer.Write("missingGlyphCount", missingGlyphs);
            writer.EndObject();

            LastJson = writer.ToString();
            LastLabelCount = labels;
            LastOverflowCount = overflowing;
            LastMissingGlyphCount = missingGlyphs;
            PublishCount++;
            _lastScene = SceneName(sceneName);
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastPublishTime = Time.unscaledTime;

            WebGlBridge.Publish(Global, LastJson);
            return LastJson;
        }

        private static string SceneName(string sceneName) =>
            string.IsNullOrEmpty(sceneName) ? "<none>" : sceneName;

        /// <summary>
        /// The distinct characters of <paramref name="text"/> the label's font cannot draw, in
        /// first-appearance order. Uses the same <c>HasCharacter</c> probe the Thai text spike
        /// adopted (see <c>llmwiki/gotchas/tmp-tryaddcharacters-lies.md</c>).
        /// </summary>
        private static string MissingCharacters(TMP_Text label, string text)
        {
            TMP_FontAsset font = label.font != null ? label.font : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                return string.Empty;
            }

            var missing = new System.Text.StringBuilder();
            var seen = new HashSet<char>();
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character == ' ' || character == '\n' || character == '\r' || !seen.Add(character))
                {
                    continue;
                }

                if (!font.HasCharacter(character))
                {
                    missing.Append(character);
                }
            }

            return missing.ToString();
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>(4);
            for (Transform current = transform; current != null; current = current.parent)
            {
                parts.Add(current.name);
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
