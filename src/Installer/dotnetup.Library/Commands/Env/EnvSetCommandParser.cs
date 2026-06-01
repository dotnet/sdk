// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvSetCommandParser
{
    public static readonly Argument<PathPreference> ModeArgument = CreateModeArgument();

    private static Argument<PathPreference> CreateModeArgument()
    {
        var argument = new Argument<PathPreference>("mode")
        {
            HelpName = "MODE",
            Description = "The env mode to apply: 'none', 'shell', or 'all'.",
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = ParseMode,
        };
        argument.CompletionSources.Add(_ => ["none", "shell", "all"]);
        return argument;
    }

    private static PathPreference ParseMode(ArgumentResult result)
    {
        var token = result.Tokens.Count > 0 ? result.Tokens[0].Value : string.Empty;
        return token.ToLowerInvariant() switch
        {
            "none" => PathPreference.None,
            "shell" => PathPreference.Shell,
            "all" => PathPreference.All,
            _ => SetError(result, token),
        };
    }

    private static PathPreference SetError(ArgumentResult result, string token)
    {
        result.AddError($"Unknown env mode '{token}'. Expected one of: none, shell, all.");
        return PathPreference.None;
    }

    public static Command ConstructCommand()
    {
        Command command = new("set", "Apply (or re-sync) the configured env mode.");
        command.Arguments.Add(ModeArgument);
        command.Options.Add(CommonOptions.ShellOption);
        command.SetAction(parseResult => new EnvSetCommand(parseResult).Execute());
        return command;
    }
}
