// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
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
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedSourceText = await s_whitespaceFormatter.FormatFileAsync(document, sourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);

            // Only run the final newline pass when the option is configured. When it is absent the
            // formatter returns the *document* text, which still points at the unformatted source
            // in a merged pass, so invoking it would discard the whitespace formatting.
            if (IsFinalNewlineConfigured(analyzerConfigOptions))
            {
                formattedSourceText = await s_finalNewlineFormatter.FormatFileAsync(document, formattedSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);
            }

            formattedSourceText = await s_endOfLineFormatter.FormatFileAsync(document, formattedSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);
            formattedSourceText = await s_charsetFormatter.FormatFileAsync(document, formattedSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);

            return formattedSourceText;
        }

        private static bool IsFinalNewlineConfigured(AnalyzerConfigOptions analyzerConfigOptions)
        {
            return analyzerConfigOptions?.TryGetValue("insert_final_newline", out var insertFinalNewlineValue) == true
                && bool.TryParse(insertFinalNewlineValue, out _);
        }
    }
}