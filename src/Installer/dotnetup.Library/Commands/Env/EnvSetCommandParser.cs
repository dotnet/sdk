// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvSetCommandParser
{
    private static readonly string[] s_supportedModes = OperatingSystem.IsWindows()
        ? ["none", "shell", "all"]
        : ["none", "shell"];

    public static readonly Argument<PathPreference> ModeArgument = CreateModeArgument();

    private static Argument<PathPreference> CreateModeArgument()
    {
        var argument = new Argument<PathPreference>("mode")
        {
            HelpName = "MODE",
            Description = $"The env mode to apply: {string.Join(", ", s_supportedModes.Select(m => $"'{m}'"))}.",
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = ParseMode,
        };
        argument.CompletionSources.Add(_ => s_supportedModes);
        return argument;
    }

    private static PathPreference ParseMode(ArgumentResult result)
    {
        var token = result.Tokens.Count > 0 ? result.Tokens[0].Value : string.Empty;
        return token.ToLowerInvariant() switch
        {
            "none" => PathPreference.None,
            "shell" => PathPreference.Shell,
            // 'all' is Windows-only. On other platforms reject at parse time with a
            // clearer error than the runtime throw inside EnvSetCommand (which is kept
            // as defense-in-depth in case this parse check is ever bypassed).
            "all" when OperatingSystem.IsWindows() => PathPreference.All,
            "all" => SetError(result, "'all' mode is only supported on Windows. Use 'shell' on this platform."),
            _ => SetError(result, $"Unknown env mode '{token}'. Expected one of: {string.Join(", ", s_supportedModes)}."),
        };
    }

    private static PathPreference SetError(ArgumentResult result, string message)
    {
        result.AddError(message);
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
