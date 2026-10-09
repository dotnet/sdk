// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Utilities;
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
                var project = await AddEditorConfigDocumentsAsync(document.Project, document.FilePath!, cancellationToken);
                var formattedText = await RazorFormatter.TryFormatAsync(project, document.Id, cancellationToken);
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

        private static async Task<Project> AddEditorConfigDocumentsAsync(
            Project project,
            string documentFilePath,
            CancellationToken cancellationToken)
        {
            var existingPaths = project.AnalyzerConfigDocuments
                .Select(static document => document.FilePath)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var solution = project.Solution;

            foreach (var editorConfigPath in EditorConfigFinder.GetEditorConfigPathsForFiles([documentFilePath]))
            {
                if (!existingPaths.Add(editorConfigPath))
                {
                    continue;
                }

                var text = SourceText.From(await File.ReadAllTextAsync(editorConfigPath, cancellationToken));
                solution = solution.AddAnalyzerConfigDocument(
                    DocumentId.CreateNewId(project.Id, debugName: editorConfigPath),
                    Path.GetFileName(editorConfigPath),
                    text,
                    filePath: editorConfigPath);
            }

            return solution.GetProject(project.Id)
                ?? throw new InvalidOperationException($"Project '{project.Name}' was not found after loading analyzer configuration.");
        }
    }
}
