// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Theme;

internal static class ThemeCommandParser
{
    public static readonly Argument<string?> ColorNameArgument = new("name")
    {
        Description = "The theme color name to set (e.g. success, error, warning, accent, brand, dim).",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Argument<string?> ColorValueArgument = new("value")
    {
        Description = "The color value (e.g. green, red, #9780E5).",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Argument<string?> PresetNameArgument = new("preset")
    {
        Description = "The preset theme name (e.g. default, standard, monokai).",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Command s_themeCommand = ConstructCommand();

    public static Command GetCommand() => s_themeCommand;

    private static Command ConstructCommand()
    {
        Command setCommand = new("set", "Set a theme color (e.g. dotnetup theme set accent #9780E5)");
        setCommand.Arguments.Add(ColorNameArgument);
        setCommand.Arguments.Add(ColorValueArgument);
        setCommand.SetAction(parseResult => new ThemeCommand(parseResult, ThemeAction.Set).Execute());

        Command resetCommand = new("reset", "Reset all theme colors to defaults");
        resetCommand.SetAction(parseResult => new ThemeCommand(parseResult, ThemeAction.Reset).Execute());

        Command useCommand = new("use", "Apply a preset theme (e.g. dotnetup theme use monokai)");
        useCommand.Arguments.Add(PresetNameArgument);
        useCommand.SetAction(parseResult => new ThemeCommand(parseResult, ThemeAction.Use).Execute());

        Command listCommand = new("list", "List available preset themes");
        listCommand.SetAction(parseResult => new ThemeCommand(parseResult, ThemeAction.List).Execute());

        Command command = new("theme", "View or customize output colors");
        command.Subcommands.Add(setCommand);
        command.Subcommands.Add(resetCommand);
        command.Subcommands.Add(useCommand);
        command.Subcommands.Add(listCommand);

        // Running `dotnetup theme` with no subcommand shows current theme
        command.SetAction(parseResult => new ThemeCommand(parseResult, ThemeAction.Show).Execute());

        return command;
    }
}
