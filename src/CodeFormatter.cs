// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class CodeFormatter
    {
        private static readonly ImmutableArray<ICodeFormatter> s_codeFormatters = new ICodeFormatter[]
        {
            new WhitespaceFormatter(),
            new FinalNewlineFormatter(),
            new EndOfLineFormatter(),
            new CharsetFormatter(),
        }.ToImmutableArray();

        public static async Task<WorkspaceFormatResult> FormatWorkspaceAsync(
            FormatOptions options,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var (workspaceFilePath, workspaceType, logLevel, saveFormattedFiles, _, filesToFormat, reportPath) = options;
            var logWorkspaceWarnings = logLevel == LogLevel.Trace;

            logger.LogInformation(string.Format(Resources.Formatting_code_files_in_workspace_0, workspaceFilePath));

            logger.LogTrace(Resources.Loading_workspace);

            var workspaceStopwatch = Stopwatch.StartNew();

            using (var workspace = await OpenWorkspaceAsync(
                workspaceFilePath, workspaceType, filesToFormat, logWorkspaceWarnings, logger, cancellationToken).ConfigureAwait(false))
            {
                if (workspace is null)
                {
                    return new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 1);
                }

                var loadWorkspaceMS = workspaceStopwatch.ElapsedMilliseconds;
                logger.LogTrace(Resources.Complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

                var projectPath = workspaceType == WorkspaceType.Project ? workspaceFilePath : string.Empty;
                var solution = workspace.CurrentSolution;

                logger.LogTrace(Resources.Determining_formattable_files);

                var (fileCount, formatableFiles) = await DetermineFormattableFiles(
                    solution, projectPath, filesToFormat, logger, cancellationToken).ConfigureAwait(false);

                var determineFilesMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS;
                logger.LogTrace(Resources.Complete_in_0_ms, determineFilesMS);

                logger.LogTrace(Resources.Running_formatters);

                var formattedFiles = new List<FormattedFile>();
                var formattedSolution = await RunCodeFormattersAsync(
                    solution, formatableFiles, options, logger, formattedFiles, cancellationToken).ConfigureAwait(false);

                var formatterRanMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS - determineFilesMS;
                logger.LogTrace(Resources.Complete_in_0_ms, formatterRanMS);

                var solutionChanges = formattedSolution.GetChanges(solution);

                var filesFormatted = 0;
                foreach (var projectChanges in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChanges.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);
                        logger.LogInformation(Resources.Formatted_code_file_0, Path.GetFileName(changedDocument.FilePath));
                        filesFormatted++;
                    }
                }

                var exitCode = 0;

                if (saveFormattedFiles && !workspace.TryApplyChanges(formattedSolution))
                {
                    logger.LogError(Resources.Failed_to_save_formatting_changes);
                    exitCode = 1;
                }

                if (exitCode == 0 && !string.IsNullOrWhiteSpace(reportPath))
                {
                    var reportFilePath = GetReportFilePath(reportPath);

                    logger.LogInformation(Resources.Writing_formatting_report_to_0, reportFilePath);
                    var seralizerOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var formattedFilesJson = JsonSerializer.Serialize(formattedFiles, seralizerOptions);

                    File.WriteAllText(reportFilePath, formattedFilesJson);
                }

                logger.LogDebug(Resources.Formatted_0_of_1_files, filesFormatted, fileCount);

                logger.LogInformation(Resources.Format_complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

                return new WorkspaceFormatResult(filesFormatted, fileCount, exitCode);
            }
        }

        private static string GetReportFilePath(string reportPath)
        {
            var defaultReportName = "format-report.json";
            if (reportPath.EndsWith(".json"))
            {
                return reportPath;
            }
            else if (reportPath == ".")
            {
                return Path.Combine(Environment.CurrentDirectory, defaultReportName);
            }
            else
            {
                return Path.Combine(reportPath, defaultReportName);
            }
        }

        private static async Task<Workspace> OpenWorkspaceAsync(
            string workspacePath,
            WorkspaceType workspaceType,
            ImmutableHashSet<string> filesToFormat,
            bool logWorkspaceWarnings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (workspaceType == WorkspaceType.Folder)
            {
                var folderWorkspace = FolderWorkspace.Create();
                await folderWorkspace.OpenFolder(workspacePath, filesToFormat, cancellationToken);
                return folderWorkspace;
            }

            return await OpenMSBuildWorkspaceAsync(workspacePath, workspaceType, logWorkspaceWarnings, logger, cancellationToken);
        }

        private static async Task<Workspace> OpenMSBuildWorkspaceAsync(
            string solutionOrProjectPath,
            WorkspaceType workspaceType,
            bool logWorkspaceWarnings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // This property ensures that XAML files will be compiled in the current AppDomain
                // rather than a separate one. Any tasks isolated in AppDomains or tasks that create
                // AppDomains will likely not work due to https://github.com/Microsoft/MSBuildLocator/issues/16.
                { "AlwaysCompileMarkupFilesInSeparateDomain", bool.FalseString },
                // This flag is used at restore time to avoid imports from packages changing the inputs to restore,
                // without this it is possible to get different results between the first and second restore.
                { "ExcludeRestorePackageImports", bool.TrueString },
            };

            var workspace = MSBuildWorkspace.Create(properties);

            if (workspaceType == WorkspaceType.Solution)
            {
                await workspace.OpenSolutionAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await workspace.OpenProjectAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    logger.LogError(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, solutionOrProjectPath);
                    workspace.Dispose();
                    return null;
                }
            }

            LogWorkspaceDiagnostics(logger, logWorkspaceWarnings, workspace.Diagnostics);

            return workspace;
        }

        private static void LogWorkspaceDiagnostics(ILogger logger, bool logWorkspaceWarnings, ImmutableList<WorkspaceDiagnostic> diagnostics)
        {
            if (!logWorkspaceWarnings)
            {
                if (diagnostics.Count > 0)
                {
                    logger.LogWarning(Resources.Warnings_were_encountered_while_loading_the_workspace_Set_the_verbosity_option_to_the_diagnostic_level_to_log_warnings);
                }

                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    logger.LogError(diagnostic.Message);
                }
                else
                {
                    logger.LogWarning(diagnostic.Message);
                }
            }
        }

        private static async Task<Solution> RunCodeFormattersAsync(
            Solution solution,
            ImmutableArray<(DocumentId, OptionSet, ICodingConventionsSnapshot)> formattableDocuments,
            FormatOptions options,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var codeFormatter in s_codeFormatters)
            {
                formattedSolution = await codeFormatter.FormatAsync(formattedSolution, formattableDocuments, options, logger, formattedFiles, cancellationToken).ConfigureAwait(false);
            }

            return formattedSolution;
        }

        internal static async Task<(int, ImmutableArray<(DocumentId, OptionSet, ICodingConventionsSnapshot)>)> DetermineFormattableFiles(
            Solution solution,
            string projectPath,
            ImmutableHashSet<string> filesToFormat,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var codingConventionsManager = CodingConventionsManagerFactory.CreateCodingConventionsManager();
            var optionsApplier = new EditorConfigOptionsApplier();

            var fileCount = 0;
            var getDocumentsAndOptions = new List<Task<(Document, OptionSet, ICodingConventionsSnapshot, bool)>>(solution.Projects.Sum(project => project.DocumentIds.Count));

            foreach (var project in solution.Projects)
            {
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

                fileCount += project.DocumentIds.Count;

                // Get project documents and options with .editorconfig settings applied.
                var getProjectDocuments = project.DocumentIds.Select(documentId => GetDocumentAndOptions(
                    project, documentId, filesToFormat, codingConventionsManager, optionsApplier, cancellationToken));
                getDocumentsAndOptions.AddRange(getProjectDocuments);
            }

            var documentsAndOptions = await Task.WhenAll(getDocumentsAndOptions).ConfigureAwait(false);
            var foundEditorConfig = documentsAndOptions.Any(documentAndOptions => documentAndOptions.Item4);

            var addedFilePaths = new HashSet<string>(documentsAndOptions.Length);
            var formattableFiles = ImmutableArray.CreateBuilder<(DocumentId, OptionSet, ICodingConventionsSnapshot)>(documentsAndOptions.Length);
            foreach (var (document, options, codingConventions, hasEditorConfig) in documentsAndOptions)
            {
                if (document is null)
                {
                    continue;
                }

                // If any code file has an .editorconfig, then we should ignore files without an .editorconfig entry.
                if (foundEditorConfig && !hasEditorConfig)
                {
                    continue;
                }

                // If we've already added this document, either via a link or multi-targeted framework, then ignore.
                if (addedFilePaths.Contains(document.FilePath))
                {
                    continue;
                }

                addedFilePaths.Add(document.FilePath);
                formattableFiles.Add((document.Id, options, codingConventions));
            }

            return (fileCount, formattableFiles.ToImmutableArray());
        }

        private static async    Task<(Document, OptionSet, ICodingConventionsSnapshot, bool)> GetDocumentAndOptions(
            Project project,
            DocumentId documentId,
            ImmutableHashSet<string> filesToFormat,
            ICodingConventionsManager codingConventionsManager,
            EditorConfigOptionsApplier optionsApplier,
            CancellationToken cancellationToken)
        {
            var document = project.Solution.GetDocument(documentId);

            // If a files list was passed in, then ignore files not present in the list.
            if (!filesToFormat.IsEmpty && !filesToFormat.Contains(document.FilePath))
            {
                return (null, null, null, false);
            }

            if (!document.SupportsSyntaxTree)
            {
                return (null, null, null, false);
            }

            // Ignore generated code files.
            if (await GeneratedCodeUtilities.IsGeneratedCodeAsync(document, cancellationToken).ConfigureAwait(false))
            {
                return (null, null, null, false);
            }

            var context = await codingConventionsManager.GetConventionContextAsync(
                document.FilePath, cancellationToken).ConfigureAwait(false);

            OptionSet options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            // Check whether an .editorconfig was found for this document.
            if (context?.CurrentConventions is null)
            {
                return (document, options, null, false);
            }

            options = optionsApplier.ApplyConventions(options, context.CurrentConventions, project.Language);
            return (document, options, context.CurrentConventions, true);
        }
    }
}
