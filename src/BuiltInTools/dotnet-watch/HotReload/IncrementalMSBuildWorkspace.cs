// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Tools.Internal;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.Watcher.Tools;

internal class IncrementalMSBuildWorkspace : Workspace
{
    private readonly IReporter _reporter;

    public IncrementalMSBuildWorkspace(IReporter reporter)
        : base(MSBuildMefHostServices.DefaultServices, WorkspaceKind.MSBuild)
    {
        WorkspaceFailed += (_sender, diag) =>
        {
            // Errors reported here are not fatal, an exception would be thrown for fatal issues.
            reporter.Verbose($"MSBuildWorkspace warning: {diag.Diagnostic}");
        };

        _reporter = reporter;
    }

    public async Task UpdateProjectConeAsync(string rootProjectPath, CancellationToken cancellationToken)
    {
        var oldSolution = CurrentSolution;

        var loader = new MSBuildProjectLoader(this);
        var projectMap = ProjectMap.Create();
        var projectInfos = await loader.LoadProjectInfoAsync(rootProjectPath, projectMap, progress: null, msbuildLogger: null, cancellationToken).ConfigureAwait(false);

        var oldProjectIdsByPath = oldSolution.Projects.ToDictionary(keySelector: static p => p.FilePath!, elementSelector: static p => p.Id);

        // Map new project id to the corresponding old one based on file path, if it exists, and null for added projects.
        // Deleted projects won't be included in this map.
        var projectIdMap = projectInfos.ToDictionary(
            keySelector: static info => info.Id,
            elementSelector: info => oldProjectIdsByPath.TryGetValue(info.FilePath!, out var oldProjectId) ? oldProjectId : null);

        var newSolution = oldSolution;

        foreach (var newProjectInfo in projectInfos)
        {
            Debug.Assert(newProjectInfo.FilePath != null);

            var oldProjectId = projectIdMap[newProjectInfo.Id];
            if (oldProjectId == null)
            {
                newSolution = newSolution.AddProject(newProjectInfo);
                continue;
            }

            newSolution = WatchHotReloadService.WithProjectInfo(newSolution, ProjectInfo.Create(
                oldProjectId,
                newProjectInfo.Version,
                newProjectInfo.Name,
                newProjectInfo.AssemblyName,
                newProjectInfo.Language,
                newProjectInfo.FilePath,
                newProjectInfo.OutputFilePath,
                newProjectInfo.CompilationOptions,
                newProjectInfo.ParseOptions,
                MapDocuments(oldProjectId, newProjectInfo.Documents),
                newProjectInfo.ProjectReferences.Select(MapProjectReference),
                newProjectInfo.MetadataReferences,
                newProjectInfo.AnalyzerReferences,
                MapDocuments(oldProjectId, newProjectInfo.AdditionalDocuments),
                isSubmission: false,
                hostObjectType: null,
                outputRefFilePath: newProjectInfo.OutputRefFilePath)
                .WithAnalyzerConfigDocuments(MapDocuments(oldProjectId, newProjectInfo.AnalyzerConfigDocuments))
                .WithCompilationOutputInfo(newProjectInfo.CompilationOutputInfo));
        }

        await ReportSolutionFilesAsync(SetCurrentSolution(newSolution), cancellationToken);
        UpdateReferencesAfterAdd();

        ProjectReference MapProjectReference(ProjectReference pr)
            // Only C# and VB projects are loaded by the MSBuildProjectLoader, so some references might be missing:
            => new(projectIdMap.TryGetValue(pr.ProjectId, out var mappedId) ? mappedId : pr.ProjectId, pr.Aliases, pr.EmbedInteropTypes);

        ImmutableArray<DocumentInfo> MapDocuments(ProjectId mappedProjectId, IReadOnlyList<DocumentInfo> documents)
            => documents.Select(docInfo =>
            {
                // TODO: can there be multiple documents of the same path in the project?

                // Map to a document of the same path. If there isn't one (a new document is added to the project),
                // create a new document id with the mapped project id.
                var mappedDocumentId = oldSolution.GetDocumentIdsWithFilePath(docInfo.FilePath).FirstOrDefault(id => id.ProjectId == mappedProjectId)
                    ?? DocumentId.CreateNewId(mappedProjectId);

                return docInfo.WithId(mappedDocumentId);
            }).ToImmutableArray();
    }

