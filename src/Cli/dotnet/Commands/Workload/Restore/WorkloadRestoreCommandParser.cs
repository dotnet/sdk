// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Restore;

internal static class WorkloadRestoreCommandParser
{
    public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new(CliStrings.SolutionOrProjectArgumentName)
    {
        Description = CliStrings.SolutionOrProjectArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("restore", CliCommandStrings.WorkloadRestoreCommandDescription);

        command.Arguments.Add(SlnOrProjectArgument);
        WorkloadInstallCommandParser.AddWorkloadInstallCommandOptions(command);

        command.SetAction((parseResult) => new WorkloadRestoreCommand(parseResult).Execute());

        return command;
    }
}
