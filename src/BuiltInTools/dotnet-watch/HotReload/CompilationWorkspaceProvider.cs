// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal static class CompilationWorkspaceProvider
    {
        public static Task<(Solution, IEditAndContinueWorkspaceService)> CreateWorkspaceAsync(string projectPath, IReporter reporter, CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<(Solution, IEditAndContinueWorkspaceService)>(TaskCreationOptions.RunContinuationsAsynchronously);
            CreateProject(taskCompletionSource, projectPath, reporter, cancellationToken);

            return taskCompletionSource.Task;
        }

        static async void CreateProject(TaskCompletionSource<(Solution, IEditAndContinueWorkspaceService)> taskCompletionSource, string projectPath, IReporter reporter, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();

            workspace.WorkspaceFailed += (_sender, diag) =>
            {
                if (diag.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
                {
                    reporter.Verbose($"MSBuildWorkspace warning: {diag.Diagnostic}");
                }
                else
                {
                    taskCompletionSource.TrySetException(new InvalidOperationException($"Failed to create MSBuildWorkspace: {diag.Diagnostic}"));
                }
            };

            await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
            var currentSolution = workspace.CurrentSolution;
            var editAndContinue = workspace.Services.GetRequiredService<IEditAndContinueWorkspaceService>();
            editAndContinue.StartDebuggingSession(workspace.CurrentSolution);

            // Read the documents to memory
            await Task.WhenAll(
                currentSolution.Projects.SelectMany(p => p.Documents.Concat(p.AdditionalDocuments)).Select(d => d.GetTextAsync(cancellationToken)));

            // Tell EnC to grab an initial snapshot of documents.
            foreach (var project in currentSolution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    await editAndContinue.OnSourceFileUpdatedAsync(document);
                }
            }

            // Warm up the compilation. This would help make the deltas for first edit appear much more quickly
            foreach (var project in currentSolution.Projects)
            {
                await project.GetCompilationAsync(cancellationToken);
            }

            taskCompletionSource.TrySetResult((currentSolution, editAndContinue));
        }
    }
}
