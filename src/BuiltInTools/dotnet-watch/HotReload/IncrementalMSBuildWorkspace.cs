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

namespace Microsoft.DotNet.Watcher.Tools;

internal class IncrementalMSBuildWorkspace : Workspace
{
    public IncrementalMSBuildWorkspace(IReporter reporter)
        : base(MSBuildMefHostServices.DefaultServices, WorkspaceKind.MSBuild)
    {
        WorkspaceFailed += (_sender, diag) =>
        {
            // Errors reported here are not fatal, an exception would be thrown for fatal issues.
            reporter.Verbose($"MSBuildWorkspace warning: {diag.Diagnostic}");
        };
    }

    internal async Task OpenProjectAsync(string rootProjectPath, CancellationToken cancellationToken)
    {
        var oldSolution = CurrentSolution;

        var loader = new MSBuildProjectLoader(this);
        var projectMap = ProjectMap.Create(oldSolution);
        var projectInfos = await loader.LoadProjectInfoAsync(rootProjectPath, projectMap, progress: null, msbuildLogger: null, cancellationToken).ConfigureAwait(false);

        var newSolution = oldSolution;

        foreach (var projectInfo in projectInfos)
        {
            var projectId = projectInfo.Id;
            if (oldSolution.GetProject(projectId) is { } oldProject)
            {
                newSolution = newSolution
                    .WithProjectAnalyzerReferences(projectId, projectInfo.AnalyzerReferences)
                    .WithProjectAssemblyName(projectId, projectInfo.AssemblyName)
                    .WithProjectCompilationOptions(projectId, projectInfo.CompilationOptions!)
                    .WithProjectCompilationOutputInfo(projectId, projectInfo.CompilationOutputInfo)
                    .WithProjectDefaultNamespace(projectId, projectInfo);
                   
                var x = oldProject.WithAnalyzerReferences(projectInfo.AnalyzerReferences);

                // LoadProjectInfoAsync maps projects to the existing ones but not documents.

                OnAddedDocuments(oldProject, projectInfo.Documents, OnDocumentAdded);
                OnAddedDocuments(oldProject, projectInfo.AdditionalDocuments, OnAdditionalDocumentAdded);
                OnAddedDocuments(oldProject, projectInfo.AnalyzerConfigDocuments, OnAnalyzerConfigDocumentAdded);

                OnRemovedDocuments(oldProject, projectInfo.Documents, OnDocumentRemoved);
                OnRemovedDocuments(oldProject, projectInfo.AdditionalDocuments, OnAdditionalDocumentRemoved);
                OnRemovedDocuments(oldProject, projectInfo.AnalyzerConfigDocuments, OnAnalyzerConfigDocumentRemoved);

                static void OnAddedDocuments(Project oldProject, IReadOnlyList<DocumentInfo> newDocumentInfos, Action<DocumentInfo> action)
                {
                    foreach (var newDocumentInfo in newDocumentInfos)
                    {
                        Debug.Assert(newDocumentInfo.FilePath != null);
                        if (!oldProject.Solution.GetDocumentIdsWithFilePath(newDocumentInfo.FilePath).Any(d => d.ProjectId == oldProject.Id))
                        {
                            action(newDocumentInfo);
                        }
                    }
                }

                static void OnRemovedDocuments(Project oldProject, IReadOnlyList<DocumentInfo> newDocumentInfos, Action<DocumentId> action)
                {
                    foreach (var oldDocument in oldProject.Documents)
                    {
                        Debug.Assert(oldDocument.FilePath != null);
                        if (!newDocumentInfos.Any(info => info.FilePath == oldDocument.FilePath))
                        {
                            action(oldDocument.Id);
                        }
                    }
                }
            }
            else
            {
                newSolution = newSolution.AddProject(projectInfo);
            }
        }

        UpdateReferencesAfterAdd();
    }
}
