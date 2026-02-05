// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List;

internal static class ListCommandParser
{
    private static readonly ListCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static ListCommandDefinition CreateCommand()
    {
        var command = new ListCommandDefinition();
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.PackageCommand.SetAction(parseResult => new PackageListCommand(parseResult).Execute());
        command.ReferenceCommand.SetAction(parseResult => new ReferenceListCommand(parseResult).Execute());

        return command;
    }
}
