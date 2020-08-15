// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

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
                var projectId = ProjectId.CreateNewId(debugName: folderPath);
                var projectFilePaths = filePaths.Where(path => path.EndsWith(FileExtension, StringComparison.InvariantCulture));

                var documents = projectFilePaths.Select(
                    path => DocumentInfo.Create(
                        DocumentId.CreateNewId(projectId, debugName: path),
                        name: Path.GetFileName(path),
                        loader: new FileTextLoader(path, DefaultEncoding),
                        filePath: path))
                    .ToImmutableArray();

                if (documents.IsDefaultOrEmpty)
                {
                    return null;
                }

                var editorConfigDocuments = editorConfigPaths.Select(
                    path => DocumentInfo.Create(
                        DocumentId.CreateNewId(projectId),
                        name: path,
                        loader: new FileTextLoader(path, Encoding.UTF8),
                        filePath: path))
                    .ToImmutableArray();

                return ProjectInfo.Create(
                    projectId,
                    version: default,
                    name: ProjectName,
                    assemblyName: folderPath,
                    Language,
                    filePath: folderPath,
                    documents: documents)
                    .WithAnalyzerConfigDocuments(editorConfigDocuments);
            }
        }
    }
}
