// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvSetCommandParser
{
    private static readonly string[] s_supportedModes = OperatingSystem.IsWindows()
        ? ["none", "shell", "full"]
        : ["none", "shell"];

    public static readonly Argument<DotnetAccessMode?> ModeArgument = CreateModeArgument();

    public static readonly Option<string?> DotnetupOnPathOption = CreateDotnetupOnPathOption();

    private static Argument<DotnetAccessMode?> CreateModeArgument()
    {
        var argument = new Argument<DotnetAccessMode?>("mode")
        {
            HelpName = "MODE",
            Description = $"The dotnet access mode: {string.Join(", ", s_supportedModes.Select(m => $"'{m}'"))}. Omit to re-apply the stored mode.",
            Arity = ArgumentArity.ZeroOrOne,
            CustomParser = ParseMode,
        };
        argument.CompletionSources.Add(_ => s_supportedModes);
        return argument;
    }

    private static DotnetAccessMode? ParseMode(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var token = result.Tokens[0].Value.ToLowerInvariant();
        return token switch
        {
            "none" => DotnetAccessMode.None,
            "shell" => DotnetAccessMode.Shell,
            "full" when OperatingSystem.IsWindows() => DotnetAccessMode.Full,
            "full" => ModeError(result, "'full' mode is only supported on Windows. Use 'shell' on this platform."),
            _ => ModeError(result, $"Unknown env mode '{token}'. Expected one of: {string.Join(", ", s_supportedModes)}."),
        };
    }

    private static DotnetAccessMode? ModeError(ArgumentResult result, string message)
    {
        result.AddError(message);
        return null;
    }

    // A string option (rather than bool?) so System.CommandLine does not treat it as a
    // value-optional boolean flag; we require an explicit 'true'/'false' token, parsed in the command.
    private static Option<string?> CreateDotnetupOnPathOption()
    {
        var option = new Option<string?>("--dotnetup-on-path")
        {
            Description = "Whether the dotnetup directory is on PATH ('true' or 'false'). Omit to leave unchanged.",
            HelpName = "true|false",
            Arity = ArgumentArity.ExactlyOne,
        };
        option.AcceptOnlyFromAmong("true", "false");
        return option;
    }

    /// <summary>
    /// Parses the raw <c>--dotnetup-on-path</c> token into a tri-state: <c>null</c> when the
    /// option was omitted (leave unchanged), otherwise the true/false boolean.
    /// </summary>
    public static bool? ParseDotnetupOnPath(string? raw) => raw?.ToLowerInvariant() switch
    {
        null => null,
        "true" => true,
        "false" => false,
        _ => throw new ArgumentException($"Invalid value '{raw}' for --dotnetup-on-path. Expected 'true' or 'false'.", nameof(raw)),
    };

    public static Command ConstructCommand()
    {
        Command command = new("set", "Apply (or re-sync) the configured env settings.");
        command.Arguments.Add(ModeArgument);
        command.Options.Add(DotnetupOnPathOption);
        command.Options.Add(CommonOptions.ShellOption);
        command.SetAction(parseResult => new EnvSetCommand(parseResult).Execute());
        return command;
    }
}
