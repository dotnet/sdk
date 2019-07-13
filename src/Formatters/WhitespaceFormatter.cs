// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// CodeFormatter that uses the <see cref="Formatter"/> to format document whitespace.
    /// </summary>
    internal sealed class WhitespaceFormatter : DocumentFormatter
    {
        protected override string FormatWarningDescription => Resources.Fix_whitespace_formatting;

        protected override async Task<SourceText> FormatFileAsync(
            Document document,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (formatOptions.SaveFormattedFiles)
            {
                return await GetFormattedDocument(document, options, cancellationToken);
            }
            else
            {
                return await GetFormattedDocumentWithDetailedChanges(document, options, cancellationToken);
            }
        }

        /// <summary>
        /// Returns a formatted <see cref="SourceText"/> with a single <see cref="TextChange"/> that encompasses the entire document.
        /// </summary>
        private static async Task<SourceText> GetFormattedDocument(Document document, OptionSet options, CancellationToken cancellationToken)
        {
            var formattedDocument = await Formatter.FormatAsync(document, options, cancellationToken).ConfigureAwait(false);
            return await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a formatted <see cref="SoureText"/> with multiple <see cref="TextChange"/>s for each formatting change.
        /// </summary>
        private static async Task<SourceText> GetFormattedDocumentWithDetailedChanges(Document document, OptionSet options, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var originalText = await document.GetTextAsync(cancellationToken);

            var formattingTextChanges = Formatter.GetFormattedTextChanges(root, document.Project.Solution.Workspace, options, cancellationToken);
            return originalText.WithChanges(formattingTextChanges);
        }
    }
}
