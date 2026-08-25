// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Complete;

internal static class CompleteCommandParser
{
    public static void ConfigureCommand(CompleteCommandDefinition command)
    {
#if CLI_AOT
        command.SetAction(parseResult =>
        {
            string input = parseResult.GetValue(command.PathArgument) ?? string.Empty;

            // Managed command parsers can add template, project, and package-backed providers to
            // command subtrees. Keep only root command/option labels in AOT; defer after a command
            // is selected so the managed CLI can supply every command-specific provider.
            if (!ReferenceEquals(Parser.Parse(input).CommandResult.Command, Parser.RootCommand))
            {
                throw new CommandNotAvailableInAotException();
            }

            return CompleteCommand.Run(parseResult);
        });
#else
        command.SetAction(CompleteCommand.Run);
#endif
    }
}
