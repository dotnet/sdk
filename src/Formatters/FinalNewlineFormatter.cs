// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal sealed class FinalNewlineFormatter : DocumentFormatter
    {
        protected override async Task<SourceText> FormatFileAsync(
            Document document,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!codingConventions.TryGetConventionValue("insert_final_newline", out bool insertFinalNewline))
            {
                return await document.GetTextAsync(cancellationToken);
            }

            var endOfLine = codingConventions.TryGetConventionValue("end_of_line", out string endOfLineOption)
                ? GetEndOfLine(endOfLineOption)
                : Environment.NewLine;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var lastLine = sourceText.Lines.Last();

            var hasFinalNewline = lastLine.Span.IsEmpty;

            if (insertFinalNewline && !hasFinalNewline)
            {
                var finalNewlineSpan = new TextSpan(lastLine.End, 0);
                var addNewlineChange = new TextChange(finalNewlineSpan, endOfLine);
                sourceText = sourceText.WithChanges(addNewlineChange);
            }
            else if (!insertFinalNewline && hasFinalNewline)
            {
                // In the case of empty files where there is a single empty line, there is nothing to remove.
                while (sourceText.Lines.Count > 1 && hasFinalNewline)
                {
                    var lineBeforeLast = sourceText.Lines[sourceText.Lines.Count - 2];
                    var finalNewlineSpan = new TextSpan(lineBeforeLast.End, lineBeforeLast.EndIncludingLineBreak - lineBeforeLast.End);
                    var removeNewlineChange = new TextChange(finalNewlineSpan, string.Empty);
                    sourceText = sourceText.WithChanges(removeNewlineChange);

                    lastLine = sourceText.Lines.Last();
                    hasFinalNewline = lastLine.Span.IsEmpty;
                }
            }

            return sourceText;
        }

        private string GetEndOfLine(string endOfLineOption)
        {
            switch (endOfLineOption)
            {
                case "lf":
                    return "\n";
                case "cr":
                    return "\r";
                case "crlf":
                    return "\r\n";
                default:
                    return Environment.NewLine;
            }
        }
    }
}
