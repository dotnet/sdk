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
}
