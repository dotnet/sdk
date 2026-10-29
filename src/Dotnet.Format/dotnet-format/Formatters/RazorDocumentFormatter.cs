// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal sealed class RazorDocumentFormatter : ICodeFormatter
    {
        private const string Name = "WHITESPACE";

        public FixCategory Category => FixCategory.Whitespace;

        public async Task<Solution> FormatAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var documentId in formattableDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var document = formattedSolution.GetAdditionalDocument(documentId);
                if (document is null || !FormatterUtilities.IsRazorDocument(document))
                {
                    continue;
                }

                var originalText = await document.GetTextAsync(cancellationToken);
                var formattedText = await RazorFormatter.TryFormatAsync(document.Project, document.Id, cancellationToken);
                if (formattedText is null ||
                    (formattedText.ContentEquals(originalText) && formattedText.Encoding?.Equals(originalText.Encoding) != false))
                {
                    continue;
                }

                var fileChanges = FormatterUtilities.GetFileChanges(
                    document,
                    originalText,
                    formattedText,
                    Name,
                    Resources.Fix_whitespace_formatting,
                    formatOptions,
                    logger);
                formattedFiles.Add(new FormattedFile(document, fileChanges));

                formattedSolution = formattedSolution.WithAdditionalDocumentText(
                    document.Id,
                    formattedText,
                    PreservationMode.PreserveIdentity);
            }

            return formattedSolution;
        }
    }
}
