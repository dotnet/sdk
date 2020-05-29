// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

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
            ImmutableArray<DocumentWithOptions> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = FormatFiles(solution, formattableDocuments, formatOptions, logger, cancellationToken);
            return await ApplyFileChangesAsync(solution, formattedDocuments, formatOptions, logger, formattedFiles, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        internal abstract Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions? analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken);

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for each <see cref="Document"/>.
        /// </summary>
        private ImmutableArray<(Document, Task<(SourceText originalText, SourceText? formattedText)>)> FormatFiles(
            Solution solution,
            ImmutableArray<DocumentWithOptions> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = ImmutableArray.CreateBuilder<(Document, Task<(SourceText originalText, SourceText? formattedText)>)>(formattableDocuments.Length);

            foreach (var formattableDocument in formattableDocuments)
            {
                var document = solution.GetDocument(formattableDocument.Document.Id);
                if (document is null)
                    continue;

                var formatTask = Task.Run(async ()
                    => await GetFormattedSourceTextAsync(document, formattableDocument.OptionSet, formattableDocument.AnalyzerConfigOptions, formatOptions, logger, cancellationToken).ConfigureAwait(false), cancellationToken);

                formattedDocuments.Add((document, formatTask));
            }

            return formattedDocuments.ToImmutableArray();
        }

        /// <summary>
        /// Get formatted <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        private async Task<(SourceText originalText, SourceText? formattedText)> GetFormattedSourceTextAsync(
            Document document,
            OptionSet optionSet,
            AnalyzerConfigOptions? analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var originalSourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var formattedSourceText = await FormatFileAsync(document, originalSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken).ConfigureAwait(false);

            return !formattedSourceText.ContentEquals(originalSourceText) || !formattedSourceText.Encoding?.Equals(originalSourceText.Encoding) == true
                ? (originalSourceText, formattedSourceText)
                : (originalSourceText, null);
        }

        /// <summary>
        /// Applies the changed <see cref="SourceText"/> to each formatted <see cref="Document"/>.
        /// </summary>
        private async Task<Solution> ApplyFileChangesAsync(
           Solution solution,
            ImmutableArray<(Document, Task<(SourceText originalText, SourceText? formattedText)>)> formattedDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var (document, formatTask) in formattedDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return formattedSolution;
                }

                if (document?.FilePath is null)
                {
                    continue;
                }

                var (originalText, formattedText) = await formatTask.ConfigureAwait(false);
                if (formattedText is null)
                {
                    continue;
                }

                var fileChanges = GetFileChanges(formatOptions, formatOptions.WorkspaceFilePath, document.FilePath, originalText, formattedText, formatOptions.ChangesAreErrors, logger);
                formattedFiles.Add(new FormattedFile(document, fileChanges));

                formattedSolution = formattedSolution.WithDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity);
            }

            return formattedSolution;
        }

        private IEnumerable<FileChange> GetFileChanges(FormatOptions formatOptions, string workspacePath, string filePath, SourceText originalText, SourceText formattedText, bool changesAreErrors, ILogger logger)
        {
            var fileChanges = new List<FileChange>();
            var workspaceFolder = Path.GetDirectoryName(workspacePath);
            var changes = formattedText.GetChangeRanges(originalText);
            if (workspaceFolder is null)
            {
                throw new Exception($"Unable to find directory name for '{workspacePath}'");
            }

            foreach (var change in changes)
            {
                var changePosition = originalText.Lines.GetLinePosition(change.Span.Start);
                var fileChange = new FileChange(changePosition, FormatWarningDescription);
                fileChanges.Add(fileChange);

                if (!formatOptions.SaveFormattedFiles || formatOptions.LogLevel == LogLevel.Trace)
                {
                    LogFormattingChanges(filePath, changesAreErrors, logger, workspaceFolder, fileChange);
                }
            }

            return fileChanges;
        }

        private static void LogFormattingChanges(string filePath, bool changesAreErrors, ILogger logger, string workspaceFolder, FileChange fileChange)
        {
            var formatMessage = $"{Path.GetRelativePath(workspaceFolder, filePath)}({fileChange.LineNumber},{fileChange.CharNumber}): {fileChange.FormatDescription}";
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
