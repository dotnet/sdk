// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandParser
{
    private static readonly Command Command = SetAction(AddCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == AddPackageCommandDefinition.Name).SetAction((parseResult) => new PackageAddCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == AddReferenceCommandDefinition.Name).SetAction((parseResult) => new ReferenceAddCommand(parseResult).Execute());
        return command;
    }
}
