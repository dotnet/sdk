// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
            new RazorDocumentFormatter(),
            new CompositeWhitespaceFormatter(),
            new OrganizeImportsFormatter(),
            AnalyzerFormatter.CodeStyleFormatter,
            AnalyzerFormatter.ThirdPartyFormatter);

        public static async Task<WorkspaceFormatResult> FormatWorkspaceAsync(
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var logWorkspaceWarnings = formatOptions.LogLevel == LogLevel.Trace;

            logger.LogInformation(string.Format(Resources.Formatting_code_files_in_workspace_0, formatOptions.WorkspaceFilePath));

            logger.LogTrace(Resources.Loading_workspace);

            var workspaceStopwatch = Stopwatch.StartNew();

            using var formatCache = FormatCache.Create(formatOptions, logger);

            using var loadedWorkspace = formatOptions.WorkspaceType == WorkspaceType.Folder
                ? OpenFolderWorkspace(formatOptions.WorkspaceFilePath, formatOptions.FileMatcher)
                : await OpenMSBuildWorkspaceAsync(formatOptions.WorkspaceFilePath, formatOptions.WorkspaceType, formatOptions.NoRestore, formatOptions.FixCategory != FixCategory.Whitespace, formatOptions.BinaryLogPath, logWorkspaceWarnings, logger, formatOptions.TargetFramework, cancellationToken);

            if (loadedWorkspace is null)
            {
                return new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 1);
            }

            var workspace = loadedWorkspace.Workspace;

            if (formatOptions.WorkspaceType == WorkspaceType.Project && loadedWorkspace.ProjectId is { } workspaceProjectId)
            {
                var project = workspace.CurrentSolution.GetProject(workspaceProjectId);
                if (project?.Language != LanguageNames.CSharp && project?.Language != LanguageNames.VisualBasic)
                {
                    logger.LogError(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, formatOptions.WorkspaceFilePath);
                    return new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 1);
                }
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

            var solution = workspace.CurrentSolution;

            logger.LogTrace(Resources.Determining_formattable_files);

            var (fileCount, formatableFiles) = await DetermineFormattableFilesAsync(
                solution, loadedWorkspace.ProjectId, formatOptions, formatCache, logger, cancellationToken);

            var determineFilesMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS;
            logger.LogTrace(Resources.Complete_in_0_ms, determineFilesMS);

            logger.LogTrace(Resources.Running_formatters);

            var formattedFiles = new List<FormattedFile>(fileCount);
            var formattedSolution = await RunCodeFormattersAsync(
                workspace, solution, formatableFiles, formatOptions, logger, formattedFiles, cancellationToken);

            var formatterRanMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS - determineFilesMS;
            logger.LogTrace(Resources.Complete_in_0_ms, formatterRanMS);

            var documentIdsWithErrors = formattedFiles.Select(file => file.DocumentId).Distinct().ToImmutableArray();
            foreach (var documentId in documentIdsWithErrors)
            {
                TextDocument? documentWithError = solution.GetDocument(documentId)
                    ?? solution.GetAdditionalDocument(documentId);
                if (documentWithError is null)
                {
                    documentWithError = await solution.GetSourceGeneratedDocumentAsync(documentId, cancellationToken);
                }

                logger.LogInformation(Resources.Formatted_code_file_0, documentWithError!.FilePath);
            }

            var exitCode = 0;

            // Capture the formatter's output snapshot before saving, not a subsequent disk edit.
            var formattedTexts = new List<KeyValuePair<string, SourceText>>();
            if (formatCache is not null)
            {
                foreach (var documentId in formatableFiles)
                {
                    var document = formattedSolution.GetDocument(documentId);
                    if (document?.FilePath is string filePath)
                    {
                        formattedTexts.Add(new(filePath, await document.GetTextAsync(cancellationToken)));
                    }
                }
            }

            if (formatOptions.SaveFormattedFiles && !workspace.TryApplyChanges(formattedSolution))
            {
                logger.LogError(Resources.Failed_to_save_formatting_changes);
                exitCode = 1;
            }
            else if (formatCache is not null)
            {
                formatCache.RecordFormattedFiles(formattedTexts, GetSolutionFilePaths(solution));
            }

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(formatOptions.ReportPath))
            {
                ReportWriter.Write(formatOptions.ReportPath!, formattedFiles, logger);
            }

            logger.LogDebug(Resources.Formatted_0_of_1_files, documentIdsWithErrors.Length, fileCount);

            logger.LogInformation(Resources.Format_complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

            return new WorkspaceFormatResult(documentIdsWithErrors.Length, fileCount, exitCode);
        }

        private static LoadedWorkspace OpenFolderWorkspace(string workspacePath, SourceFileMatcher fileMatcher)
        {
            var folderWorkspace = FolderWorkspace.Create();
            folderWorkspace.OpenFolder(workspacePath, fileMatcher);
            return new LoadedWorkspace(folderWorkspace, ProjectId: null);
        }

        private static IEnumerable<string> GetSolutionFilePaths(Solution solution)
        {
            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if (document.FilePath is not null)
                    {
                        yield return document.FilePath;
                    }
                }
            }
        }

        private static async Task<LoadedWorkspace?> OpenMSBuildWorkspaceAsync(
            string solutionOrProjectPath,
            WorkspaceType workspaceType,
            bool noRestore,
            bool requiresSemantics,
            string? binaryLogPath,
            bool logWorkspaceWarnings,
            ILogger logger,
            string? targetFramework,
            CancellationToken cancellationToken)
        {
            if (requiresSemantics &&
                !noRestore &&
                await DotNetHelper.PerformRestoreAsync(solutionOrProjectPath, logger) != 0)
            {
                throw new Exception("Restore operation failed.");
            }

            return await MSBuildWorkspaceLoader.LoadAsync(solutionOrProjectPath, workspaceType, binaryLogPath, logWorkspaceWarnings, logger, targetFramework, cancellationToken);
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

                formattedSolution = await s_codeFormatters[index].FormatAsync(workspace, formattedSolution, formattableDocuments, formatOptions, logger, formattedFiles, cancellationToken);
            }

            return formattedSolution;
        }

        internal static async Task<(int, ImmutableArray<DocumentId>)> DetermineFormattableFilesAsync(
            Solution solution,
            ProjectId? projectId,
            FormatOptions formatOptions,
            FormatCache? cache,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Debug.Assert(formatOptions.WorkspaceType is not WorkspaceType.Project || projectId is not null);

            var totalFileCount = solution.Projects.Sum(project => project.DocumentIds.Count + project.AdditionalDocumentIds.Count);
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
                if (projectId != null && project.Id != projectId)
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

                    if (cache is not null && cache.IsUpToDate(document.FilePath))
                    {
                        continue;
                    }

                    var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                    if (syntaxTree is null)
                    {
                        throw new Exception($"Unable to get a syntax tree for '{document.Name}'");
                    }

                    if (await GeneratedCodeUtilities.IsGeneratedCodeAsync(syntaxTree, cancellationToken))
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

                if (formatOptions.FixCategory.HasFlag(FixCategory.Whitespace))
                {
                    foreach (var document in project.AdditionalDocuments)
                    {
                        if (document.FilePath is null ||
                            !FormatterUtilities.IsRazorDocument(document) ||
                            !addedFilePaths.Add(document.FilePath))
                        {
                            continue;
                        }

                        projectFileCount++;

                        var isFileIncluded = formatOptions.WorkspaceType == WorkspaceType.Folder ||
                            (formatOptions.FileMatcher.HasMatches(document.FilePath) && File.Exists(document.FilePath));
                        if (!isFileIncluded)
                        {
                            continue;
                        }

                        var analyzerConfigOptions = FormatterUtilities.GetAnalyzerConfigOptionsForAdditionalDocument(document);

                        if (analyzerConfigOptions is not null)
                        {
                            documentsCoveredByEditorConfig.Add(document.Id);
                        }
                        else
                        {
                            documentsNotCoveredByEditorConfig.Add(document.Id);
                        }
                    }
                }

                if (formatOptions.IncludeGeneratedFiles)
                {
                    var generatedDocuments = await project.GetSourceGeneratedDocumentsAsync(cancellationToken);
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
