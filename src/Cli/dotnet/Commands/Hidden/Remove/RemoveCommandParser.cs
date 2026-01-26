// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove;

internal static class RemoveCommandParser
{
    private static readonly RemoveCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static RemoveCommandDefinition CreateCommand()
    {
        var command = new RemoveCommandDefinition();
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.PackageCommand.SetAction(parseResult => new PackageRemoveCommand(parseResult).Execute());
        command.ReferenceCommand.SetAction(parseResult => new ReferenceRemoveCommand(parseResult).Execute());

        return command;
    }
}
