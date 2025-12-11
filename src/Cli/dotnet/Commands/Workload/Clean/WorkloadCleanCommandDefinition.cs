// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal static class WorkloadCleanCommandDefinition
{
    public static readonly Option<bool> CleanAllOption = new("--all") { Description = CliCommandStrings.CleanAllOptionDescription };

    public static Command Create()
    {
        Command command = new("clean", CliCommandStrings.WorkloadCleanCommandDescription);

        command.Options.Add(CleanAllOption);

        return command;
    }
}
