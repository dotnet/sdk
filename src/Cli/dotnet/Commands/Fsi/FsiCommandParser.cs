// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static class FsiCommandParser
{
    private static readonly Command Command = SetAction(FsiCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction((parseResult) => FsiCommand.Run(parseResult.GetValue(FsiCommandDefinition.Arguments)));
        return command;
    }
}
