// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove;

internal static class RemoveCommandParser
{
    private static readonly Command Command = ConfigureCommand(new RemoveCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(RemoveCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());

        def.PackageCommand.SetAction(parseResult => new PackageRemoveCommand(parseResult).Execute());
        def.ReferenceCommand.SetAction(parseResult => new ReferenceRemoveCommand(parseResult).Execute());

        return def;
    }
}
