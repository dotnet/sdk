// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Build;

internal static class BuildCommandParser
{
    private static readonly BuildCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static BuildCommandDefinition CreateCommand()
    {
        var command = new BuildCommandDefinition();
        command.SetAction(BuildCommand.Run);
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RunTimesFromProjectFile);
        return command;
    }
}
