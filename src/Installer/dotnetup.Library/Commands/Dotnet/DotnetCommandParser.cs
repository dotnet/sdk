// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet;

internal static class DotnetCommandParser
{
    internal static readonly Argument<string[]> ForwardedArguments = new("additional arguments")
    {
        Arity = ArgumentArity.ZeroOrMore,
        CaptureRemainingTokens = true,
        Hidden = true,
    };

    private static readonly Command s_dotnetCommand = ConstructCommand();

    public static Command GetCommand() => s_dotnetCommand;

    private static Command ConstructCommand()
    {
        var command = new Command("dotnet", Strings.DotnetCommandDescription)
        {
            TreatUnmatchedTokensAsErrors = false,
            Aliases = { "do" }
        };
        command.Arguments.Add(ForwardedArguments);

        command.SetAction(parseResult => new DotnetCommand(parseResult).Execute());

        return command;
    }
}
