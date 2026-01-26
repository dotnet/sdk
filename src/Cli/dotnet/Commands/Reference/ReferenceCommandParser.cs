// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class ReferenceCommandParser
{
    private static readonly Command Command = ConfigureCommand(ReferenceCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == ReferenceAddCommandDefinition.Name).SetAction((parseResult) => new ReferenceAddCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == ReferenceListCommandDefinition.Name).SetAction((parseResult) => new ReferenceListCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == ReferenceRemoveCommandDefinition.Name).SetAction((parseResult) => new ReferenceRemoveCommand(parseResult).Execute());

        return command;
    }
}
