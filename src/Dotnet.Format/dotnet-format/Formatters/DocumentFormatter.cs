// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
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
        /// Applies formatting and returns a formatted <see cref="Solution"/>.
        /// </summary>
        public Task<Solution> FormatAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
            => FormatAsync(ImmutableArray.Create(this), solution, formattableDocuments, formatOptions, logger, formattedFiles, cancellationToken);

        internal static async Task<Solution> FormatAsync(
            ImmutableArray<DocumentFormatter> formatters,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var results = new (TextDocument document, SourceText originalText, SourceText formattedText)?[formatters.Length, formattableDocuments.Length];

            // Bound concurrent formatting while retaining document order for applying and reporting changes.
            await Parallel.ForEachAsync(Enumerable.Range(0, formattableDocuments.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                },
                async (index, token) =>
                {
                    TextDocument? document = solution.GetDocument(formattableDocuments[index]);
                    if (document is null)
                    {
                        document = solution.GetAdditionalDocument(formattableDocuments[index]);
                        if (document is null || !FormatterUtilities.IsRazorDocument(document))
                        {
                            return;
                        }
                    }

                    var originalText = await document.GetTextAsync(token);
                    AnalyzerConfigOptions? analyzerConfigOptions;
                    if (document is Document sourceDocument)
                    {
                        var syntaxTree = await sourceDocument.GetSyntaxTreeAsync(token);
                        if (syntaxTree is null)
                        {
                            return;
                        }

                        analyzerConfigOptions = sourceDocument.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
                    }
                    else
                    {
                        analyzerConfigOptions = FormatterUtilities.GetAnalyzerConfigOptionsForAdditionalDocument(document);
                    }

                    if (analyzerConfigOptions is null)
                    {
                        return;
                    }

                    for (var stage = 0; stage < formatters.Length; stage++)
                    {
                        var formattedText = await formatters[stage].FormatFileAsync(document, originalText, analyzerConfigOptions, formatOptions, logger, token);
                        if (!formattedText.ContentEquals(originalText) || !Equals(formattedText.Encoding, originalText.Encoding))
                        {
                            results[stage, index] = (document, originalText, formattedText);
                        }

                        originalText = formattedText;
                    }
                });

            var formattedSolution = solution;
            // Report in formatter-first order without repeating per-document setup.
            for (var stage = 0; stage < formatters.Length; stage++)
            {
                var formatter = formatters[stage];
                for (var index = 0; index < formattableDocuments.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (results[stage, index] is not { } value || value.document.FilePath is null)
                    {
                        continue;
                    }

                    var (document, originalText, formattedText) = value;
                    var fileChanges = FormatterUtilities.GetFileChanges(
                        document, originalText, formattedText, formatter.Name, formatter.FormatWarningDescription, formatOptions, logger);
                    formattedFiles.Add(new FormattedFile(document, fileChanges));

                    formattedSolution = document is Document
                        ? formattedSolution.WithDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity)
                        : formattedSolution.WithAdditionalDocumentText(document.Id, formattedText, PreservationMode.PreserveIdentity);
                }
            }

            return formattedSolution;
        }

        /// <summary>
        /// Applies formatting and returns the changed text for a source or Razor document.
        /// </summary>
        internal abstract Task<SourceText> FormatFileAsync(
            TextDocument document,
            SourceText sourceText,
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken);

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
