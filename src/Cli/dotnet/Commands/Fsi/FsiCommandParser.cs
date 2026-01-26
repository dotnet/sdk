// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static class FsiCommandParser
{
    private static readonly FsiCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static FsiCommandDefinition CreateCommand()
    {
        var command = new FsiCommandDefinition();
        command.SetAction(parseResult => FsiCommand.Run(parseResult.GetValue(command.Arguments) ?? []));
        return command;
    }
}
