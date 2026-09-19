// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// CodeFormatter that uses the <see cref="Formatter"/> to format document whitespace.
    /// </summary>
    internal sealed class WhitespaceFormatter : DocumentFormatter
    {
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
            if (formatOptions.SaveFormattedFiles)
            {
                return await GetFormattedDocument(document, optionSet, cancellationToken);
            }
            else
            {
                return await GetFormattedDocumentWithDetailedChanges(document, sourceText, optionSet, cancellationToken);
            }
        }

        /// <summary>
        /// Returns a formatted <see cref="SourceText"/> with a single <see cref="TextChange"/> that encompasses the entire document.
        /// </summary>
        private static async Task<SourceText> GetFormattedDocument(Document document, OptionSet optionSet, CancellationToken cancellationToken)
        {
            var formattedDocument = await Formatter.FormatAsync(document, optionSet, cancellationToken);
            return await formattedDocument.GetTextAsync(cancellationToken);
        }

        /// <summary>
        /// Returns a formatted <see cref="SoureText"/> with multiple <see cref="TextChange"/>s for each formatting change.
        /// </summary>
        private static async Task<SourceText> GetFormattedDocumentWithDetailedChanges(Document document, SourceText sourceText, OptionSet optionSet, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            // Since we've already checked that formatable documents support syntax tree, we know the `root` is not null.
            var formattingTextChanges = Formatter.GetFormattedTextChanges(root!, document.Project.Solution.Workspace, optionSet, cancellationToken);

            return sourceText.WithChanges(formattingTextChanges);
        }
    }
}
