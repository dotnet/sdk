// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    /// <summary>
    /// Used to collect a set of files to watch.
    ///
    /// Invokes msbuild to evaluate <see cref="TargetName"/> on the root project, which traverses all project dependencies and collects
    /// items that are to be watched. The target can be customized by defining CustomCollectWatchItems target. This is currently done by Razor SDK
    /// to collect razor and cshtml files.
    /// Consider replacing with <see cref="Build.Graph.ProjectGraph"/> traversal (https://github.com/dotnet/sdk/issues/40214).
    /// </summary>
    internal class MSBuildFileSetFactory(
        string rootProjectFile,
        string? targetFramework,
        IEnumerable<string> buildArguments,
        ProcessRunner processRunner,
        ILogger logger,
        EnvironmentOptions environmentOptions)
    {
        private const string TargetName = "GenerateWatchList";
        private const string WatchTargetsFileName = "DotNetWatch.targets";

        public string RootProjectFile => rootProjectFile;

        private readonly ProjectGraphFactory _buildGraphFactory = new(
            [new ProjectRepresentation(rootProjectFile, entryPointFilePath: null)],
            targetFramework,
            buildProperties: BuildUtilities.ParseBuildProperties(buildArguments).ToImmutableDictionary(keySelector: arg => arg.key, elementSelector: arg => arg.value),
            logger);

        internal sealed class EvaluationResult(IReadOnlyDictionary<string, FileItem> files, LoadedProjectGraph? projectGraph)
        {
            public readonly IReadOnlyDictionary<string, FileItem> Files = files;
            public readonly LoadedProjectGraph? ProjectGraph = projectGraph;
        }

        // Virtual for testing.
        public virtual async ValueTask<EvaluationResult?> TryCreateAsync(bool? requireProjectGraph, CancellationToken cancellationToken)
        {
            var watchList = Path.GetTempFileName();
            try
            {
                var projectDir = Path.GetDirectoryName(rootProjectFile);
                var arguments = GetMSBuildArguments(watchList);
                var capturedOutput = new List<OutputLine>();

                var processSpec = new ProcessSpec
                {
                    Executable = environmentOptions.MuxerPath,
                    WorkingDirectory = projectDir,
                    IsUserApplication = false,
                    Arguments = arguments,
                    OnOutput = line =>
                    {
                        lock (capturedOutput)
                        {
                            capturedOutput.Add(line);
                        }
                    }
                };

                logger.LogDebug("Running MSBuild target '{TargetName}' on '{Path}'", TargetName, rootProjectFile);

                var exitCode = await processRunner.RunAsync(processSpec, logger, launchResult: null, cancellationToken);

                var success = exitCode == 0 && File.Exists(watchList);

                if (!success)
                {
                    logger.LogError("Error(s) finding watch items project file '{FileName}'.", Path.GetFileName(rootProjectFile));
                    logger.LogInformation("MSBuild output from target '{TargetName}':", TargetName);
                }

                BuildOutput.ReportBuildOutput(logger, capturedOutput, success);
                if (!success)
                {
                    return null;
                }

                using var watchFile = File.OpenRead(watchList);
                var result = await JsonSerializer.DeserializeAsync<MSBuildFileSetResult>(watchFile, cancellationToken: cancellationToken);
                Debug.Assert(result != null);

                var fileItems = new Dictionary<string, FileItem>();
                foreach (var (projectPath, projectItems) in result.Projects)
                {
                    foreach (var filePath in projectItems.Files)
                    {
                        AddFile(filePath, staticWebAssetPath: null);
                    }

                    foreach (var staticFile in projectItems.StaticFiles)
                    {
                        // that target adds items with "wwwroot/" prefix:
                        AddFile(staticFile.FilePath, staticFile.StaticWebAssetPath?["wwwroot/".Length..]);
                    }

                    void AddFile(string filePath, string? staticWebAssetPath)
                    {
                        if (!fileItems.TryGetValue(filePath, out var existingFile))
                        {
                            fileItems.Add(filePath, new FileItem
                            {
                                FilePath = filePath,
                                ContainingProjectPaths = [projectPath],
                                StaticWebAssetRelativeUrl = staticWebAssetPath,
                            });
                        }
                        else if (!existingFile.ContainingProjectPaths.Contains(projectPath))
                        {
                            // linked files might be included to multiple projects:
                            existingFile.ContainingProjectPaths.Add(projectPath);
                        }
                    }
                }

                BuildReporter.ReportWatchedFiles(logger, fileItems);
#if DEBUG
                Debug.Assert(fileItems.Values.All(f => Path.IsPathRooted(f.FilePath)), "All files should be rooted paths");
#endif

                // Load the project graph after the project has been restored:
                LoadedProjectGraph? projectGraph = null;
                if (requireProjectGraph != null)
                {
                    projectGraph = _buildGraphFactory.TryLoadProjectGraph(requireProjectGraph.Value, cancellationToken);
                    if (projectGraph == null && requireProjectGraph == true)
                    {
                        return null;
                    }
                }

                return new EvaluationResult(fileItems, projectGraph);
            }
            finally
            {
                File.Delete(watchList);
            }
        }

        private IReadOnlyList<string> GetMSBuildArguments(string watchListFilePath)
        {
            var watchTargetsFile = FindTargetsFile();

            var arguments = new List<string>
            {
                "msbuild",
                "/restore",
                "/nologo",
                "/v:m",
                rootProjectFile,
                "/t:" + TargetName
            };

            arguments.AddRange(buildArguments);

            // Set dotnet-watch reserved properties after the user specified propeties,
            // so that the former take precedence.

            if (environmentOptions.SuppressHandlingStaticWebAssets)
            {
                arguments.Add("/p:DotNetWatchContentFiles=false");
            }

            arguments.Add("/p:_DotNetWatchListFile=" + watchListFilePath);
            arguments.Add("/p:DotNetWatchBuild=true"); // extensibility point for users
            arguments.Add("/p:DesignTimeBuild=true"); // don't do expensive things
            arguments.Add("/p:CustomAfterMicrosoftCommonTargets=" + watchTargetsFile);
            arguments.Add("/p:CustomAfterMicrosoftCommonCrossTargetingTargets=" + watchTargetsFile);

            return arguments;
        }

        private static string FindTargetsFile()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(MSBuildFileSetFactory).Assembly.Location);
            Debug.Assert(assemblyDir != null);

            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "assets"),
                Path.Combine(assemblyDir, "assets"),
                AppContext.BaseDirectory,
                assemblyDir,
            };

            var targetPath = searchPaths.Select(p => Path.Combine(p, WatchTargetsFileName)).FirstOrDefault(File.Exists);
            return targetPath ?? throw new FileNotFoundException("Fatal error: could not find DotNetWatch.targets");
        }
    }
}
