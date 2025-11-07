// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static partial class PackCommandParser
{
    private static readonly Command Command = ConfigureCommand(CreateCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction(PackCommand.Run);
        return command;
    }
}
