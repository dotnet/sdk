// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal sealed partial class FolderWorkspace : Workspace
    {
        private static class FolderSolutionLoader
        {
            private static ImmutableArray<ProjectLoader> ProjectLoaders
                => ImmutableArray.Create<ProjectLoader>(new CSharpProjectLoader(), new VisualBasicProjectLoader());

            public static async Task<SolutionInfo> LoadSolutionInfoAsync(string folderPath, ImmutableHashSet<string> pathsToInclude, CancellationToken cancellationToken)
            {
                var absoluteFolderPath = Path.IsPathFullyQualified(folderPath)
                    ? folderPath
                    : Path.GetFullPath(folderPath, Directory.GetCurrentDirectory());

                var projectInfos = ImmutableArray.CreateBuilder<ProjectInfo>(ProjectLoaders.Length);

                // Create projects for each of the supported languages.
                foreach (var loader in ProjectLoaders)
                {
                    var projectInfo = await loader.LoadProjectInfoAsync(folderPath, pathsToInclude, cancellationToken);
                    if (projectInfo is null)
                    {
                        continue;
                    }

                    projectInfos.Add(projectInfo);
                }

                // Construct workspace from loaded project infos.
                return SolutionInfo.Create(
                    SolutionId.CreateNewId(debugName: absoluteFolderPath),
                    version: default,
                    absoluteFolderPath,
                    projectInfos);
            }
        }
    }
}
