﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal static class WorkloadCleanCommandParser
{
    public static readonly CliOption<bool> CleanAllOption = new("--all") { Description = CliCommandStrings.CleanAllOptionDescription };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("clean", CliCommandStrings.WorkloadCleanCommandDescription);

        command.Options.Add(CleanAllOption);

        command.SetAction((parseResult) => new WorkloadCleanCommand(parseResult).Execute());

        return command;
    }
}
