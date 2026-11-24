// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvSetCommandParser
{
    public static readonly Argument<DotnetAccessMode?> ModeArgument = CreateModeArgument();

    public static readonly Option<bool?> DotnetupOnPathOption = CreateDotnetupOnPathOption();

    private static Argument<DotnetAccessMode?> CreateModeArgument()
    {
        var argument = new Argument<DotnetAccessMode?>("mode")
        {
            HelpName = "MODE",
            Description = string.Format(
                CultureInfo.InvariantCulture,
                Strings.EnvSetModeArgumentDescription,
                AccessModeCliParsing.FormatSupportedTokens()),
            Arity = ArgumentArity.ZeroOrOne,
            CustomParser = AccessModeCliParsing.Parse,
        };
        argument.CompletionSources.Add(_ => AccessModeCliParsing.SupportedTokens);
        return argument;
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
