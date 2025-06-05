// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal static class WorkloadSearchCommandParser
{
    public static readonly Argument<string> WorkloadIdStubArgument =
        new(CliCommandStrings.WorkloadIdStubArgumentName)
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = CliCommandStrings.WorkloadIdStubArgumentDescription
        };

    public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("search", CliCommandStrings.WorkloadSearchCommandDescription);
        command.Subcommands.Add(WorkloadSearchVersionsCommandParser.GetCommand());
        command.Arguments.Add(WorkloadIdStubArgument);
        command.Options.Add(CommonOptions.HiddenVerbosityOption);
        command.Options.Add(VersionOption);

        command.SetAction((parseResult) => new WorkloadSearchCommand(parseResult).Execute());

        return command;
    }
}
