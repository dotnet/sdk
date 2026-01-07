// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove.Reference;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove;

internal static class RemoveCommandParser
{
    private static readonly Command Command = ConfigureCommand(RemoveCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == RemovePackageCommandDefinition.Name).SetAction((parseResult) => new PackageRemoveCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == RemoveReferenceCommandDefinition.Name).SetAction((parseResult) => new ReferenceRemoveCommand(parseResult).Execute());

        return command;
    }
}
