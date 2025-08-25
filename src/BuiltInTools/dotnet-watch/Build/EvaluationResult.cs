// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class EvaluationResult(IReadOnlyDictionary<string, FileItem> files, ProjectGraph projectGraph)
{
    public readonly IReadOnlyDictionary<string, FileItem> Files = files;
    public readonly ProjectGraph ProjectGraph = projectGraph;

    public readonly FilePathExclusions ItemExclusions
        = projectGraph != null ? FilePathExclusions.Create(projectGraph) : FilePathExclusions.Empty;

    private readonly Lazy<IReadOnlySet<string>> _lazyBuildFiles
        = new(() => projectGraph != null ? CreateBuildFileSet(projectGraph) : new HashSet<string>());

    private static IReadOnlySet<string> CreateBuildFileSet(ProjectGraph projectGraph)
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

    /// <summary>
    /// Loads project graph and performs design-time build.
    /// </summary>
    public static EvaluationResult? TryCreate(
        string rootProjectPath,
        IEnumerable<string> buildArguments,
        ILogger logger,
        GlobalOptions options,
        EnvironmentOptions environmentOptions,
        bool restore,
        CancellationToken cancellationToken)
    {
        var buildReporter = new BuildReporter(logger, options, environmentOptions);

        // See https://github.com/dotnet/project-system/blob/main/docs/well-known-project-properties.md

        var globalOptions = CommandLineOptions.ParseBuildProperties(buildArguments)
            .ToImmutableDictionary(keySelector: arg => arg.key, elementSelector: arg => arg.value)
            .SetItem(PropertyNames.DotNetWatchBuild, "true")
            .SetItem(PropertyNames.DesignTimeBuild, "true")
            .SetItem(PropertyNames.SkipCompilerExecution, "true")
            .SetItem(PropertyNames.ProvideCommandLineArgs, "true")
            // F# targets depend on host path variable:
            .SetItem("DOTNET_HOST_PATH", environmentOptions.MuxerPath);

        var projectGraph = ProjectGraphUtilities.TryLoadProjectGraph(
            rootProjectPath,
            globalOptions,
            logger,
            projectGraphRequired: true,
            cancellationToken);

        if (projectGraph == null)
        {
            return null;
        }

        var rootNode = projectGraph.GraphRoots.Single();

        if (restore)
        {
            using (var loggers = buildReporter.GetLoggers(rootNode.ProjectInstance.FullPath, "Restore"))
            {
                if (!rootNode.ProjectInstance.Build([TargetNames.Restore], loggers))
                {
                    logger.LogError("Failed to restore project '{Path}'.", rootProjectPath);
                    loggers.ReportOutput();
                    return null;
                }
            }
        }

        var fileItems = new Dictionary<string, FileItem>();

        foreach (var project in projectGraph.ProjectNodesTopologicallySorted)
        {
            // Deep copy so that we can reuse the graph for building additional targets later on.
            // If we didn't copy the instance the targets might duplicate items that were already
            // populated by design-time build.
            var projectInstance = project.ProjectInstance.DeepCopy();

            // skip outer build project nodes:
            if (projectInstance.GetPropertyValue(PropertyNames.TargetFramework) == "")
            {
                continue;
            }

            var customCollectWatchItems = projectInstance.GetStringListPropertyValue(PropertyNames.CustomCollectWatchItems);

            using (var loggers = buildReporter.GetLoggers(projectInstance.FullPath, "DesignTimeBuild"))
            {
                if (!projectInstance.Build([TargetNames.Compile, .. customCollectWatchItems], loggers))
                {
                    logger.LogError("Failed to build project '{Path}'.", projectInstance.FullPath);
                    loggers.ReportOutput();
                    return null;
                }
            }

            var projectPath = projectInstance.FullPath;
            var projectDirectory = Path.GetDirectoryName(projectPath)!;

            // TODO: Compile and AdditionalItems should be provided by Roslyn
            var items = projectInstance.GetItems(ItemNames.Compile)
                .Concat(projectInstance.GetItems(ItemNames.AdditionalFiles))
                .Concat(projectInstance.GetItems(ItemNames.Watch));

            foreach (var item in items)
            {
                AddFile(item.EvaluatedInclude, staticWebAssetPath: null);
            }

            if (!environmentOptions.SuppressHandlingStaticContentFiles &&
                projectInstance.GetBooleanPropertyValue(PropertyNames.UsingMicrosoftNETSdkRazor) &&
                projectInstance.GetBooleanPropertyValue(PropertyNames.DotNetWatchContentFiles, defaultValue: true))
            {
                foreach (var item in projectInstance.GetItems(ItemNames.Content))
                {
                    if (item.GetBooleanMetadataValue(MetadataNames.Watch, defaultValue: true))
                    {
                        var relativeUrl = item.EvaluatedInclude.Replace('\\', '/');
                        if (relativeUrl.StartsWith("wwwroot/"))
                        {
                            AddFile(item.EvaluatedInclude, staticWebAssetPath: relativeUrl);
                        }
                    }
                }
            }

            void AddFile(string include, string? staticWebAssetPath)
            {
                var filePath = Path.GetFullPath(Path.Combine(projectDirectory, include));

                if (!fileItems.TryGetValue(filePath, out var existingFile))
                {
                    fileItems.Add(filePath, new FileItem
                    {
                        FilePath = filePath,
                        ContainingProjectPaths = [projectPath],
                        StaticWebAssetPath = staticWebAssetPath,
                    });
                }
                else if (!existingFile.ContainingProjectPaths.Contains(projectPath))
                {
                    // linked files might be included to multiple projects:
                    existingFile.ContainingProjectPaths.Add(projectPath);
                }
            }
        }

        buildReporter.ReportWatchedFiles(fileItems);

        return new EvaluationResult(fileItems, projectGraph);
    }
}
