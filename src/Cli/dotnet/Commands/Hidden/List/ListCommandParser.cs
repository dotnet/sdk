// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List;

internal static class ListCommandParser
{
    private static readonly Command Command = SetAction(ListCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == ListPackageCommandDefinition.Name).SetAction((parseResult) => new PackageListCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == ListReferenceCommandDefinition.Name).SetAction((parseResult) => new ReferenceListCommand(parseResult).Execute());

        return command;
    }
}
