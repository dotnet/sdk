// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class RunCommandParser
{
    private static readonly RunCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static RunCommandDefinition CreateCommand()
    {
        var command = new RunCommandDefinition();
        command.SetAction(RunCommand.Run);
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RunTimesFromProjectFile);
        return command;
    }
}
