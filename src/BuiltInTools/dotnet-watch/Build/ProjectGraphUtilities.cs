// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Watch;

internal static class ProjectGraphUtilities
{
    /// <summary>
    /// Tries to create a project graph by running the build evaluation phase on the <see cref="rootProjectFile"/>.
    /// </summary>
    public static ProjectGraph? TryLoadProjectGraph(
        string rootProjectFile,
        ImmutableDictionary<string, string> globalOptions,
        IReporter reporter,
        bool projectGraphRequired,
        CancellationToken cancellationToken)
    {
        var entryPoint = new ProjectGraphEntryPoint(rootProjectFile, globalOptions);
        try
        {
            // Create a new project collection that does not reuse element cache
            // to work around https://github.com/dotnet/msbuild/issues/12064:
            var collection = new ProjectCollection(
                globalProperties: globalOptions,
                loggers: [],
                remoteLoggers: [],
                ToolsetDefinitionLocations.Default,
                maxNodeCount: 1,
                onlyLogCriticalEvents: false,
                loadProjectsReadOnly: false,
                useAsynchronousLogging: false,
                reuseProjectRootElementCache: false);

            return new ProjectGraph([entryPoint], collection, projectInstanceFactory: null, cancellationToken);
        }
        catch (Exception e)
        {
            reporter.Verbose("Failed to load project graph.");

            if (e is AggregateException { InnerExceptions: var innerExceptions })
            {
                foreach (var inner in innerExceptions)
                {
                    Report(inner);
                }
            }
            else
            {
                Report(e);
            }

            void Report(Exception e)
            {
                if (projectGraphRequired)
                {
                    reporter.Error(e.Message);
                }
                else
                {
                    reporter.Warn(e.Message);
                }
            }
        }

        return null;
    }

    public static string GetDisplayName(this ProjectGraphNode projectNode)
        => $"{Path.GetFileNameWithoutExtension(projectNode.ProjectInstance.FullPath)} ({projectNode.GetTargetFramework()})";

    public static string GetTargetFramework(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue(PropertyNames.TargetFramework);

    public static IEnumerable<string> GetTargetFrameworks(this ProjectGraphNode projectNode)
        => projectNode.GetStringListPropertyValue(PropertyNames.TargetFrameworks);

    public static Version? GetTargetFrameworkVersion(this ProjectGraphNode projectNode)
        => EnvironmentVariableNames.TryParseTargetFrameworkVersion(projectNode.ProjectInstance.GetPropertyValue(PropertyNames.TargetFrameworkVersion));

    public static IEnumerable<string> GetWebAssemblyCapabilities(this ProjectGraphNode projectNode)
        => projectNode.GetStringListPropertyValue(PropertyNames.WebAssemblyHotReloadCapabilities);

    public static bool IsTargetFrameworkVersionOrNewer(this ProjectGraphNode projectNode, Version minVersion)
        => projectNode.GetTargetFrameworkVersion() is { } version && version >= minVersion;

    public static bool IsNetCoreApp(string identifier)
        => string.Equals(identifier, ".NETCoreApp", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetCoreApp(this ProjectGraphNode projectNode)
        => IsNetCoreApp(projectNode.ProjectInstance.GetPropertyValue(PropertyNames.TargetFrameworkIdentifier));

    public static bool IsNetCoreApp(this ProjectGraphNode projectNode, Version minVersion)
        => projectNode.IsNetCoreApp() && projectNode.IsTargetFrameworkVersionOrNewer(minVersion);

    public static bool IsWebApp(this ProjectGraphNode projectNode)
        => projectNode.GetCapabilities().Any(static value => value is "AspNetCore" or "WebAssembly");

    public static string? GetOutputDirectory(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue(PropertyNames.TargetPath) is { Length: >0 } path ? Path.GetDirectoryName(Path.Combine(projectNode.ProjectInstance.Directory, path)) : null;

    public static string GetAssemblyName(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue(PropertyNames.TargetName);

    public static string? GetIntermediateOutputDirectory(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue(PropertyNames.IntermediateOutputPath) is { Length: >0 } path ? Path.Combine(projectNode.ProjectInstance.Directory, path) : null;

    public static IEnumerable<string> GetCapabilities(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetItems(ItemNames.ProjectCapability).Select(item => item.EvaluatedInclude);

    public static bool IsAutoRestartEnabled(this ProjectGraphNode projectNode)
        => projectNode.GetBooleanPropertyValue(PropertyNames.HotReloadAutoRestart);

    public static bool AreDefaultItemsEnabled(this ProjectGraphNode projectNode)
        => projectNode.GetBooleanPropertyValue(PropertyNames.EnableDefaultItems);

    public static IEnumerable<string> GetDefaultItemExcludes(this ProjectGraphNode projectNode)
        => projectNode.GetStringListPropertyValue(PropertyNames.DefaultItemExcludes);

    public static IEnumerable<string> GetStringListPropertyValue(this ProjectGraphNode projectNode, string propertyName)
        => projectNode.ProjectInstance.GetStringListPropertyValue(propertyName);

    public static IEnumerable<string> GetStringListPropertyValue(this ProjectInstance project, string propertyName)
        => project.GetPropertyValue(propertyName).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static bool GetBooleanPropertyValue(this ProjectGraphNode projectNode, string propertyName, bool defaultValue = false)
        => GetBooleanPropertyValue(projectNode.ProjectInstance, propertyName, defaultValue);

    public static bool GetBooleanPropertyValue(this ProjectInstance project, string propertyName, bool defaultValue = false)
        => project.GetPropertyValue(propertyName) is { Length: >0 } value ? bool.TryParse(value, out var result) && result : defaultValue;

    public static bool GetBooleanMetadataValue(this ProjectItemInstance item, string metadataName, bool defaultValue = false)
        => item.GetMetadataValue(metadataName) is { Length: > 0 } value ? bool.TryParse(value, out var result) && result : defaultValue;

    public static IEnumerable<ProjectGraphNode> GetTransitivelyReferencingProjects(this IEnumerable<ProjectGraphNode> projects)
    {
        var visited = new HashSet<ProjectGraphNode>();
        var queue = new Queue<ProjectGraphNode>();
        foreach (var project in projects)
        {
            queue.Enqueue(project);
        }

        while (queue.Count > 0)
        {
            var project = queue.Dequeue();
            if (visited.Add(project))
            {
                foreach (var referencingProject in project.ReferencingProjects)
                {
                    queue.Enqueue(referencingProject);
                }
            }
        }

        return visited;
    }
}
