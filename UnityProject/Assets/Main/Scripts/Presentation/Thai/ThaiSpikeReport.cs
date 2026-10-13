using System.Collections.Generic;
using System.Globalization;
using ProjectSurvive.Game.Presentation.Web;

namespace ProjectSurvive.Game.Presentation.Thai
{
    /// <summary>
    /// One measured line: how one text stack rendered one Thai string.
    /// </summary>
    /// <remarks>
    /// The ADR behind this spike (<c>docs/adr/0008-thai-text-ui-stack.md</c>) is only
    /// worth anything if it is decided on numbers, so every field here is a
    /// measurement, not an opinion: which code points the font could not supply,
    /// and the size the stack laid the string out at.
    /// </remarks>
    public sealed class ThaiSpikeRow
    {
        public ThaiSpikeRow(
            string stack,
            string font,
            string text,
            int missingGlyphs,
            string missingCharacters,
            float width,
            float height)
        {
            Stack = stack;
            Font = font;
            Text = text;
            MissingGlyphs = missingGlyphs;
            MissingCharacters = missingCharacters;
            Width = width;
            Height = height;
        }

        /// <summary>Text stack under test, e.g. <c>uGUI+TMP</c>.</summary>
        public string Stack { get; }

        /// <summary>Font asset the stack rendered with.</summary>
        public string Font { get; }

        /// <summary>The localized Thai string being measured.</summary>
        public string Text { get; }

        /// <summary>Code points the font could not supply (0 is the requirement).</summary>
        public int MissingGlyphs { get; }

        /// <summary>The missing code points themselves, for a diagnosable failure.</summary>
        public string MissingCharacters { get; }

        /// <summary>Preferred width the stack measured, in pixels.</summary>
        public float Width { get; }

        /// <summary>Preferred height the stack measured, in pixels.</summary>
        public float Height { get; }
    }

    /// <summary>All rows from one run of the spike, plus the two verdicts it feeds.</summary>
    public sealed class ThaiSpikeReport
    {
        private readonly List<ThaiSpikeRow> _rows = new List<ThaiSpikeRow>();

        public ThaiSpikeReport(string platform, string fontName)
        {
            Platform = platform;
            FontName = fontName;
        }

        public string Platform { get; }

        /// <summary>The font file both stacks are asked to render.</summary>
        public string FontName { get; }

        public IReadOnlyList<ThaiSpikeRow> Rows => _rows;

        /// <summary>Sum of missing glyphs across every measured string and stack.</summary>
        public int TotalMissingGlyphs
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _rows.Count; i++)
                {
                    total += _rows[i].MissingGlyphs;
                }

                return total;
            }
        }

        /// <summary>
        /// True when the font supplied every code point in every string on every
        /// stack — the condition the ADR must not accept a substitute for.
        /// </summary>
        public bool CoversEveryGlyph => TotalMissingGlyphs == 0 && _rows.Count > 0;

        public void Add(ThaiSpikeRow row) => _rows.Add(row);

        /// <summary>
        /// True when every pair of stacks measured the same string to the same size
        /// within <see cref="MeasurementTolerance"/>.
        /// </summary>
        /// <remarks>
        /// The tolerance is <b>relative</b> on purpose. TMP and TextCore round glyph
        /// advances differently, so an absolute pixel threshold would call a 0.5 %
        /// difference on a 830 px line a disagreement while accepting the same
        /// absolute difference on a 60 px line. Both stacks are measured at the same
        /// nominal size (see <c>ProjectSetup.SpikeTmpFontSize</c>), so a relative
        /// comparison is the honest one.
        /// </remarks>
        public bool StacksMeasureEqually()
        {
            if (_rows.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                for (int j = i + 1; j < _rows.Count; j++)
                {
                    if (!string.Equals(_rows[i].Text, _rows[j].Text, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!WithinTolerance(_rows[i].Width, _rows[j].Width)
                        || !WithinTolerance(_rows[i].Height, _rows[j].Height))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Relative difference limit between two measurements.</summary>
        public const float MeasurementTolerance = 0.01f;

        private static bool WithinTolerance(float a, float b)
        {
            float largest = System.Math.Max(System.Math.Abs(a), System.Math.Abs(b));
            if (largest <= 0f)
            {
                return true;
            }

            return System.Math.Abs(a - b) / largest <= MeasurementTolerance;
        }

        public string ToJson()
        {
            var json = new JsonReportWriter();
            json.BeginObject()
                .Write("platform", Platform)
                .Write("font", FontName)
                .Write("rowCount", _rows.Count)
                .Write("totalMissingGlyphs", TotalMissingGlyphs)
                .Write("coversEveryGlyph", CoversEveryGlyph)
                .Write("stacksMeasureEqually", StacksMeasureEqually())
                .BeginArrayProperty("rows");

            for (int i = 0; i < _rows.Count; i++)
            {
                ThaiSpikeRow row = _rows[i];
                json.BeginObject()
                    .Write("stack", row.Stack)
                    .Write("font", row.Font)
                    .Write("text", row.Text)
                    .Write("missingGlyphs", row.MissingGlyphs)
                    .Write("missingCharacters", row.MissingCharacters)
                    .Write("width", row.Width)
                    .Write("height", row.Height)
                    .EndObject();
            }

            json.EndArray().EndObject();
            return json.ToString();
        }

        public string ToLog()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("ProjectSurvive Thai text spike (").Append(Platform).Append(") font=").Append(FontName)
              .Append(" missingGlyphs=").Append(TotalMissingGlyphs)
              .Append(" stacksMeasureEqually=").Append(StacksMeasureEqually() ? "yes" : "no");

            for (int i = 0; i < _rows.Count; i++)
            {
                ThaiSpikeRow row = _rows[i];
                sb.Append('\n')
                  .Append("  [").Append(row.Stack).Append("] ")
                  .Append(row.Width.ToString("0.#", CultureInfo.InvariantCulture)).Append('x')
                  .Append(row.Height.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append(" px, missing=").Append(row.MissingGlyphs)
                  .Append(row.MissingCharacters.Length > 0 ? " (" + row.MissingCharacters + ")" : string.Empty)
                  .Append(" - ").Append(row.Text);
            }

            return sb.ToString();
        }
    }
}
