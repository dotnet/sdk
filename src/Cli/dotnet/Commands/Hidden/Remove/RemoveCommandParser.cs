// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove;

internal static class RemoveCommandParser
{
    public static void ConfigureCommand(RemoveCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.PackageCommand.SetAction((parseResult, cancellationToken) => Task.FromResult(new PackageRemoveCommand(parseResult).Execute(cancellationToken)));
        command.ReferenceCommand.SetAction((parseResult, cancellationToken) => Task.FromResult(new ReferenceRemoveCommand(parseResult).Execute(cancellationToken)));
    }
}
