// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static partial class RestoreCommandParser
{
    private static readonly Command Command = ConfigureCommand(CreateCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction(RestoreCommand.Run);
        return command;
    }
}
