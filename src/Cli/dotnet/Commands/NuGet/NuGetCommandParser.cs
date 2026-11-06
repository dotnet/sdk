// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

internal static class NuGetCommandParser
{
    public static void ConfigureCommand(Command command)
    {
        command.SetAction(NuGetCommand.Run);

        foreach (var subcommand in command.Subcommands)
        {
            ConfigureCommand(subcommand);
        }
    }
}
