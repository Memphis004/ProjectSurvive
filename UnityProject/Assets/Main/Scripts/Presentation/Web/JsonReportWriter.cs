using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjectSurvive.Game.Presentation.Web
{
    /// <summary>
    /// Minimal JSON writer for the browser-facing smoke reports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The project already has a serializer — MessagePack — but its job is save
    /// data, not producing a text document for JavaScript, and
    /// <c>JsonUtility</c> would drag <c>UnityEngine</c> into a class the tests
    /// build strings with. A handful of fields do not justify a JSON library
    /// (AGENTS rule 12).
    /// </para>
    /// <para>
    /// Every non-ASCII code point is escaped as <c>\uXXXX</c>. That is not
    /// cosmetic: the report crosses into JavaScript through a
    /// <c>[MarshalAs(UnmanagedType.LPStr)]</c> boundary, and Thai text would be
    /// mangled if the raw UTF-16 span were handed to an ANSI marshaller. Escaped
    /// output is pure ASCII, so it survives any marshalling the platform picks.
    /// </para>
    /// <para>
    /// This is a diagnostics path, not a hot path, so it uses a
    /// <see cref="StringBuilder"/> freely.
    /// </para>
    /// </remarks>
    public sealed class JsonReportWriter
    {
        private readonly StringBuilder _sb = new StringBuilder(512);

        /// <summary>
        /// "Has at least one item" for each open container, so a nested object
        /// knows where it is: the flag is saved when a container opens and restored
        /// when it closes.
        /// </summary>
        /// <remarks>
        /// Without this the writer emitted <c>[...}{...]</c> — every element but the
        /// first lost its comma, and the page's <c>JSON.parse</c> rejected the whole
        /// report. Single-element arrays hid it, which is exactly why the browser
        /// test asserts on parsed values rather than on the string.
        /// </remarks>
        private readonly Stack<bool> _enclosingHasItems = new Stack<bool>();
        private bool _hasItems;

        /// <summary>Starts an object.</summary>
        public JsonReportWriter BeginObject()
        {
            BeginValue();
            _sb.Append('{');
            _enclosingHasItems.Push(_hasItems);
            _hasItems = false;
            return this;
        }

        /// <summary>Ends the current object.</summary>
        public JsonReportWriter EndObject()
        {
            _sb.Append('}');
            _hasItems = _enclosingHasItems.Count > 0 && _enclosingHasItems.Pop();
            return this;
        }

        /// <summary>Starts an array-valued property, e.g. <c>"probes"</c>.</summary>
        public JsonReportWriter BeginArrayProperty(string name)
        {
            WriteName(name);
            _sb.Append('[');
            _enclosingHasItems.Push(_hasItems);
            _hasItems = false;
            return this;
        }

        /// <summary>
        /// Starts an object-valued property, e.g. <c>"orthographic"</c>. Its members are written
        /// with the same calls as the enclosing object's; <see cref="EndObject"/> closes it.
        /// </summary>
        public JsonReportWriter BeginObjectProperty(string name)
        {
            WriteName(name);
            _sb.Append('{');
            _enclosingHasItems.Push(_hasItems);
            _hasItems = false;
            return this;
        }

        /// <summary>Ends the array opened by <see cref="BeginArrayProperty"/>.</summary>
        public JsonReportWriter EndArray()
        {
            _sb.Append(']');
            _hasItems = _enclosingHasItems.Count > 0 && _enclosingHasItems.Pop();
            return this;
        }

        public JsonReportWriter Write(string name, string value)
        {
            WriteName(name);
            AppendString(value);
            return this;
        }

        public JsonReportWriter Write(string name, bool value)
        {
            WriteName(name);
            _sb.Append(value ? "true" : "false");
            return this;
        }

        public JsonReportWriter Write(string name, int value)
        {
            WriteName(name);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonReportWriter Write(string name, float value)
        {
            WriteName(name);
            _sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            return this;
        }

        /// <summary>Adds a bare string element to the current array.</summary>
        public JsonReportWriter WriteArrayString(string value)
        {
            BeginValue();
            AppendString(value);
            return this;
        }

        public override string ToString() => _sb.ToString();

        private void WriteName(string name)
        {
            BeginValue();
            AppendString(name);
            _sb.Append(':');
        }

        /// <summary>
        /// Emits the separator a value needs in its container and marks the container
        /// as non-empty. Every value (a scalar property, a nested object, an array)
        /// starts through here, which is what keeps the commas correct at any depth.
        /// </summary>
        private void BeginValue()
        {
            if (_hasItems)
            {
                _sb.Append(',');
            }

            _hasItems = true;
        }

        private void AppendString(string value)
        {
            if (value is null)
            {
                _sb.Append("null");
                return;
            }

            _sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        _sb.Append("\\\"");
                        break;
                    case '\\':
                        _sb.Append("\\\\");
                        break;
                    case '\n':
                        _sb.Append("\\n");
                        break;
                    case '\r':
                        _sb.Append("\\r");
                        break;
                    case '\t':
                        _sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ' || c > '~')
                        {
                            _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _sb.Append(c);
                        }

                        break;
                }
            }

            _sb.Append('"');
        }
    }
}
