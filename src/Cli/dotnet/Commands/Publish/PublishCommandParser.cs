// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Publish;

internal static class PublishCommandParser
{
    private static readonly PublishCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static PublishCommandDefinition CreateCommand()
    {
        var command = new PublishCommandDefinition();
        command.SetAction(PublishCommand.Run);
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RunTimesFromProjectFile);
        return command;
    }
}
