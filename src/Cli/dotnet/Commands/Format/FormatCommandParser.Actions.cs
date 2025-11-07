// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Format;

internal static partial class FormatCommandParser
{
    private static readonly Command Command = ConfigureCommand(CreateCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction((parseResult) => FormatCommand.Run(parseResult.GetValue(Arguments)));
        return command;
    }
}
