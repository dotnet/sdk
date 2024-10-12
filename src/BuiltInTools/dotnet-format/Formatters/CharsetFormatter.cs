// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal sealed class CharsetFormatter : DocumentFormatter
    {
        protected override string FormatWarningDescription => Resources.Fix_file_encoding;

        private static Encoding Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static Encoding Latin1 => Encoding.GetEncoding("iso-8859-1");

        public override string Name => "CHARSET";
        public override FixCategory Category => FixCategory.Whitespace;

        internal override Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                if (!TryGetCharset(analyzerConfigOptions, out var encoding)
                    || sourceText.Encoding?.Equals(encoding) == true
                    || IsEncodingEquivalent(sourceText, encoding))
                {
                    return sourceText;
                }

                return SourceText.From(sourceText.ToString(), encoding, sourceText.ChecksumAlgorithm);
            });
        }

        private static bool IsEncodingEquivalent(SourceText sourceText, Encoding encoding)
        {
            if (sourceText.Encoding is null)
            {
                throw new System.Exception($"source text did not have an identifiable encoding");
            }

            var text = sourceText.ToString();
            var originalBytes = GetEncodedBytes(text, sourceText.Encoding);
            var encodedBytes = GetEncodedBytes(text, encoding);

            return originalBytes.Length == encodedBytes.Length
                && originalBytes.SequenceEqual(encodedBytes);
        }

        private static byte[] GetEncodedBytes(string text, Encoding encoding)
        {
            // Start with a large initial capacity, double the character count with additional space for the BOM
            using var stream = new MemoryStream(text.Length * 2 + 3);
            using var streamWriter = new StreamWriter(stream, encoding);
            streamWriter.Write(text);
            streamWriter.Flush();
            return stream.ToArray();
        }

        private static bool TryGetCharset(AnalyzerConfigOptions analyzerConfigOptions, [NotNullWhen(true)] out Encoding? encoding)
        {
            if (analyzerConfigOptions != null &&
                analyzerConfigOptions.TryGetValue("charset", out var charsetOption))
            {
                encoding = GetCharset(charsetOption);
                return true;
            }

            encoding = null;
            return false;
        }

        public static Encoding GetCharset(string charsetOption)
        {
            return charsetOption switch
            {
                "latin1" => Latin1,
                "utf-8-bom" => Encoding.UTF8,// UTF-8 with BOM Marker
                "utf-16be" => Encoding.BigEndianUnicode,// Big Endian with BOM Marker
                "utf-16le" => Encoding.Unicode,// Little Endian with BOM Marker
                _ => Utf8,
            };
        }
    }
}
