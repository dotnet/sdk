// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal sealed class EndOfLineFormatter : DocumentFormatter
    {
        protected override string FormatWarningDescription => Resources.Fix_end_of_line_marker;

        public override string Name => "ENDOFLINE";
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
                if (!TryGetEndOfLine(analyzerConfigOptions, out var endOfLine))
                {
                    return sourceText;
                }

                // Fast path: when the source already uses the configured line ending for every
                // line-ending span, avoid building the line map and per-line allocations entirely.
                if (!NeedsEndOfLineFixes(sourceText, endOfLine))
                {
                    return sourceText;
                }

                var newSourceText = sourceText;
                var changes = new List<TextChange>();
                for (var lineIndex = 0; lineIndex < newSourceText.Lines.Count; lineIndex++)
                {
                    var line = newSourceText.Lines[lineIndex];
                    var lineEndingSpan = new TextSpan(line.End, line.EndIncludingLineBreak - line.End);

                    // Check for end of file
                    if (lineEndingSpan.IsEmpty)
                    {
                        break;
                    }

                    var lineEnding = newSourceText.ToString(lineEndingSpan);

                    if (lineEnding == endOfLine)
                    {
                        continue;
                    }

                    changes.Add(new TextChange(lineEndingSpan, endOfLine));
                }

                newSourceText = newSourceText.WithChanges(changes);

                return newSourceText;
            });
        }

        private static bool NeedsEndOfLineFixes(SourceText sourceText, string endOfLine)
        {
            switch (endOfLine)
            {
                case "\n":
                    // A 'cr' character can only appear as part of a cr or crlf line ending, so
                    // its absence means every line ending is already a plain 'lf'.
                    return sourceText.ToString().IndexOf('\r') >= 0;
                case "\r":
                    return sourceText.ToString().IndexOf('\n') >= 0;
                case "\r\n":
                    return HasLooseEndOfLine(sourceText.ToString());
                default:
                    return true;
            }
        }

        private static bool HasLooseEndOfLine(string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character == '\n' && (index == 0 || text[index - 1] != '\r'))
                {
                    return true;
                }

                if (character == '\r' && (index == text.Length - 1 || text[index + 1] != '\n'))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetEndOfLine(AnalyzerConfigOptions analyzerConfigOptions, [NotNullWhen(true)] out string? endOfLine)
        {
            if (analyzerConfigOptions != null &&
                analyzerConfigOptions.TryGetValue("end_of_line", out var endOfLineOption))
            {
                endOfLine = GetEndOfLine(endOfLineOption);
                return true;
            }

            endOfLine = null;
            return false;
        }

        private static string GetEndOfLine(string endOfLineOption)
        {
            return endOfLineOption switch
            {
                "lf" => "\n",
                "cr" => "\r",
                "crlf" => "\r\n",
                _ => Environment.NewLine,
            };
        }

        internal static string GetEndOfLineOption(string newLine)
        {
            return newLine switch
            {
                "\n" => "lf",
                "\r" => "cr",
                "\r\n" => "crlf",
                _ => GetEndOfLineOption(Environment.NewLine),
            };
        }
    }
}
