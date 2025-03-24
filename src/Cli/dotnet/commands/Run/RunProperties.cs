// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal record RunProperties(string? RunCommand, string? RunArguments, string? RunWorkingDirectory)
{
    internal static RunProperties FromProjectAndApplicationArguments(ProjectInstance project, string[] applicationArgs, bool fallbackToTargetPath)
    {
        string runProgram = project.GetPropertyValue("RunCommand");
        if (fallbackToTargetPath &&
            (string.IsNullOrEmpty(runProgram) || !File.Exists(runProgram)))
        {
            // If we can't find the executable that runCommand is pointing to, we simply use TargetPath instead.
            // In this case, we discard everything related to "Run" (i.e, RunWorkingDirectory and RunArguments) and use only TargetPath
            runProgram = project.GetPropertyValue("TargetPath");
            return new(runProgram, null, null);
        }

        string runArguments = project.GetPropertyValue("RunArguments");
        string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

        if (applicationArgs.Length != 0)
        {
            runArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(applicationArgs);
        }

        return new(runProgram, runArguments, runWorkingDirectory);
    }
}
