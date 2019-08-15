// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// Base class for code formatters that work against a single document at a time.
    /// </summary>
    internal abstract class DocumentFormatter : ICodeFormatter
    {
        protected abstract string FormatWarningDescription { get; }

        /// <summary>
        /// Applies formatting and returns a formatted <see cref="Solution"/>
        /// </summary>
        public async Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<(DocumentId, OptionSet, ICodingConventionsSnapshot)> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = FormatFiles(solution, formattableDocuments, formatOptions, logger, cancellationToken);
            return await ApplyFileChangesAsync(solution, formattedDocuments, formatOptions, logger, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        protected abstract Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken);

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for each <see cref="Document"/>.
        /// </summary>
        private ImmutableArray<(Document, Task<(SourceText originalText, SourceText formattedText)>)> FormatFiles(
            Solution solution,
            ImmutableArray<(DocumentId, OptionSet, ICodingConventionsSnapshot)> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = ImmutableArray.CreateBuilder<(Document, Task<(SourceText originalText, SourceText formattedText)>)>(formattableDocuments.Length);

            foreach (var (documentId, options, codingConventions) in formattableDocuments)
            {
                var document = solution.GetDocument(documentId);
                var formatTask = Task.Run(async () => await GetFormattedSourceTextAsync(document, options, codingConventions, formatOptions, logger, cancellationToken).ConfigureAwait(false), cancellationToken);

                formattedDocuments.Add((document, formatTask));
            }

            return formattedDocuments.ToImmutableArray();
        }

        /// <summary>
        /// Get formatted <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        private async Task<(SourceText originalText, SourceText formattedText)> GetFormattedSourceTextAsync(
            Document document,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            logger.LogTrace(Resources.Formatting_code_file_0, Path.GetFileName(document.FilePath));

            var originalSourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var formattedSourceText = await FormatFileAsync(document, originalSourceText, options, codingConventions, formatOptions, logger, cancellationToken).ConfigureAwait(false);

            return !formattedSourceText.ContentEquals(originalSourceText) || !formattedSourceText.Encoding.Equals(originalSourceText.Encoding)
                ? (originalSourceText, formattedSourceText)
                : (originalSourceText, null);
        }

        /// <summary>
        /// Applies the changed <see cref="SourceText"/> to each formatted <see cref="Document"/>.
        /// </summary>
        private async Task<Solution> ApplyFileChangesAsync(
            Solution solution,
            ImmutableArray<(Document, Task<(SourceText originalText, SourceText formattedText)>)> formattedDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var (document, formatTask) in formattedDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return formattedSolution;
                }

                var (originalText, formattedText) = await formatTask.ConfigureAwait(false);
                if (formattedText is null)
                {
                    continue;
                }

                if (!formatOptions.SaveFormattedFiles)
                {
                    // Log formatting changes as errors when we are doing a dry-run.
                    LogFormattingChanges(formatOptions.WorkspaceFilePath, document.FilePath, originalText, formattedText, formatOptions.ChangesAreErrors, logger);
                }

                formattedSolution = formattedSolution.WithDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity);
            }

            return formattedSolution;
        }

        private void LogFormattingChanges(string workspacePath, string filePath, SourceText originalText, SourceText formattedText, bool changesAreErrors, ILogger logger)
        {
            var workspaceFolder = Path.GetDirectoryName(workspacePath);
            var changes = formattedText.GetChangeRanges(originalText);

            foreach (var change in changes)
            {
                // LinePosition is zero based so we need to increment to report numbers people expect.
                var changePosition = originalText.Lines.GetLinePosition(change.Span.Start);
                var formatMessage = $"{Path.GetRelativePath(workspaceFolder, filePath)}({changePosition.Line + 1},{changePosition.Character + 1}): {FormatWarningDescription}";

                if (changesAreErrors)
                {
                    logger.LogError(formatMessage);
                }
                else
                {
                    logger.LogWarning(formatMessage);
                }
            }
        }
    }
}
