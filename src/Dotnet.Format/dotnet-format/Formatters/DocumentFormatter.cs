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
            var formattedDocuments = FormatFiles(solution, formattableDocuments, formatOptions, logger, cancellationToken);
            return await ApplyFileChangesAsync(solution, formattedDocuments, formatOptions, logger, formattedFiles, cancellationToken);
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
                    var originalSourceText = await document.GetTextAsync(cancellationToken);

                    var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                    if (syntaxTree is null)
                    {
                        return (originalSourceText, null);
                    }

                    var analyzerConfigOptions = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
                    var optionSet = await document.GetOptionsAsync(cancellationToken);

                    return await GetFormattedSourceTextAsync(document, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);
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
            var originalSourceText = await document.GetTextAsync(cancellationToken);
            var formattedSourceText = await FormatFileAsync(document, originalSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);

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

                var (originalText, formattedText) = await formatTask;
                if (formattedText is null)
                {
                    continue;
                }

                var fileChanges = FormatterUtilities.GetFileChanges(
                    document,
                    originalText,
                    formattedText,
                    Name,
                    FormatWarningDescription,
                    formatOptions,
                    logger);
                formattedFiles.Add(new FormattedFile(document, fileChanges));

                formattedSolution = formattedSolution.WithDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity);
            }

            return formattedSolution;
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
