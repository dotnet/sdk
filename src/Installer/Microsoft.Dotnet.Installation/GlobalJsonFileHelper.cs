// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

/// <summary>
/// Helper for reading and writing global.json files while preserving their original encoding
/// (including UTF-16 LE/BE with BOM).
/// </summary>
public static class GlobalJsonFileHelper
{
    /// <summary>
    /// Reads a file's text content and detects the encoding from a byte-order mark (BOM).
    /// Returns the content as a string and the detected <see cref="Encoding"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <returns>A tuple of (content, encoding).</returns>
    public static (string Content, Encoding Encoding) ReadFileWithEncodingDetection(string filePath)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        reader.Peek(); // trigger BOM detection without consuming content
        var content = reader.ReadToEnd();
        return (content, reader.CurrentEncoding);
    }

    /// <summary>
    /// Opens a file stream suitable for <see cref="System.Text.Json.JsonSerializer.Deserialize{T}(Stream, System.Text.Json.JsonSerializerOptions?)"/>.
    /// If the file uses a non-UTF-8 encoding (e.g. UTF-16 with BOM), the content is transcoded to a
    /// UTF-8 <see cref="MemoryStream"/>. Otherwise the original file stream is returned directly.
    /// The caller must dispose the returned stream.
    /// </summary>
    /// <param name="filePath">Path to the file to open.</param>
    /// <returns>A stream containing UTF-8 encoded content suitable for System.Text.Json deserialization.</returns>
    public static Stream OpenAsUtf8Stream(string filePath)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        reader.Peek(); // trigger BOM detection

        if (reader.CurrentEncoding is UTF8Encoding)
        {
            // UTF-8 (with or without BOM): rewind and return the underlying stream directly.
            // JsonSerializer handles the UTF-8 BOM itself.
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            // Detach the stream from the reader so Dispose doesn't close it.
            var stream = reader.BaseStream;
            // We can't detach from StreamReader, so re-open the file instead.
            reader.Close();
            return File.OpenRead(filePath);
        }

        // For other encodings (e.g. UTF-16 LE/BE), transcode to UTF-8 in memory.
        // global.json files are small so this is acceptable.
        var content = reader.ReadToEnd();
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
