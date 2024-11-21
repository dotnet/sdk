// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal sealed partial class FolderWorkspace : Workspace
    {
        private abstract class ProjectLoader
        {
            public abstract string Language { get; }
            public abstract string FileExtension { get; }
            public virtual string ProjectName => $"{Language}{FileExtension}proj";

            public virtual ProjectInfo? LoadProjectInfo(string folderPath, ImmutableArray<string> filePaths, ImmutableArray<string> editorConfigPaths)
            {
                var projectFilePaths = ImmutableArray.CreateBuilder<string>(filePaths.Length);
                for (var index = 0; index < filePaths.Length; index++)
                {
                    if (filePaths[index].EndsWith(FileExtension, StringComparison.InvariantCulture))
                    {
                        projectFilePaths.Add(filePaths[index]);
                    }
                }

                if (projectFilePaths.Count == 0)
                {
                    return null;
                }

                var projectId = ProjectId.CreateNewId(debugName: folderPath);

                return ProjectInfo.Create(
                    projectId,
                    version: default,
                    name: ProjectName,
                    assemblyName: folderPath,
                    Language,
                    filePath: folderPath,
                    documents: LoadDocuments(projectId, projectFilePaths.ToImmutable()))
                    .WithAnalyzerConfigDocuments(LoadDocuments(projectId, editorConfigPaths));

                static IEnumerable<DocumentInfo> LoadDocuments(ProjectId projectId, ImmutableArray<string> filePaths)
                {
                    var documents = new DocumentInfo[filePaths.Length];
                    for (var index = 0; index < filePaths.Length; index++)
                    {
                        documents[index] = DocumentInfo.Create(
                            DocumentId.CreateNewId(projectId, debugName: filePaths[index]),
                            name: filePaths[index],
                            loader: new FileTextLoader(filePaths[index], DefaultEncoding),
                            filePath: filePaths[index]);
                    }

                    return documents;
                }
            }
        }
    }
}
