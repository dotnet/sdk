// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLI_AOT
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Execution;
#endif
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Contains the command and runtime context produced for launching an SDK project.
/// </summary>
/// <param name="Command">The executable command.</param>
/// <param name="Arguments">The command arguments.</param>
/// <param name="WorkingDirectory">The command working directory.</param>
/// <param name="RuntimeIdentifier">The selected runtime identifier.</param>
/// <param name="DefaultAppHostRuntimeIdentifier">The default apphost runtime identifier.</param>
/// <param name="TargetFrameworkVersion">The target framework version used for runtime-root selection.</param>
[method: JsonConstructor]
internal sealed record RunProperties(
    string Command,
    string? Arguments,
    string? WorkingDirectory,
    string RuntimeIdentifier,
    string DefaultAppHostRuntimeIdentifier,
    string TargetFrameworkVersion)
{
    /// <summary>
    /// Initializes launch properties without runtime-root selection metadata.
    /// </summary>
    /// <param name="command">The executable command.</param>
    /// <param name="arguments">The command arguments.</param>
    /// <param name="workingDirectory">The command working directory.</param>
    internal RunProperties(string command, string? arguments, string? workingDirectory)
        : this(command, arguments, workingDirectory, string.Empty, string.Empty, string.Empty)
    {
    }

#if !CLI_AOT
    /// <summary>
    /// Creates launch properties from an evaluated project when it supplies a run command.
    /// </summary>
    /// <param name="project">The evaluated project.</param>
    /// <param name="result">Receives the launch properties when available.</param>
    /// <returns><see langword="true"/> when the project supplies a run command; otherwise, <see langword="false"/>.</returns>
    internal static bool TryFromProject(ProjectInstance project, [NotNullWhen(returnValue: true)] out RunProperties? result)
    {
        result = new RunProperties(
            Command: project.GetPropertyValue("RunCommand"),
            Arguments: project.GetPropertyValue("RunArguments"),
            WorkingDirectory: project.GetPropertyValue("RunWorkingDirectory"),
            RuntimeIdentifier: project.GetPropertyValue("RuntimeIdentifier"),
            DefaultAppHostRuntimeIdentifier: project.GetPropertyValue("DefaultAppHostRuntimeIdentifier"),
            TargetFrameworkVersion: project.GetPropertyValue("TargetFrameworkVersion"));

        if (string.IsNullOrEmpty(result.Command))
        {
            result = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates launch properties from an evaluated project.
    /// </summary>
    /// <param name="project">The evaluated project.</param>
    /// <returns>The project launch properties.</returns>
    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    internal static RunProperties FromProject(ProjectInstance project)
    {
        if (!TryFromProject(project, out var result))
        {
            RunCommand.ThrowUnableToRunError(project);
        }

        return result;
    }
#endif

    /// <summary>
    /// Appends escaped application arguments to the cached command arguments.
    /// </summary>
    /// <param name="applicationArgs">The application arguments.</param>
    /// <returns>A copy containing the appended arguments.</returns>
    internal RunProperties WithApplicationArguments(string[] applicationArgs)
    {
        if (applicationArgs.Length != 0)
        {
            return this with
            {
                Arguments = CommonRunHelpers.CombineRunArguments(
                    Arguments,
                    applicationArgs,
                    launchProfileArguments: null,
                    appendApplicationArgumentsToBase: true),
            };
        }

        return this;
    }
}
