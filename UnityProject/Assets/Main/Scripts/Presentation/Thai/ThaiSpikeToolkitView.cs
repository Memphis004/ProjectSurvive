using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace ProjectSurvive.Game.Presentation.Thai
{
    /// <summary>
    /// The UI Toolkit side of the Thai text spike.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UI Toolkit renders through TextCore, so coverage and metrics come from a
    /// <see cref="FontAsset"/> rather than a TMP atlas. The comparison against
    /// <see cref="ThaiSpikeTmpView"/> is therefore "same font file, two engines",
    /// which is what makes the ADR's numbers comparable.
    /// </para>
    /// <para>
    /// Coverage uses <c>HasCharacter</c> per character — deliberately the same
    /// primitive as the TMP side, and deliberately not <c>TryAddCharacters</c>,
    /// whose return value is unreliable on an already-populated asset
    /// (<c>llmwiki/gotchas/tmp-tryaddcharacters-lies.md</c>).
    /// </para>
    /// <para>
    /// The panel is built in code instead of from UXML because a
    /// <see cref="VisualElement"/> is not a <c>UnityEngine.Object</c> and cannot be
    /// wired in a scene; building it here keeps the spike self-contained and avoids
    /// committing a UXML asset that would exist only for a throwaway experiment
    /// (AGENTS rule 12).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ThaiSpikeToolkitView : MonoBehaviour
    {
        /// <summary>Label used in the report rows.</summary>
        public const string StackName = "UI Toolkit";

        [SerializeField] private UIDocument _document;

        [Tooltip("The TextCore font asset generated from the Thai font (Editor-created).")]
        [SerializeField] private FontAsset _fontAsset;

        [SerializeField] private int _fontSize = 32;

        private Label _label;

        /// <summary>
        /// Measures every string and appends the rows to <paramref name="report"/>.
        /// </summary>
        public void AppendRows(string[] texts, ThaiSpikeReport report)
        {
            string fontName = _fontAsset is null ? "<none>" : _fontAsset.name;

            if (!EnsureLabel())
            {
                report.Add(new ThaiSpikeRow(StackName, fontName, "<no UIDocument assigned>", 0, string.Empty, 0f, 0f));
                return;
            }

            _label.style.fontSize = _fontSize;
            if (_fontAsset is not null)
            {
                _label.style.unityFontDefinition = new StyleFontDefinition(_fontAsset);
            }

            for (int i = 0; i < texts.Length; i++)
            {
                string text = texts[i];
                string missing = CountMissingCharacters(text);

                _label.text = text;
                Vector2 size = _label.MeasureTextSize(
                    text,
                    0f,
                    VisualElement.MeasureMode.Undefined,
                    0f,
                    VisualElement.MeasureMode.Undefined);

                report.Add(new ThaiSpikeRow(StackName, fontName, text, missing.Length, missing, size.x, size.y));
            }
        }

        /// <summary>
        /// The distinct characters of <paramref name="text"/> this font asset cannot
        /// draw, in first-appearance order.
        /// </summary>
        private string CountMissingCharacters(string text)
        {
            if (_fontAsset is null)
            {
                return string.Empty;
            }

            var missing = new StringBuilder();
            var checkedCharacters = new HashSet<char>();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || !checkedCharacters.Add(c))
                {
                    continue;
                }

                if (!_fontAsset.HasCharacter(c))
                {
                    missing.Append(c);
                }
            }

            return missing.ToString();
        }

        private bool EnsureLabel()
        {
            if (_label is not null)
            {
                return true;
            }

            if (_document is null)
            {
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            if (root is null)
            {
                return false;
            }

            _label = new Label();
            root.Add(_label);
            return true;
        }
    }
}
