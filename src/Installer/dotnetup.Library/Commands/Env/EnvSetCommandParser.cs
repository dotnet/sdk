// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvSetCommandParser
{
    private readonly record struct ModeOption(string Token, DotnetAccessMode Mode, bool WindowsOnly);

    // Single source of truth tying each CLI token to its DotnetAccessMode.
    private static readonly ModeOption[] s_modes =
    [
        new("none", DotnetAccessMode.None, WindowsOnly: false),
        new("shell", DotnetAccessMode.Shell, WindowsOnly: false),
        new("everywhere", DotnetAccessMode.Everywhere, WindowsOnly: true),
    ];

    private static readonly string[] s_supportedModes = s_modes
        .Where(m => !m.WindowsOnly || OperatingSystem.IsWindows())
        .Select(m => m.Token)
        .ToArray();

    public static readonly Argument<DotnetAccessMode?> ModeArgument = CreateModeArgument();

    public static readonly Option<bool?> DotnetupOnPathOption = CreateDotnetupOnPathOption();

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
        foreach (var option in s_modes)
        {
            if (!string.Equals(option.Token, token, StringComparison.Ordinal))
            {
                continue;
            }

            if (option.WindowsOnly && !OperatingSystem.IsWindows())
            {
                return ModeError(result, $"'{token}' mode is only supported on Windows. Use 'shell' on this platform.");
            }

            return option.Mode;
        }

        return ModeError(result, $"Unknown env mode '{token}'. Expected one of: {string.Join(", ", s_supportedModes)}.");
    }

    private static DotnetAccessMode? ModeError(ArgumentResult result, string message)
    {
        result.AddError(message);
        return null;
    }

    // A tri-state bool?: omitted → null (leave unchanged), otherwise the explicit true/false.
    // ExactlyOne arity requires the value so it is never treated as a value-optional flag.
    private static Option<bool?> CreateDotnetupOnPathOption()
    {
        var option = new Option<bool?>("--dotnetup-on-path")
        {
            Description = "Whether the dotnetup directory is on PATH ('true' or 'false'). Omit to leave unchanged.",
            HelpName = "true|false",
            Arity = ArgumentArity.ExactlyOne,
        };
        option.AcceptOnlyFromAmong("true", "false");
        return option;
    }

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
