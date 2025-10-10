// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
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
        /// Gets the fix name to use when logging.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the fix category this formatter belongs to.
        /// </summary>
        public abstract FixCategory Category { get; }

        /// <summary>
        /// Applies formatting and returns a formatted <see cref="Solution"/>
        /// </summary>
        public async Task<Solution> FormatAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
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
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken);

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for each <see cref="Document"/>.
        /// </summary>
        private ImmutableArray<(Document, Task<(SourceText originalText, SourceText? formattedText)>)> FormatFiles(
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = ImmutableArray.CreateBuilder<(Document, Task<(SourceText originalText, SourceText? formattedText)>)>(formattableDocuments.Length);

            for (var index = 0; index < formattableDocuments.Length; index++)
            {
                var document = solution.GetDocument(formattableDocuments[index]);
                if (document is null)
                {
                    continue;
                }

                var formatTask = Task.Run(async () =>
                {
                    var originalSourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                    var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                    if (syntaxTree is null)
                    {
                        return (originalSourceText, null);
                    }

                    var analyzerConfigOptions = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
                    var optionSet = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

                    return await GetFormattedSourceTextAsync(document, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken).ConfigureAwait(false);
                }, cancellationToken);

                formattedDocuments.Add((document, formatTask));
            }

            return formattedDocuments.ToImmutable();
        }

        /// <summary>
        /// Get formatted <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        private async Task<(SourceText originalText, SourceText? formattedText)> GetFormattedSourceTextAsync(
            Document document,
            OptionSet optionSet,
            AnalyzerConfigOptions analyzerConfigOptions,
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

            for (var index = 0; index < formattedDocuments.Length; index++)
            {
                var (document, formatTask) = formattedDocuments[index];
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

                var fileChanges = GetFileChanges(formatOptions, document, originalText, formattedText, formatOptions.ChangesAreErrors, logger);
                formattedFiles.Add(new FormattedFile(document, fileChanges));

                formattedSolution = formattedSolution.WithDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity);
            }

            return formattedSolution;
        }

        private ImmutableArray<FileChange> GetFileChanges(FormatOptions formatOptions, Document document, SourceText originalText, SourceText formattedText, bool changesAreErrors, ILogger logger)
        {
            var fileChanges = ImmutableArray.CreateBuilder<FileChange>();
            var changes = formattedText.GetTextChanges(originalText);

            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];

                var changeMessage = changes.Count > 1 || change.NewText?.Length != formattedText.Length
                    ? BuildChangeMessage(change)
                    : string.Empty;

                var changePosition = originalText.Lines.GetLinePosition(change.Span.Start);

                var fileChange = new FileChange(changePosition, Name, $"{FormatWarningDescription}{changeMessage}");
                fileChanges.Add(fileChange);

                if (!formatOptions.SaveFormattedFiles || formatOptions.LogLevel == LogLevel.Debug)
                {
                    logger.LogFormattingIssue(document, Name, fileChange, changesAreErrors);
                }
            }

            return fileChanges.ToImmutable();

            static string BuildChangeMessage(TextChange change)
            {
                var isDelete = string.IsNullOrEmpty(change.NewText);
                var isAdd = change.Span.Length == 0;
                if (isDelete && isAdd)
                {
                    return string.Empty;
                }

                // Escape characters in the text changes so that it can be more easily read.
                var textChange = change.NewText?.Replace(" ", "\\s").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
                var message = isDelete
                    ? string.Format(Resources.Delete_0_characters, change.Span.Length)
                    : isAdd
                        ? string.Format(Resources.Insert_0, textChange)
                        : string.Format(Resources.Replace_0_characters_with_1, change.Span.Length, textChange);
                return $" {message}";
            }
        }

        protected static async Task<bool> IsSameDocumentAndVersionAsync(Document a, Document b, CancellationToken cancellationToken)
        {
            if (a == b)
            {
                return true;
            }

            if (a.Id != b.Id)
            {
                return false;
            }

            var aVersion = await a.GetTextVersionAsync(cancellationToken).ConfigureAwait(false);
            var bVersion = await b.GetTextVersionAsync(cancellationToken).ConfigureAwait(false);

            return aVersion == bVersion;
        }
    }
}
