// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        /// <summary>
        /// Applies formatting and returns a formatted <see cref="Solution"/>
        /// </summary>
        public async Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<(Document, OptionSet)> formattableDocuments,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = FormatFiles(formattableDocuments, logger, cancellationToken);
            return await ApplyFileChangesAsync(solution, formattedDocuments, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        protected abstract Task<SourceText> FormatFileAsync(
            Document document,
            OptionSet options,
            ILogger logger,
            CancellationToken cancellationToken);

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for each <see cref="Document"/>.
        /// </summary>
        private ImmutableArray<(Document, Task<SourceText>)> FormatFiles(
            ImmutableArray<(Document, OptionSet)> formattableDocuments,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = ImmutableArray.CreateBuilder<(Document, Task<SourceText>)>(formattableDocuments.Length);

            foreach (var (document, options) in formattableDocuments)
            {
                var formatTask = Task.Run(async () => await GetFormattedSourceTextAsync(document, options, logger, cancellationToken).ConfigureAwait(false), cancellationToken);

                formattedDocuments.Add((document, formatTask));
            }

            return formattedDocuments.ToImmutableArray();
        }

        /// <summary>
        /// Get formatted <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        private async Task<SourceText> GetFormattedSourceTextAsync(
            Document document,
            OptionSet options,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            logger.LogTrace(Resources.Formatting_code_file_0, Path.GetFileName(document.FilePath));

            var originalSourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var formattedSourceText = await FormatFileAsync(document, options, logger, cancellationToken).ConfigureAwait(false);

            return !formattedSourceText.ContentEquals(originalSourceText)
                ? formattedSourceText
                : null;
        }

        /// <summary>
        /// Applies the changed <see cref="SourceText"/> to each formatted <see cref="Document"/>.
        /// </summary>
        private static async Task<Solution> ApplyFileChangesAsync(
            Solution solution,
            ImmutableArray<(Document, Task<SourceText>)> formattedDocuments,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var (document, formatTask) in formattedDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return formattedSolution;
                }

                var text = await formatTask.ConfigureAwait(false);
                if (text is null)
                {
                    continue;
                }

                formattedSolution = formattedSolution.WithDocumentText(document.Id, text);
            }

            return formattedSolution;
        }
    }
}
