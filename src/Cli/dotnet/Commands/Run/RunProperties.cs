// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal sealed record RunProperties(
    string Command,
    string? Arguments,
    string? WorkingDirectory,
    string RuntimeIdentifier,
    string DefaultAppHostRuntimeIdentifier,
    string TargetFrameworkVersion)
{
    internal RunProperties(string command, string? arguments, string? workingDirectory)
        : this(command, arguments, workingDirectory, string.Empty, string.Empty, string.Empty)
    {
    }

    internal static RunProperties FromProject(ProjectInstance project)
    {
        var result = new RunProperties(
            Command: project.GetPropertyValue("RunCommand"),
            Arguments: project.GetPropertyValue("RunArguments"),
            WorkingDirectory: project.GetPropertyValue("RunWorkingDirectory"),
            RuntimeIdentifier: project.GetPropertyValue("RuntimeIdentifier"),
            DefaultAppHostRuntimeIdentifier: project.GetPropertyValue("DefaultAppHostRuntimeIdentifier"),
            TargetFrameworkVersion: project.GetPropertyValue("TargetFrameworkVersion"));

        if (string.IsNullOrEmpty(result.Command))
        {
            RunCommand.ThrowUnableToRunError(project);
        }

        return result;
    }

    internal RunProperties WithApplicationArguments(string[] applicationArgs)
    {
        if (applicationArgs.Length != 0)
        {
            return this with { Arguments = Arguments + " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(applicationArgs) };
        }

        return this;
    }

    internal static RunProperties GetPropsFromProject(ProjectInstance project)
    {
        var result = new RunProperties(
            Command: project.GetPropertyValue("RunCommand"),
            Arguments: project.GetPropertyValue("RunArguments"),
            WorkingDirectory: project.GetPropertyValue("RunWorkingDirectory"),
            RuntimeIdentifier: project.GetPropertyValue("RuntimeIdentifier"),
            DefaultAppHostRuntimeIdentifier: project.GetPropertyValue("DefaultAppHostRuntimeIdentifier"),
            TargetFrameworkVersion: project.GetPropertyValue("TargetFrameworkVersion"));

        if (string.IsNullOrEmpty(result.Command))
        {
            ThrowUnableToRunError(project);
        }

        return result;
    }

    internal static void ThrowUnableToRunError(ProjectInstance project)
    {
        string targetFrameworks = project.GetPropertyValue("TargetFrameworks");
        if (!string.IsNullOrEmpty(targetFrameworks))
        {
            string targetFramework = project.GetPropertyValue("TargetFramework");
            if (string.IsNullOrEmpty(targetFramework))
            {
                throw new GracefulException(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework");
            }
        }

        throw new GracefulException(
                string.Format(
                    CliCommandStrings.TestCommandExceptionUnableToRun,
                    project.GetPropertyValue(ProjectProperties.ProjectFullPath),
                    "dotnet test",
                    "OutputType",
                    project.GetPropertyValue("OutputType")));
    }
}
