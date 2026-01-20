// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run.Api;

internal sealed class RunApiCommandParser
{
    private static readonly RunApiCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static RunApiCommandDefinition CreateCommand()
    {
        var command = new RunApiCommandDefinition();
        command.SetAction(parseResult => new RunApiCommand(parseResult).Execute());
        return command;
    }
}
