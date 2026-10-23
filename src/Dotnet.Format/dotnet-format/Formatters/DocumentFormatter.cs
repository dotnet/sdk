// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            var (formattedDocuments, fileChanges) = await FormatFilesAsync(solution, formattableDocuments, formatOptions, logger, cancellationToken);
            return await ApplyFileChangesAsync(solution, formattedDocuments, fileChanges, formatOptions, logger, formattedFiles, cancellationToken);
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
        /// Formats each <see cref="Document"/> concurrently and returns the changed text in document order.
        /// </summary>
        private async Task<(ImmutableArray<(Document, SourceText originalText, SourceText? formattedText)> Documents, ImmutableArray<ImmutableArray<FileChange>> FileChanges)> FormatFilesAsync(
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var results = new (Document, SourceText, SourceText?)?[formattableDocuments.Length];

            await Parallel.ForEachAsync(Enumerable.Range(0, formattableDocuments.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                },
                async (index, cancellationToken) =>
                {
                    var document = solution.GetDocument(formattableDocuments[index]);
                    if (document is null)
                    {
                        return;
                    }

                    var originalSourceText = await document.GetTextAsync(cancellationToken);

                    var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                    if (syntaxTree is null)
                    {
                        results[index] = (document, originalSourceText, null);
                        return;
                    }

                    var analyzerConfigOptions = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
                    var optionSet = await document.GetOptionsAsync(cancellationToken);

                    var formattedSourceText = await FormatFileAsync(document, originalSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);

                    results[index] = !formattedSourceText.ContentEquals(originalSourceText) || !formattedSourceText.Encoding?.Equals(originalSourceText.Encoding) == true
                        ? (document, originalSourceText, formattedSourceText)
                        : (document, originalSourceText, null);
                });

            var formattedDocuments = ImmutableArray.CreateBuilder<(Document, SourceText originalText, SourceText? formattedText)>(formattableDocuments.Length);
            for (var index = 0; index < results.Length; index++)
            {
                if (results[index] is { } result)
                {
                    formattedDocuments.Add(result);
                }
            }

            var formattedDocumentsArray = formattedDocuments.MoveToImmutable();

            var fileChanges = new ImmutableArray<FileChange>[formattedDocumentsArray.Length];
            await Parallel.ForEachAsync(Enumerable.Range(0, formattedDocumentsArray.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                },
                (index, cancellationToken) =>
                {
                    var (document, originalText, formattedText) = formattedDocumentsArray[index];
                    fileChanges[index] = document.FilePath is not null && formattedText is not null
                        ? GetFileChanges(document, originalText, formattedText)
                        : ImmutableArray<FileChange>.Empty;
                    return ValueTask.CompletedTask;
                });

            return (formattedDocumentsArray, fileChanges.ToImmutableArray());
        }

        /// <summary>
        /// Applies the changed <see cref="SourceText"/> to each formatted <see cref="Document"/>.
        /// </summary>
        private async Task<Solution> ApplyFileChangesAsync(
            Solution solution,
            ImmutableArray<(Document, SourceText originalText, SourceText? formattedText)> formattedDocuments,
            ImmutableArray<ImmutableArray<FileChange>> fileChanges,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            for (var index = 0; index < formattedDocuments.Length; index++)
            {
                var (document, _, formattedText) = formattedDocuments[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    return formattedSolution;
                }

                if (document?.FilePath is null)
                {
                    continue;
                }

                if (formattedText is null)
                {
                    continue;
                }

                var documentFileChanges = fileChanges[index];
                formattedFiles.Add(new FormattedFile(document, documentFileChanges));

                LogFileChanges(formatOptions, document, documentFileChanges, formatOptions.ChangesAreErrors, logger);

                formattedSolution = formattedSolution.WithDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity);
            }

            return formattedSolution;
        }

        private ImmutableArray<FileChange> GetFileChanges(Document document, SourceText originalText, SourceText formattedText)
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

        private void LogFileChanges(FormatOptions formatOptions, Document document, ImmutableArray<FileChange> fileChanges, bool changesAreErrors, ILogger logger)
        {
            if (formatOptions.SaveFormattedFiles && formatOptions.LogLevel != LogLevel.Debug)
            {
                return;
            }

            foreach (var fileChange in fileChanges)
            {
                logger.LogFormattingIssue(document, Name, fileChange, changesAreErrors);
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

            var aVersion = await a.GetTextVersionAsync(cancellationToken);
            var bVersion = await b.GetTextVersionAsync(cancellationToken);

            return aVersion == bVersion;
        }
    }
}
