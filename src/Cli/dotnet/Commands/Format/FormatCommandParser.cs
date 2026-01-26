// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Format;

internal static class FormatCommandParser
{
    private static readonly FormatCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static FormatCommandDefinition CreateCommand()
    {
        var command = new FormatCommandDefinition();
        command.SetAction(parseResult => FormatCommand.Run(parseResult.GetValue(command.Arguments) ?? []));
        return command;
    }
}
