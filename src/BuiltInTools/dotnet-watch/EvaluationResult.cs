// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal sealed class EvaluationResult(IReadOnlyDictionary<string, FileItem> files, ProjectGraph? projectGraph)
{
    public readonly IReadOnlyDictionary<string, FileItem> Files = files;
    public readonly ProjectGraph? ProjectGraph = projectGraph;

    public readonly FilePathExclusions ItemExclusions
        = projectGraph != null ? FilePathExclusions.Create(projectGraph) : FilePathExclusions.Empty;

    private readonly Lazy<IReadOnlySet<string>> _lazyBuildFiles
        = new(() => projectGraph != null ? CreateBuildFileSet(projectGraph) : new HashSet<string>());

    public static IReadOnlySet<string> CreateBuildFileSet(ProjectGraph projectGraph)
        => projectGraph.ProjectNodes.SelectMany(p => p.ProjectInstance.ImportPaths)
            .Concat(projectGraph.ProjectNodes.Select(p => p.ProjectInstance.FullPath))
            .ToHashSet(PathUtilities.OSSpecificPathComparer);

    public IReadOnlySet<string> BuildFiles
        => _lazyBuildFiles.Value;

    public void WatchFiles(FileWatcher fileWatcher)
    {
        fileWatcher.WatchContainingDirectories(Files.Keys, includeSubdirectories: true);
        fileWatcher.WatchFiles(BuildFiles);
    }
}
