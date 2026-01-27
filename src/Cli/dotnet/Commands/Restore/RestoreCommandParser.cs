// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static class RestoreCommandParser
{
    private static readonly RestoreCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static RestoreCommandDefinition CreateCommand()
    {
        var command = new RestoreCommandDefinition();
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RuntimesFromProjectFile);
        command.SetAction(RestoreCommand.Run);
        return command;
    }
}
