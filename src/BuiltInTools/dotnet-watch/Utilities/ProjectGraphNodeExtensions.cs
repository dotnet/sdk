// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Watch;

internal static class ProjectGraphNodeExtensions
{
    public static string GetDisplayName(this ProjectGraphNode projectNode)
        => $"{Path.GetFileNameWithoutExtension(projectNode.ProjectInstance.FullPath)} ({projectNode.GetTargetFramework()})";

    public static string GetTargetFramework(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue("TargetFramework");

    public static Version? GetTargetFrameworkVersion(this ProjectGraphNode projectNode)
        => EnvironmentVariableNames.TryParseTargetFrameworkVersion(projectNode.ProjectInstance.GetPropertyValue("TargetFrameworkVersion"));

    public static bool IsTargetFrameworkVersionOrNewer(this ProjectGraphNode projectNode, Version minVersion)
        => GetTargetFrameworkVersion(projectNode) is { } version && version >= minVersion;

    public static bool IsNetCoreApp(string identifier)
        => string.Equals(identifier, ".NETCoreApp", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetCoreApp(this ProjectGraphNode projectNode)
        => IsNetCoreApp(projectNode.ProjectInstance.GetPropertyValue("TargetFrameworkIdentifier"));

    public static bool IsNetCoreApp(this ProjectGraphNode projectNode, Version minVersion)
        => IsNetCoreApp(projectNode) && IsTargetFrameworkVersionOrNewer(projectNode, minVersion);

    public static IEnumerable<string> GetCapabilities(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetItems("ProjectCapability").Select(item => item.EvaluatedInclude);
}
