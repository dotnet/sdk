// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal static class WorkloadSearchVersionsCommandParser
{
    public static readonly Argument<IEnumerable<string>> WorkloadVersionArgument = WorkloadSearchVersionsCommandDefinition.WorkloadVersionArgument;

    public static readonly Option<int> TakeOption = WorkloadSearchVersionsCommandDefinition.TakeOption;

    public static readonly Option<string> FormatOption = WorkloadSearchVersionsCommandDefinition.FormatOption;

    public static readonly Option<bool> IncludePreviewsOption = WorkloadSearchVersionsCommandDefinition.IncludePreviewsOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = WorkloadSearchVersionsCommandDefinition.Create();

        command.SetAction(parseResult => new WorkloadSearchVersionsCommand(parseResult).Execute());

        return command;
    }
}
