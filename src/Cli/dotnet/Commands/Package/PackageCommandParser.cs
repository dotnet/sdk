// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Package.Search;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Package;

internal class PackageCommandParser
{
    private static readonly Command Command = SetAction(PackageCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == PackageRemoveCommandDefinition.Name).SetAction(parseResult => new PackageRemoveCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == PackageListCommandDefinition.Name).SetAction(parseResult => new PackageListCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == PackageAddCommandDefinition.Name).SetAction(parseResult => new PackageAddCommand(parseResult).Execute());

        command.Subcommands.Single(c => c.Name == PackageSearchCommandDefinition.Name).SetAction((parseResult) =>
        {
            var command = new PackageSearchCommand(parseResult);
            int exitCode = command.Execute();

            if (exitCode == 1)
            {
                parseResult.ShowHelp();
            }
            // Only return 1 or 0
            return exitCode == 0 ? 0 : 1;
        });

        return command;
    }
}
