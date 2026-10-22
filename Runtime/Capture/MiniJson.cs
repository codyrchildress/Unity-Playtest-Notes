using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlaytestNotes
{
    /// <summary>
    /// Small JSON reader and indented writer for note.json. JsonUtility can't write dictionaries, and the core
    /// runtime takes no third-party dependencies. Objects read as Dictionary&lt;string, object&gt;, arrays as
    /// List&lt;object&gt;, numbers as double.
    /// </summary>
    internal static class MiniJson
    {
        public static string Write(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value, 0);
            builder.Append('\n');
            return builder.ToString();
        }

        public static object Read(string json)
        {
            var reader = new Reader(json);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw reader.Error("Unexpected text after the JSON value");
            }

            return value;
        }

        private static void WriteValue(StringBuilder builder, object value, int depth)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    WriteString(builder, text);
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                case IDictionary<string, object> map:
                    WriteObject(builder, map, depth);
                    break;
                case IDictionary<string, string> strings:
                    var boxed = new SortedDictionary<string, object>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, string> pair in strings)
                    {
                        boxed[pair.Key] = pair.Value;
                    }

                    WriteObject(builder, boxed, depth);
                    break;
                case IList<object> list:
                    WriteArray(builder, list, depth);
                    break;
                case float single:
                    WriteNumber(builder, single);
                    break;
                case double number:
                    WriteNumber(builder, number);
                    break;
                case int integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    break;
                case long integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new ArgumentException("MiniJson can't write a " + value.GetType().Name);
            }
        }

        private static void WriteNumber(StringBuilder builder, double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                builder.Append("null");
                return;
            }

            builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder builder, IDictionary<string, object> map, int depth)
        {
            if (map.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append("{\n");
            int written = 0;
            foreach (KeyValuePair<string, object> pair in map)
            {
                Indent(builder, depth + 1);
                WriteString(builder, pair.Key);
                builder.Append(": ");
                WriteValue(builder, pair.Value, depth + 1);
                written++;
                builder.Append(written < map.Count ? ",\n" : "\n");
            }

            Indent(builder, depth);
            builder.Append('}');
        }

        // Arrays of plain values (vectors, sizes) stay on one line so note.json reads well.
        private static void WriteArray(StringBuilder builder, IList<object> list, int depth)
        {
            bool flat = true;
            foreach (object item in list)
            {
                if (item is IDictionary<string, object> || item is IList<object>)
                {
                    flat = false;
                    break;
                }
            }

            builder.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (flat)
                {
                    builder.Append(i == 0 ? string.Empty : ", ");
                }
                else
                {
                    builder.Append(i == 0 ? "\n" : ",\n");
                    Indent(builder, depth + 1);
                }

                WriteValue(builder, list[i], depth + 1);
            }

            if (!flat && list.Count > 0)
            {
                builder.Append('\n');
                Indent(builder, depth);
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static void Indent(StringBuilder builder, int depth)
        {
            builder.Append(' ', depth * 2);
        }

        private sealed class Reader
        {
            private readonly string json;
            private int index;

            public Reader(string json)
            {
                this.json = json ?? throw new ArgumentNullException(nameof(json));
            }

            public bool AtEnd => index >= json.Length;

            public FormatException Error(string message)
            {
                return new FormatException(message + " at character " + index + ".");
            }

            public void SkipWhitespace()
            {
                while (!AtEnd && char.IsWhiteSpace(json[index]))
                {
                    index++;
                }
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd)
                {
                    throw Error("Unexpected end of JSON");
                }

                char c = json[index];
                switch (c)
                {
                    case '{':
                        return ReadObject();
                    case '[':
                        return ReadArray();
                    case '"':
                        return ReadString();
                    case 't':
                        Expect("true");
                        return true;
                    case 'f':
                        Expect("false");
                        return false;
                    case 'n':
                        Expect("null");
                        return null;
                    default:
                        if (c == '-' || char.IsDigit(c))
                        {
                            return ReadNumber();
                        }

                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                index++;
                SkipWhitespace();
                if (!AtEnd && json[index] == '}')
                {
                    index++;
                    return map;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || json[index] != '"')
                    {
                        throw Error("Expected a property name");
                    }

                    string key = ReadString();
                    SkipWhitespace();
                    ExpectChar(':');
                    map[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd)
                    {
                        throw Error("Unterminated object");
                    }

                    if (json[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    ExpectChar('}');
                    return map;
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                index++;
                SkipWhitespace();
                if (!AtEnd && json[index] == ']')
                {
                    index++;
                    return list;
                }

                while (true)
                {
                    list.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd)
                    {
                        throw Error("Unterminated array");
                    }

                    if (json[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    ExpectChar(']');
                    return list;
                }
            }

            private string ReadString()
            {
                var builder = new StringBuilder();
                index++;
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("Unterminated string");
                    }

                    char c = json[index++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("Unterminated escape");
                    }

                    char escape = json[index++];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escape);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (index + 4 > json.Length)
                            {
                                throw Error("Short unicode escape");
                            }

                            builder.Append((char)int.Parse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            index += 4;
                            break;
                        default:
                            throw Error("Unknown escape '\\" + escape + "'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = index;
                while (!AtEnd && "+-0123456789.eE".IndexOf(json[index]) >= 0)
                {
                    index++;
                }

                string text = json.Substring(start, index - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    throw Error("Bad number '" + text + "'");
                }

                return number;
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(json, index, word, 0, word.Length) != 0)
                {
                    throw Error("Expected '" + word + "'");
                }

                index += word.Length;
            }

            private void ExpectChar(char c)
            {
                if (AtEnd || json[index] != c)
                {
                    throw Error("Expected '" + c + "'");
                }

                index++;
            }
        }
    }
}
