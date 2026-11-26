// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// Applies all whitespace-related formatting in a single pass over each document.
    /// This avoids re-reading and re-parsing every document once per constituent formatter.
    /// </summary>
    /// <remarks>
    /// The individual <see cref="WhitespaceFormatter"/>, <see cref="FinalNewlineFormatter"/>,
    /// <see cref="EndOfLineFormatter"/> and <see cref="CharsetFormatter"/> classes are kept
    /// intact as they are used directly by unit tests and by <see cref="OrganizeImportsFormatter"/>.
    /// </remarks>
    internal sealed class CompositeWhitespaceFormatter : DocumentFormatter
    {
        private static readonly WhitespaceFormatter s_whitespaceFormatter = new();
        private static readonly FinalNewlineFormatter s_finalNewlineFormatter = new();
        private static readonly EndOfLineFormatter s_endOfLineFormatter = new();
        private static readonly CharsetFormatter s_charsetFormatter = new();

        protected override string FormatWarningDescription => Resources.Fix_whitespace_formatting;

        public override string Name => "WHITESPACE";

        public override FixCategory Category => FixCategory.Whitespace;

        internal override async Task<SourceText> FormatFileAsync(
            TextDocument document,
            SourceText sourceText,
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedSourceText = await s_whitespaceFormatter.FormatFileAsync(document, sourceText, analyzerConfigOptions, formatOptions, logger, cancellationToken);

            formattedSourceText = await s_finalNewlineFormatter.FormatFileAsync(document, formattedSourceText, analyzerConfigOptions, formatOptions, logger, cancellationToken);

            formattedSourceText = await s_endOfLineFormatter.FormatFileAsync(document, formattedSourceText, analyzerConfigOptions, formatOptions, logger, cancellationToken);
            formattedSourceText = await s_charsetFormatter.FormatFileAsync(document, formattedSourceText, analyzerConfigOptions, formatOptions, logger, cancellationToken);

            return formattedSourceText;
        }
    }
}
