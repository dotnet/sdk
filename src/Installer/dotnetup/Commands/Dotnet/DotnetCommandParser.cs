// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet;

internal static class DotnetCommandParser
{
    private static readonly Command s_dotnetCommand = ConstructCommand();

    public static Command GetCommand() => s_dotnetCommand;

    private static Command ConstructCommand()
    {
        var command = new Command("dotnet", Strings.DotnetCommandDescription)
        {
            // No arguments or options defined — all tokens after the subcommand name
            // are captured via TreatUnmatchedTokensAsErrors = false and read from
            // ParseResult.UnmatchedTokens in DotnetCommand.
            TreatUnmatchedTokensAsErrors = false,
            Aliases = { "do" }
        };

        command.SetAction(parseResult => new DotnetCommand(parseResult).Execute());

        return command;
    }
}
