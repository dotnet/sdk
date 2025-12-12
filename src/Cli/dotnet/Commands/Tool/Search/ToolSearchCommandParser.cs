// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal static class ToolSearchCommandParser
{
    public static readonly Argument<string> SearchTermArgument = ToolSearchCommandDefinition.SearchTermArgument;

    public static readonly Option<bool> DetailOption = ToolSearchCommandDefinition.DetailOption;

    public static readonly Option<string> SkipOption = ToolSearchCommandDefinition.SkipOption;

    public static readonly Option<string> TakeOption = ToolSearchCommandDefinition.TakeOption;

    public static readonly Option<bool> PrereleaseOption = ToolSearchCommandDefinition.PrereleaseOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolSearchCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolSearchCommand(parseResult).Execute());

        return command;
    }
}
