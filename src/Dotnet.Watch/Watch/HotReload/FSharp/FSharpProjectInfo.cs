// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using BuildLogger = Microsoft.Build.Framework.ILogger;

namespace Microsoft.DotNet.Watch;

internal sealed record FSharpProjectInfo(
    ProjectInstanceId ProjectId,
    string ProjectPath,
    string TargetFramework,
    string TargetPath,
    string DotnetFscCompilerPath,
    ImmutableArray<string> CommandLineArgs)
{
    public static ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo> Collect(ProjectGraph projectGraph, ILogger logger)
    {
        var trace = IsTraceEnabled();
        var builder = ImmutableDictionary.CreateBuilder<ProjectInstanceId, FSharpProjectInfo>();

        foreach (var node in projectGraph.ProjectNodes)
        {
            if (!IsFSharpProject(node))
            {
                continue;
            }

            var projectPath = node.ProjectInstance.FullPath;
            var targetFramework = node.ProjectInstance.GetTargetFramework();
            var targetPath = node.ProjectInstance.GetPropertyValue(PropertyNames.TargetPath);
            var dotnetFscCompilerPath = node.ProjectInstance.GetPropertyValue("DotnetFscCompilerPath");
            var commandLineArgs = GetCommandLineArgs(node, logger, trace);

            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(dotnetFscCompilerPath) || commandLineArgs.IsEmpty)
            {
                if (trace)
                {
                    logger.LogDebug(
                        "Skipping F# project '{ProjectPath}' for managed updates (TargetPath='{TargetPath}', DotnetFscCompilerPath='{CompilerPath}', ArgCount={ArgCount}).",
                        projectPath,
                        targetPath,
                        dotnetFscCompilerPath,
                        commandLineArgs.Length);
                }

                continue;
            }

            var projectId = new ProjectInstanceId(projectPath, targetFramework);
            var projectInfo = new FSharpProjectInfo(
                projectId,
                projectPath,
                targetFramework,
                NormalizeFullPath(targetPath),
                NormalizeFullPath(dotnetFscCompilerPath),
                commandLineArgs);

            if (trace)
            {
                logger.LogDebug(
                    "F# hot reload project discovered: '{ProjectPath}' ({TargetFramework}), compiler='{CompilerPath}', args={ArgCount}.",
                    projectInfo.ProjectPath,
                    projectInfo.TargetFramework,
                    projectInfo.DotnetFscCompilerPath,
                    projectInfo.CommandLineArgs.Length);
            }

            builder[projectId] = projectInfo;
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> GetCommandLineArgs(ProjectGraphNode node, ILogger logger, bool trace)
    {
        var commandLineArgs = node.ProjectInstance.GetItems("FscCommandLineArgs").Select(item => item.EvaluatedInclude).ToImmutableArray();
        if (!commandLineArgs.IsEmpty)
        {
            return commandLineArgs;
        }

        // CoreCompile is often skipped as up-to-date during design-time evaluation, which leaves
        // FscCommandLineArgs empty. Force a no-op compile pass to materialize captured arguments.
        var designTimeProject = node.ProjectInstance.DeepCopy();
        var forcedOutputPath = Path.Combine(
            Path.GetDirectoryName(designTimeProject.FullPath) ?? Directory.GetCurrentDirectory(),
            "obj",
            $".dotnet-watch-fsharp-force-{Guid.NewGuid():N}.tmp");
        designTimeProject.SetProperty("NonExistentFile", forcedOutputPath);

        var customCollectWatchItems = designTimeProject.GetStringListPropertyValue(PropertyNames.CustomCollectWatchItems);
        if (!designTimeProject.Build([TargetNames.Compile, .. customCollectWatchItems], Array.Empty<BuildLogger>()))
        {
            if (trace)
            {
                logger.LogDebug("F# design-time compile failed while collecting command-line arguments for '{ProjectPath}'.", designTimeProject.FullPath);
            }

            return [];
        }

        commandLineArgs = designTimeProject.GetItems("FscCommandLineArgs").Select(item => item.EvaluatedInclude).ToImmutableArray();
        if (trace)
        {
            logger.LogDebug(
                "F# command-line argument capture after forced compile for '{ProjectPath}': {ArgCount} argument(s).",
                designTimeProject.FullPath,
                commandLineArgs.Length);
        }

        return commandLineArgs;
    }

    private static bool IsFSharpProject(ProjectGraphNode node)
    {
        if (Path.GetExtension(node.ProjectInstance.FullPath).Equals(".fsproj", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var language = node.ProjectInstance.GetPropertyValue("Language");
        return string.Equals(language, "F#", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTraceEnabled()
    {
        var value = Environment.GetEnvironmentVariable("DOTNET_WATCH_TRACE_FSHARP_HOTRELOAD");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFullPath(string path)
    {
        var normalized = path.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = normalized[1..^1];
        }

        return Path.GetFullPath(normalized);
    }
}
