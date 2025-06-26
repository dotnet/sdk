// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;

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

    public static EvaluationResult? TryCreate(
        string rootProjectPath,
        IEnumerable<string> buildArguments,
        IReporter reporter,
        EnvironmentOptions environmentOptions,
        CancellationToken cancellationToken)
    {
        var globalOptions = CommandLineOptions.ParseBuildProperties(buildArguments)
            .ToDictionary(keySelector: arg => arg.key, elementSelector: arg => arg.value);

        globalOptions["DotNetWatchBuild"] = "true";

        // See https://github.com/dotnet/project-system/blob/main/docs/well-known-project-properties.md

        globalOptions["DesignTimeBuild"] = "true";
        globalOptions["SkipCompilerExecution"] = "true";
        globalOptions["ProvideCommandLineArgs"] = "true";

        var projectGraph = MSBuildFileSetFactory.TryLoadProjectGraph(
            rootProjectPath,
            globalOptions,
            reporter,
            projectGraphRequired: true,
            cancellationToken);

        if (projectGraph == null)
        {
            return null;
        }

        var rootNode = projectGraph.GraphRoots.Single();

        var buildOutputLogger = new BuildLogger()
        {
            Verbosity = LoggerVerbosity.Minimal
        };

        ILogger[] loggers;
        if (environmentOptions.TestFlags.HasFlag(TestFlags.RunningAsTest))
        {
            loggers =
            [
                new BinaryLogger()
                {
                    Verbosity = LoggerVerbosity.Minimal,
                    Parameters = "LogFile=" + Path.Combine(environmentOptions.TestOutput, "DotnetWatch.DesignTimeBuild.binlog")
                },
                buildOutputLogger
            ];
        }
        else
        {
            loggers = [buildOutputLogger];
        }

        if (!rootNode.ProjectInstance.Build(["Restore"], loggers))
        {
            reporter.Error($"Error(s) restoring project file '{Path.GetFileName(rootProjectPath)}'.");
            reporter.Output($"MSBuild output:");
            BuildOutput.ReportBuildOutput(reporter, buildOutputLogger.Messages, success: false, projectDisplay: null);
            return null;
        }

        var fileItems = new Dictionary<string, FileItem>();

        foreach (var project in projectGraph.ProjectNodesTopologicallySorted)
        {
            buildOutputLogger.Clear();

            var projectInstance = project.ProjectInstance;
            var customCollectWatchItems = project.GetStringListPropertyValue("CustomCollectWatchItems");

            var success = projectInstance.Build(["Compile", .. customCollectWatchItems], loggers);
            if (!success)
            {
                reporter.Error($"Error(s) building project file '{projectInstance.FullPath}'.");
                reporter.Output($"MSBuild output:");
                BuildOutput.ReportBuildOutput(reporter, buildOutputLogger.Messages, success: false, projectDisplay: null);
                return null;
            }

            var projectPath = projectInstance.FullPath;
            var projectDirectory = Path.GetDirectoryName(projectPath)!;

            // TODO: Compile and AdditionalItems should be provided by Roslyn
            var items = projectInstance.GetItems("Compile")
                .Concat(projectInstance.GetItems("AdditionalFiles"))
                .Concat(projectInstance.GetItems("Watch"));

            foreach (var item in items)
            {
                AddFile(item.EvaluatedInclude, staticWebAssetPath: null);
            }

            if (!environmentOptions.SuppressHandlingStaticContentFiles &&
                project.GetBooleanPropertyValue("UsingMicrosoftNETSdkRazor") &&
                project.GetBooleanPropertyValue("DotNetWatchContentFiles", defaultValue: true))
            {
                foreach (var item in projectInstance.GetItems("Content"))
                {
                    if (item.GetBooleanMetadataValue("Watch", defaultValue: true))
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

        reporter.Verbose($"Watching {fileItems.Count} file(s) for changes");
#if DEBUG
        foreach (var file in fileItems.Values)
        {
            reporter.Verbose(file.StaticWebAssetPath != null
                ? $"> {file.FilePath}{Path.PathSeparator}{file.StaticWebAssetPath}"
                : $"> {file.FilePath}");
        }
#endif
        return new EvaluationResult(fileItems, projectGraph);
    }
}
