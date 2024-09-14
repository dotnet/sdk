// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal sealed class FinalNewlineFormatter : DocumentFormatter
    {
        protected override string FormatWarningDescription => Resources.Fix_final_newline;

        public override string Name => "FINALNEWLINE";
        public override FixCategory Category => FixCategory.Whitespace;

        internal override async Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!analyzerConfigOptions.TryGetValue("insert_final_newline", out var insertFinalNewlineValue) ||
                !bool.TryParse(insertFinalNewlineValue, out var insertFinalNewline))
            {
                return await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!EndOfLineFormatter.TryGetEndOfLine(analyzerConfigOptions, out var endOfLine))
            {
                endOfLine = Environment.NewLine;
            }

            var lastLine = sourceText.Lines[^1];
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
                    var lineBeforeLast = sourceText.Lines[^2];
                    var finalNewlineSpan = new TextSpan(lineBeforeLast.End, lineBeforeLast.EndIncludingLineBreak - lineBeforeLast.End);
                    var removeNewlineChange = new TextChange(finalNewlineSpan, string.Empty);
                    sourceText = sourceText.WithChanges(removeNewlineChange);

                    lastLine = sourceText.Lines[^1];
                    hasFinalNewline = lastLine.Span.IsEmpty;
                }
            }

            return sourceText;
        }
    }
}
