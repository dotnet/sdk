// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Complete;

internal static class CompleteCommandParser
{
    public static void ConfigureCommand(CompleteCommandDefinition command)
    {
        command.SetAction(CompleteCommand.Run);
    }
}
