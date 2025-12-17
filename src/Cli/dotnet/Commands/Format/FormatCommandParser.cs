// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Format;

internal static class FormatCommandParser
{
    private static readonly Command Command = SetAction(FormatCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction(parseResult => FormatCommand.Run(parseResult.GetValue(FormatCommandDefinition.Arguments)));
        return command;
    }
}
