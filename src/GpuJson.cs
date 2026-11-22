#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2026]  [Young-il Park]
/// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
/// the Free Software Foundation version 3 of the License
/// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
/// You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.
///</remarks>
#endregion

using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GRAL_2001
{
    /// <summary>
    /// Writes the JSON of the GPU path (frame headers, metrics lines, cache entries, the line of GRAL --gpu-check) with
    /// Utf8JsonWriter. JsonSerializer would find the members of the values by reflection, which a trimmed publish switches off
    /// (JsonSerializerIsReflectionEnabledByDefault = false): the GPU path then failed at its first JSON line. The values are
    /// dictionaries with string keys, lists and arrays, strings, booleans, numbers, null and JsonElement, and the text is the
    /// same that JsonSerializer wrote for them, so cache keys and log formats do not change. Any other type is a programming
    /// error and throws.
    /// </summary>
    internal static class GpuJson
    {
        internal static byte[] Bytes(object value, JsonWriterOptions options = default)
        {
            using var buffer = new MemoryStream();
            Write(buffer, value, options);
            return buffer.ToArray();
        }

        internal static string Text(object value, JsonWriterOptions options = default) => Encoding.UTF8.GetString(Bytes(value, options));

        internal static void Write(Stream output, object value, JsonWriterOptions options = default)
        {
            using var writer = new Utf8JsonWriter(output, options);
            Write(writer, value);
        }

        private static void Write(Utf8JsonWriter writer, object value)
        {
            switch (value)
            {
                case null: writer.WriteNullValue(); break;
                case string text: writer.WriteStringValue(text); break;
                case bool flag: writer.WriteBooleanValue(flag); break;
                case int number: writer.WriteNumberValue(number); break;
                case long number: writer.WriteNumberValue(number); break;
                case uint number: writer.WriteNumberValue(number); break;
                case ulong number: writer.WriteNumberValue(number); break;
                case short number: writer.WriteNumberValue(number); break;
                case ushort number: writer.WriteNumberValue(number); break;
                case byte number: writer.WriteNumberValue(number); break;
                case float number: writer.WriteNumberValue(number); break;
                case double number: writer.WriteNumberValue(number); break;
                case JsonElement element: element.WriteTo(writer); break;
                case IDictionary map:
                    writer.WriteStartObject();
                    foreach (DictionaryEntry entry in map)
                    {
                        writer.WritePropertyName((string)entry.Key);
                        Write(writer, entry.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case IEnumerable list:
                    writer.WriteStartArray();
                    foreach (object item in list) Write(writer, item);
                    writer.WriteEndArray();
                    break;
                default: throw new NotSupportedException("GPU JSON: a value of type " + value.GetType() + " cannot be written");
            }
        }
    }
}
