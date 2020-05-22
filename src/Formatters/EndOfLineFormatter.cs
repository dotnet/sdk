// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal sealed class EndOfLineFormatter : DocumentFormatter
    {
        protected override string FormatWarningDescription => Resources.Fix_end_of_line_marker;

        protected override Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions? analyzerConfigOptions,
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

                var newSourceText = sourceText;
                for (var lineIndex = 0; lineIndex < newSourceText.Lines.Count; lineIndex++)
                {
                    var line = newSourceText.Lines[lineIndex];
                    var lineEndingSpan = new TextSpan(line.End, line.EndIncludingLineBreak - line.End);

                    // Check for end of file
                    if (lineEndingSpan.Length == 0)
                    {
                        break;
                    }

                    var lineEnding = newSourceText.ToString(lineEndingSpan);

                    if (lineEnding == endOfLine)
                    {
                        continue;
                    }

                    var newLineChange = new TextChange(lineEndingSpan, endOfLine);
                    newSourceText = newSourceText.WithChanges(newLineChange);
                }

                return newSourceText;
            });
        }

        public static bool TryGetEndOfLine(AnalyzerConfigOptions? analyzerConfigOptions, [NotNullWhen(true)] out string? endOfLine)
        {
            if (analyzerConfigOptions is object &&
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
    }
}
