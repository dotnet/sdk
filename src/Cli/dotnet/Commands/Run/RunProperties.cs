// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal record RunProperties(string RunCommand, string? RunArguments, string? RunWorkingDirectory)
{
    internal static RunProperties FromProjectAndApplicationArguments(ProjectInstance project, string[] applicationArgs)
    {
        string runProgram = project.GetPropertyValue("RunCommand");
        string runArguments = project.GetPropertyValue("RunArguments");
        string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

        if (applicationArgs.Length != 0)
        {
            runArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(applicationArgs);
        }

        return new(runProgram, runArguments, runWorkingDirectory);
    }

    internal static RunProperties FromPropertyValues(
    string runCommand,
    string? runArguments,
    string? runWorkingDirectory,
    string? targetPath,
    string[] applicationArgs,
    bool fallbackToTargetPath)
    {
        string runProgram = runCommand;
        if (fallbackToTargetPath &&
            (string.IsNullOrEmpty(runProgram) || !File.Exists(runProgram)))
        {
            // Fallback to TargetPath if RunCommand is missing or invalid.
            runProgram = targetPath ?? string.Empty;
            return new(runProgram, null, null);
        }

        string? finalRunArguments = runArguments;
        if (applicationArgs.Length != 0)
        {
            finalRunArguments = (finalRunArguments ?? string.Empty) +
                " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(applicationArgs);
        }

        return new(runProgram, finalRunArguments, runWorkingDirectory);
    }
}
