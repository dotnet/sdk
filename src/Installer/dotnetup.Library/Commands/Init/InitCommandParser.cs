// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

internal static class InitCommandParser
{
    public static readonly Option<DotnetAccessMode?> ModeOption = CreateModeOption();

    private static readonly Command s_command = ConstructCommand();

    public static Command GetCommand() => s_command;

    private static Option<DotnetAccessMode?> CreateModeOption()
    {
        var option = new Option<DotnetAccessMode?>("--mode")
        {
            HelpName = "MODE",
            Description = BuildModeDescription(),
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = AccessModeCliParsing.Parse,
        };
        option.CompletionSources.Add(_ => AccessModeCliParsing.SupportedTokens);
        return option;
    }

    private static string BuildModeDescription()
    {
        string description = string.Format(
            CultureInfo.CurrentCulture,
            Strings.InitModeOptionDescription,
            AccessModeCliParsing.FormatSupportedTokens());
        return DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(DotnetAccessMode.Admin)
            ? description + " " + Strings.InitAdminModeOptionDescription
            : description;
    }

    private static Command ConstructCommand()
    {
        Command command = new("init", Strings.InitCommandDescription);

        command.Options.Add(ModeOption);
        command.Options.Add(CommonOptions.InstallPathOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.NoProgressOption);
        command.Options.Add(CommonOptions.ShellOption);
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(CommonOptions.RequireMuxerUpdateOption);
        command.Options.Add(CommonOptions.InteractiveOption);
        command.Options.Add(CommonOptions.DryRunOption);

        command.SetAction(parseResult => new InitCommand(parseResult).Execute());

        return command;
    }
}
