// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Hidden.Parse;

internal static class ParseCommandParser
{
    public static void ConfigureCommand(ParseCommandDefinition command)
    {
        command.SetAction(ParseCommand.Run);
    }
}
