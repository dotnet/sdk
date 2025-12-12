// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal static class WorkloadSearchCommandParser
{
    public static readonly Argument<string> WorkloadIdStubArgument = WorkloadSearchCommandDefinition.WorkloadIdStubArgument;

    public static readonly Option<string> VersionOption = WorkloadSearchCommandDefinition.VersionOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = WorkloadSearchCommandDefinition.Create();

        command.SetAction((parseResult) => new WorkloadSearchCommand(parseResult).Execute());

        return command;
    }
}
