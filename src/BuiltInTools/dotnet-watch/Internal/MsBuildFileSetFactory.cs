// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    /// <summary>
    /// Used to collect a set of files to watch.
    ///
    /// Invokes msbuild to evaluate <see cref="TargetName"/> on the root project, which traverses all project dependencies and collects
    /// items that are to be watched. The target can be customized by defining CustomCollectWatchItems target. This is currently done by Razor SDK
    /// to collect razor and cshtml files.
    ///
    /// Consider replacing with <see cref="Build.Graph.ProjectGraph"/> traversal (https://github.com/dotnet/sdk/issues/40214).
    /// </summary>
    internal class MSBuildFileSetFactory(
        string rootProjectFile,
        string? targetFramework,
        IReadOnlyList<(string, string)>? buildProperties,
        EnvironmentOptions environmentOptions,
        IReporter reporter,
        OutputSink? outputSink,
        bool trace)
    {
        private const string TargetName = "GenerateWatchList";
        private const string WatchTargetsFileName = "DotNetWatch.targets";

        private readonly OutputSink _outputSink = outputSink ?? new OutputSink();
        private readonly IReadOnlyList<string> _buildFlags = InitializeArgs(FindTargetsFile(), targetFramework, buildProperties, trace);

        public string RootProjectFile => rootProjectFile;

        // Virtual for testing.
        public virtual async ValueTask<EvaluationResult?> TryCreateAsync(CancellationToken cancellationToken)
        {
            var watchList = Path.GetTempFileName();
            try
            {
                var projectDir = Path.GetDirectoryName(rootProjectFile);

                var capture = _outputSink.StartCapture();
                var arguments = new List<string>
                {
                    "msbuild",
                    "/nologo",
                    rootProjectFile,
                    $"/p:_DotNetWatchListFile={watchList}",
                };

#if !DEBUG
                if (environmentOptions.TestFlags.HasFlag(TestFlags.RunningAsTest))
#endif
                {
                    arguments.Add("/bl");
                }

                if (environmentOptions.SuppressHandlingStaticContentFiles)
                {
                    arguments.Add("/p:DotNetWatchContentFiles=false");
                }

                arguments.AddRange(_buildFlags);

                var processSpec = new ProcessSpec
                {
                    Executable = environmentOptions.MuxerPath,
                    WorkingDirectory = projectDir,
                    Arguments = arguments,
                    OutputCapture = capture
                };

                reporter.Verbose($"Running MSBuild target '{TargetName}' on '{rootProjectFile}'");

                var exitCode = await ProcessRunner.RunAsync(processSpec, reporter, isUserApplication: false, processExitedSource: null, cancellationToken);

                if (exitCode != 0 || !File.Exists(watchList))
                {
                    reporter.Error($"Error(s) finding watch items project file '{Path.GetFileName(rootProjectFile)}'");

                    reporter.Output($"MSBuild output from target '{TargetName}':");
                    reporter.Output(string.Empty);

                    foreach (var line in capture.Lines)
                    {
                        reporter.Output($"   {line}");
                    }

                    reporter.Output(string.Empty);

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
                        AddFile(staticFile.FilePath, staticFile.StaticWebAssetPath);
                    }

                    void AddFile(string filePath, string? staticWebAssetPath)
                    {
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
                    reporter.Verbose($"  -> {file.FilePath} {file.StaticWebAssetPath}");
                }

                Debug.Assert(fileItems.Values.All(f => Path.IsPathRooted(f.FilePath)), "All files should be rooted paths");
#endif

                return new EvaluationResult(fileItems);
            }
            finally
            {
                File.Delete(watchList);
            }
        }

        private static IReadOnlyList<string> InitializeArgs(string watchTargetsFile, string? targetFramework, IReadOnlyList<(string name, string value)>? buildProperties, bool trace)
        {
            var args = new List<string>
            {
                "/nologo",
                "/v:n",
                "/t:" + TargetName,
                "/p:DotNetWatchBuild=true", // extensibility point for users
                "/p:DesignTimeBuild=true", // don't do expensive things
                "/p:CustomAfterMicrosoftCommonTargets=" + watchTargetsFile,
                "/p:CustomAfterMicrosoftCommonCrossTargetingTargets=" + watchTargetsFile,
            };

            if (targetFramework != null)
            {
                args.Add("/p:TargetFramework=" + targetFramework);
            }

            if (buildProperties != null)
            {
                args.AddRange(buildProperties.Select(p => $"/p:{p.name}={p.value}"));
            }

            if (trace)
            {
                // enables capturing markers to know which projects have been visited
                args.Add("/p:_DotNetWatchTraceOutput=true");
            }

            return args;
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
