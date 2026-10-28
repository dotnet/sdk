// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvSetCommandParser
{
    // The dotnet-access modes. CLI tokens are derived from the enum names (lowercased),
    // so the enum stays the single source of truth for the set of modes.
    private static readonly DotnetAccessMode[] s_modes = Enum.GetValues<DotnetAccessMode>();

    private static readonly string[] s_supportedModes = s_modes
        .Where(DotnetAccessModePolicy.IsSupportedOnCurrentPlatform)
        .Select(ToToken)
        .ToArray();

    public static readonly Argument<DotnetAccessMode?> ModeArgument = CreateModeArgument();

    public static readonly Option<bool?> DotnetupOnPathOption = CreateDotnetupOnPathOption();

    private static string ToToken(DotnetAccessMode mode) => mode.ToString().ToLowerInvariant();

    private static Argument<DotnetAccessMode?> CreateModeArgument()
    {
        var argument = new Argument<DotnetAccessMode?>("mode")
        {
            HelpName = "MODE",
            Description = string.Format(
                CultureInfo.InvariantCulture,
                Strings.EnvSetModeArgumentDescription,
                string.Join(", ", s_supportedModes.Select(m => $"'{m}'"))),
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
        foreach (var mode in s_modes)
        {
            if (!string.Equals(ToToken(mode), token, StringComparison.Ordinal))
            {
                continue;
            }

            if (DotnetAccessModePolicy.RequiresWindows(mode) && !OperatingSystem.IsWindows())
            {
                return ModeError(result, string.Format(CultureInfo.InvariantCulture, Strings.EnvModeWindowsOnly, token));
            }

            return mode;
        }

        return ModeError(result, string.Format(
            CultureInfo.InvariantCulture,
            Strings.EnvSetUnknownMode,
            token,
            string.Join(", ", s_supportedModes)));
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
            Description = Strings.EnvSetDotnetupOnPathOptionDescription,
            HelpName = "true|false",
            Arity = ArgumentArity.ExactlyOne,
        };
        option.AcceptOnlyFromAmong("true", "false");
        return option;
    }

    public static Command ConstructCommand()
    {
        Command command = new("set", Strings.EnvSetCommandDescription);
        command.Arguments.Add(ModeArgument);
        command.Options.Add(DotnetupOnPathOption);
        command.Options.Add(CommonOptions.ShellOption);
        command.SetAction(parseResult => new EnvSetCommand(parseResult).Execute());
        return command;
    }
}
