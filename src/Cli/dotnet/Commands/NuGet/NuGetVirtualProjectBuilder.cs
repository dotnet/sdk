// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;
using NuGet.CommandLine.XPlat;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

internal sealed class NuGetVirtualProjectBuilder : IVirtualProjectBuilder
{
    public static NuGetVirtualProjectBuilder Instance => field ??= new();

    private NuGetVirtualProjectBuilder() { }

    public bool IsValidEntryPointPath(string entryPointFilePath) => VirtualProjectBuilder.IsValidEntryPointPath(entryPointFilePath);

    public string GetVirtualProjectPath(string entryPointFilePath) => VirtualProjectBuilder.GetVirtualProjectPath(entryPointFilePath);

    public ProjectRootElement CreateProjectRootElement(string entryPointFilePath, ProjectCollection projectCollection)
    {
        if (!Path.IsPathFullyQualified(entryPointFilePath))
        {
            throw new ArgumentException($"'{entryPointFilePath}' is not a fully qualified path.", paramName: nameof(entryPointFilePath));
        }

        var builder = new VirtualProjectBuilder(entryPointFilePath, VirtualProjectBuildingCommand.TargetFramework);

        builder.CreateProjectInstance(
            projectCollection,
            ErrorReporters.IgnoringReporter,
            out _,
            out var projectRootElement,
            out _);

        return projectRootElement;
    }

    public void SaveProject(string entryPointFilePath, ProjectRootElement projectRootElement)
    {
        VirtualProjectPackageReflector.ReflectChangesToDirectives(projectRootElement, entryPointFilePath);
    }
}
