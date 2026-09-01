// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Help;

internal static class HelpCommandParser
{
    public static void ConfigureCommand(HelpCommandDefinition command)
    {
        command.SetAction(HelpCommand.Run);
    }
}