    public async ValueTask UpdateFileContentAsync(IEnumerable<FileItem> changedFiles, CancellationToken cancellationToken)
    {
        var updatedSolution = CurrentSolution;

        foreach (var changedFile in changedFiles)
        {
            var documentIds = updatedSolution.GetDocumentIdsWithFilePath(changedFile.FilePath);
            foreach (var documentId in documentIds)
            {
                var textDocument = updatedSolution.GetDocument(documentId)
                    ?? updatedSolution.GetAdditionalDocument(documentId)
                    ?? updatedSolution.GetAnalyzerConfigDocument(documentId);

                if (textDocument == null)
                {
                    _reporter.Verbose($"Could not find document with path '{changedFile.FilePath}' in the workspace.");
                    continue;
                }

                var project = updatedSolution.GetProject(documentId.ProjectId);
                Debug.Assert(project?.FilePath != null);

                var sourceText = await GetSourceTextAsync(changedFile.FilePath, cancellationToken);

                updatedSolution = textDocument switch
                {
                    Document document => document.WithText(sourceText).Project.Solution,
                    AdditionalDocument ad => updatedSolution.WithAdditionalDocumentText(textDocument.Id, sourceText, PreservationMode.PreserveValue),
                    AnalyzerConfigDocument acd => updatedSolution.WithAnalyzerConfigDocumentText(textDocument.Id, sourceText, PreservationMode.PreserveValue),
                    _ => throw new InvalidOperationException()
                };
            }
        }

        await ReportSolutionFilesAsync(SetCurrentSolution(updatedSolution), cancellationToken);
    }

    private static async ValueTask<SourceText> GetSourceTextAsync(string filePath, CancellationToken cancellationToken)
    {
        var zeroLengthRetryPerformed = false;
        for (var attemptIndex = 0; attemptIndex < 6; attemptIndex++)
        {
            try
            {
                // File.OpenRead opens the file with FileShare.Read. This may prevent IDEs from saving file
                // contents to disk
                SourceText sourceText;
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    sourceText = SourceText.From(stream, Encoding.UTF8);
                }

                if (!zeroLengthRetryPerformed && sourceText.Length == 0)
                {
                    zeroLengthRetryPerformed = true;

                    // VSCode (on Windows) will sometimes perform two separate writes when updating a file on disk.
                    // In the first update, it clears the file contents, and in the second, it writes the intended
                    // content.
                    // It's atypical that a file being watched for hot reload would be empty. We'll use this as a
                    // hueristic to identify this case and perform an additional retry reading the file after a delay.
                    await Task.Delay(20, cancellationToken);

                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    sourceText = SourceText.From(stream, Encoding.UTF8);
                }

                return sourceText;
            }
            catch (IOException) when (attemptIndex < 5)
            {
                await Task.Delay(20 * (attemptIndex + 1), cancellationToken);
            }
        }

        Debug.Fail("This shouldn't happen.");
        return null;
    }

    public async Task ReportSolutionFilesAsync(Solution solution, CancellationToken cancellationToken)
    {
        _reporter.Verbose($"Solution: {solution.FilePath}");
        foreach (var project in solution.Projects)
        {
            _reporter.Verbose($"  Project: {project.FilePath}");

            foreach (var document in project.Documents)
            {
                await InspectDocumentAsync(document, "Document");
            }

            foreach (var document in project.AdditionalDocuments)
            {
                await InspectDocumentAsync(document, "Additional");
            }

            foreach (var document in project.AnalyzerConfigDocuments)
            {
                await InspectDocumentAsync(document, "Config");
            }
        }

        async ValueTask InspectDocumentAsync(TextDocument document, string kind)
        {
            var text = await document.GetTextAsync(cancellationToken);
            _reporter.Verbose($"    {kind}: {document.FilePath} [{Convert.ToBase64String(text.GetChecksum().ToArray())}]");
        }
    }
}
