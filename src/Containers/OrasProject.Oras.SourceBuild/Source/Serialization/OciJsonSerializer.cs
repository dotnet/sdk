// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Serialization/OciJsonSerializer.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;

namespace OrasProject.Oras.Serialization;

/// <summary>
/// OciJsonSerializer provides JSON serialization for OCI content
/// with Go-compatible encoding (literal '+' instead of '\u002B').
/// </summary>
internal static class OciJsonSerializer
{
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    /// <summary>
    /// Serializes the value to a UTF-8 JSON byte array with
    /// Go-compatible string escaping. Throws if the result exceeds
    /// <see cref="OciLimits.MaxManifestBytes"/>.
    /// </summary>
    /// <remarks>
    /// The full byte array is materialized before the size check.
    /// This is acceptable because OCI manifests are bounded in
    /// practice, but callers constructing adversarially large
    /// objects will allocate before the guard fires.
    /// </remarks>
    internal static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, s_options);
        if (bytes.Length > OciLimits.MaxManifestBytes)
        {
            throw new SizeLimitExceededException(
                $"Serialized size {bytes.Length} bytes exceeds"
                + $" limit of {OciLimits.MaxManifestBytes} bytes.");
        }
        return bytes;
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON byte array to the specified type.
    /// </summary>
    internal static T? Deserialize<T>(byte[] utf8Json)
    {
        return JsonSerializer.Deserialize<T>(utf8Json, s_options);
    }

    /// <summary>
    /// Deserializes a JSON string to the specified type.
    /// </summary>
    internal static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, s_options);
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON stream to the specified type.
    /// </summary>
    internal static async Task<T?> DeserializeAsync<T>(
        Stream utf8Json,
        CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync<T>(
            utf8Json, s_options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Formats a JsonElement as a JSON string for error display.
    /// </summary>
    internal static string FormatErrorDetail(JsonElement detail)
    {
        return JsonSerializer.Serialize(detail, s_options);
    }

    /// <summary>
    /// Escapes a JSON string value.
    /// Escapes: ", \, control chars, &lt;, &gt;, &amp;, U+2028, U+2029.
    /// Does NOT escape '+', matching Go's encoding/json.Marshal for
    /// cross-runtime content-addressable storage compatibility.
    /// </summary>
    internal static string EscapeJsonString(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                // JSON structural characters
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                // Named control character escapes
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                // HTML-sensitive characters (Go escapes these)
                case '<':
                    sb.Append("\\u003c");
                    break;
                case '>':
                    sb.Append("\\u003e");
                    break;
                case '&':
                    sb.Append("\\u0026");
                    break;
                // Unicode line/paragraph separators (Go escapes these)
                case '\u2028':
                    sb.Append("\\u2028");
                    break;
                case '\u2029':
                    sb.Append("\\u2029");
                    break;
                default:
                    // Escape remaining control chars (U+0000–U+001F)
                    // as \uXXXX; pass all other characters through
                    // literally, including '+'.
                    if (ch <= '\u001F')
                    {
                        sb.Append($"\\u{(int)ch:x4}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new OciStringConverter());
        options.Converters.Add(new OciDictionaryConverter());
        return options;
    }
}
