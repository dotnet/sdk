// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class CodeFormatter
    {
        private static readonly ImmutableArray<ICodeFormatter> s_codeFormatters = ImmutableArray.Create<ICodeFormatter>(
            new WhitespaceFormatter(),
            new FinalNewlineFormatter(),
            new EndOfLineFormatter(),
            new CharsetFormatter(),
            new OrganizeImportsFormatter(),
            AnalyzerFormatter.CodeStyleFormatter,
            AnalyzerFormatter.ThirdPartyFormatter);

        public static async Task<WorkspaceFormatResult> FormatWorkspaceAsync(
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken,
            string? binaryLogPath = null)
        {
            var logWorkspaceWarnings = formatOptions.LogLevel == LogLevel.Trace;

            logger.LogInformation(string.Format(Resources.Formatting_code_files_in_workspace_0, formatOptions.WorkspaceFilePath));

            logger.LogTrace(Resources.Loading_workspace);

            var workspaceStopwatch = Stopwatch.StartNew();

            using var workspace = formatOptions.WorkspaceType == WorkspaceType.Folder
                ? OpenFolderWorkspace(formatOptions.WorkspaceFilePath, formatOptions.FileMatcher)
                : await OpenMSBuildWorkspaceAsync(formatOptions.WorkspaceFilePath, formatOptions.WorkspaceType, formatOptions.NoRestore, formatOptions.FixCategory != FixCategory.Whitespace, binaryLogPath, logWorkspaceWarnings, logger, cancellationToken).ConfigureAwait(false);

            if (workspace is null)
            {
                return new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 1);
            }

            if (formatOptions.LogLevel <= LogLevel.Debug)
            {
                foreach (var project in workspace.CurrentSolution.Projects)
                {
                    foreach (var configDocument in project.AnalyzerConfigDocuments)
                    {
                        logger.LogDebug(Resources.Project_0_is_using_configuration_from_1, project.Name, configDocument.FilePath);
                    }
                }
            }

            var loadWorkspaceMS = workspaceStopwatch.ElapsedMilliseconds;
            logger.LogTrace(Resources.Complete_in_0_ms, loadWorkspaceMS);

            var projectPath = formatOptions.WorkspaceType == WorkspaceType.Project ? formatOptions.WorkspaceFilePath : string.Empty;
            var solution = workspace.CurrentSolution;

            logger.LogTrace(Resources.Determining_formattable_files);

            var (fileCount, formatableFiles) = await DetermineFormattableFilesAsync(
                solution, projectPath, formatOptions, logger, cancellationToken).ConfigureAwait(false);

            var determineFilesMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS;
            logger.LogTrace(Resources.Complete_in_0_ms, determineFilesMS);

            logger.LogTrace(Resources.Running_formatters);

            var formattedFiles = new List<FormattedFile>(fileCount);
            var formattedSolution = await RunCodeFormattersAsync(
                workspace, solution, formatableFiles, formatOptions, logger, formattedFiles, cancellationToken).ConfigureAwait(false);

            var formatterRanMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS - determineFilesMS;
            logger.LogTrace(Resources.Complete_in_0_ms, formatterRanMS);

            var documentIdsWithErrors = formattedFiles.Select(file => file.DocumentId).Distinct().ToImmutableArray();
            foreach (var documentId in documentIdsWithErrors)
            {
                var documentWithError = solution.GetDocument(documentId);
                if (documentWithError is null)
                {
                    documentWithError = await solution.GetSourceGeneratedDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
                }

                logger.LogInformation(Resources.Formatted_code_file_0, documentWithError!.FilePath);
            }

            var exitCode = 0;

            if (formatOptions.SaveFormattedFiles && !workspace.TryApplyChanges(formattedSolution))
            {
                logger.LogError(Resources.Failed_to_save_formatting_changes);
                exitCode = 1;
            }

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(formatOptions.ReportPath))
            {
                ReportWriter.Write(formatOptions.ReportPath!, formattedFiles, logger);
            }

            logger.LogDebug(Resources.Formatted_0_of_1_files, documentIdsWithErrors.Length, fileCount);

            logger.LogInformation(Resources.Format_complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

            return new WorkspaceFormatResult(documentIdsWithErrors.Length, fileCount, exitCode);
        }

        private static Workspace OpenFolderWorkspace(string workspacePath, SourceFileMatcher fileMatcher)
        {
            var folderWorkspace = FolderWorkspace.Create();
            folderWorkspace.OpenFolder(workspacePath, fileMatcher);
            return folderWorkspace;
        }

        private static async Task<Workspace?> OpenMSBuildWorkspaceAsync(
            string solutionOrProjectPath,
            WorkspaceType workspaceType,
            bool noRestore,
            bool requiresSemantics,
            string? binaryLogPath,
            bool logWorkspaceWarnings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (requiresSemantics &&
                !noRestore &&
                await DotNetHelper.PerformRestoreAsync(solutionOrProjectPath, logger) != 0)
            {
                throw new Exception("Restore operation failed.");
            }

            return await MSBuildWorkspaceLoader.LoadAsync(solutionOrProjectPath, workspaceType, binaryLogPath, logWorkspaceWarnings, logger, cancellationToken);
        }

        private static async Task<Solution> RunCodeFormattersAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            for (var index = 0; index < s_codeFormatters.Length; index++)
            {
                // Only run the formatter if it belongs to one of the categories being fixed.
                if (!formatOptions.FixCategory.HasFlag(s_codeFormatters[index].Category))
                {
                    continue;
                }

                formattedSolution = await s_codeFormatters[index].FormatAsync(workspace, formattedSolution, formattableDocuments, formatOptions, logger, formattedFiles, cancellationToken).ConfigureAwait(false);
            }

            return formattedSolution;
        }

        internal static async Task<(int, ImmutableArray<DocumentId>)> DetermineFormattableFilesAsync(
            Solution solution,
            string projectPath,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var totalFileCount = solution.Projects.Sum(project => project.DocumentIds.Count);
            var projectFileCount = 0;

            var documentsCoveredByEditorConfig = ImmutableArray.CreateBuilder<DocumentId>(totalFileCount);
            var documentsNotCoveredByEditorConfig = ImmutableArray.CreateBuilder<DocumentId>(totalFileCount);
            var sourceGeneratedDocuments = ImmutableArray.CreateBuilder<DocumentId>();

            var addedFilePaths = new HashSet<string>(totalFileCount);

            foreach (var project in solution.Projects)
            {
                if (project?.FilePath is null)
                {
                    continue;
                }

                // If a project is used as a workspace, then ignore other referenced projects.
                if (!string.IsNullOrEmpty(projectPath) && !project.FilePath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug(Resources.Skipping_referenced_project_0, project.Name);
                    continue;
                }

                // Ignore unsupported project types.
                if (project.Language != LanguageNames.CSharp && project.Language != LanguageNames.VisualBasic)
                {
                    logger.LogWarning(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, project.FilePath);
                    continue;
                }

                projectFileCount += project.DocumentIds.Count;

                foreach (var document in project.Documents)
                {
                    // If we've already added this document, either via a link or multi-targeted framework, then ignore.
                    if (document?.FilePath is null ||
                        addedFilePaths.Contains(document.FilePath))
                    {
                        continue;
                    }

                    addedFilePaths.Add(document.FilePath);

                    var isFileIncluded = formatOptions.WorkspaceType == WorkspaceType.Folder ||
                        (formatOptions.FileMatcher.HasMatches(document.FilePath) && File.Exists(document.FilePath));
                    if (!isFileIncluded || !document.SupportsSyntaxTree)
                    {
                        continue;
                    }

                    var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                    if (syntaxTree is null)
                    {
                        throw new Exception($"Unable to get a syntax tree for '{document.Name}'");
                    }

                    if (await GeneratedCodeUtilities.IsGeneratedCodeAsync(syntaxTree, cancellationToken).ConfigureAwait(false))
                    {
                        if (!formatOptions.IncludeGeneratedFiles)
                        {
                            continue;
                        }
                        else
                        {
                            Debug.WriteLine($"Including generated file '{document.FilePath}'.");
                        }
                    }

                    // Track files covered by an editorconfig separately from those not covered.
                    var analyzerConfigOptions = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
                    if (analyzerConfigOptions != null)
                    {
                        if (formatOptions.IncludeGeneratedFiles ||
                            GeneratedCodeUtilities.GetIsGeneratedCodeFromOptions(analyzerConfigOptions) != true)
                        {
                            documentsCoveredByEditorConfig.Add(document.Id);
                        }
                    }
                    else
                    {
                        documentsNotCoveredByEditorConfig.Add(document.Id);
                    }
                }

                if (formatOptions.IncludeGeneratedFiles)
                {
                    var generatedDocuments = await project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var generatedDocument in generatedDocuments)
                    {
                        Debug.WriteLine($"Including source generated file '{generatedDocument.FilePath}'.");
                        sourceGeneratedDocuments.Add(generatedDocument.Id);
                    }
                }
            }

            // Initially we would format all documents in a workspace, even if some files weren't covered by an
            // .editorconfig and would have defaults applied. This behavior was an early requested change since
            // users were surprised to have files not specified by the .editorconfig modified. The assumption is
            // that users without an .editorconfig still wanted formatting (they did run a formatter after all),
            // so we run on all files with defaults.

            // If no files are covered by an editorconfig, then return them all. Otherwise only return
            // files that are covered by an editorconfig.
            var formattableDocuments = documentsCoveredByEditorConfig.Count == 0
                ? documentsNotCoveredByEditorConfig
                : documentsCoveredByEditorConfig;

            formattableDocuments.AddRange(sourceGeneratedDocuments);
            return (projectFileCount + sourceGeneratedDocuments.Count, formattableDocuments.ToImmutable());
        }
    }
}
