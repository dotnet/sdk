// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Theme;

internal enum ThemeAction
{
    Show,
    Set,
    Reset,
    Use,
    List,
}

internal class ThemeCommand : CommandBase
{
    private readonly ThemeAction _action;
    private readonly string? _colorName;
    private readonly string? _colorValue;

    private readonly string? _presetName;

    public ThemeCommand(ParseResult result, ThemeAction action) : base(result)
    {
        _action = action;
        _colorName = result.GetValue(ThemeCommandParser.ColorNameArgument);
        _colorValue = result.GetValue(ThemeCommandParser.ColorValueArgument);
        _presetName = result.GetValue(ThemeCommandParser.PresetNameArgument);
    }

    protected override string GetCommandName() => "theme";

    protected override int ExecuteCore()
    {
        return _action switch
        {
            ThemeAction.Show => ShowTheme(),
            ThemeAction.Set => SetThemeColor(),
            ThemeAction.Reset => ResetTheme(),
            ThemeAction.Use => UsePreset(),
            ThemeAction.List => ListPresets(),
            _ => 1,
        };
    }

    private static int ShowTheme()
    {
        ThemeColors theme = DotnetupTheme.Current;
        AnsiConsole.MarkupLine("[bold]Current theme:[/]");
        AnsiConsole.WriteLine();

        foreach (var (name, (get, _)) in ThemeColors.s_properties)
        {
            string value = get(theme);
            AnsiConsole.MarkupLine($"  [{value}]{name,-10}[/]  {value}");
        }

        return 0;
    }

    private int SetThemeColor()
    {
        if (string.IsNullOrWhiteSpace(_colorName))
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Error("Error: Please specify a color name."));
            AnsiConsole.MarkupLine($"Available names: {string.Join(", ", ThemeColors.s_properties.Keys)}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(_colorValue))
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Error("Error: Please specify a color value (e.g. green, #9780E5)."));
            return 1;
        }

        if (!ThemeColors.s_properties.TryGetValue(_colorName, out var prop))
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: Unknown color name '{_colorName.EscapeMarkup()}'."));
            AnsiConsole.MarkupLine($"Available names: {string.Join(", ", ThemeColors.s_properties.Keys)}");
            return 1;
        }

        if (!DotnetupTheme.IsValidColor(_colorValue))
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: '{_colorValue.EscapeMarkup()}' is not a valid color."));
            AnsiConsole.MarkupLine("Use a named color (green, red), hex (#RRGGBB), or rgb(r,g,b).");
            return 1;
        }

        DotnetupConfigData config = DotnetupConfig.Read() ?? new DotnetupConfigData();
        config.Theme ??= new ThemeColors();
        prop.Set(config.Theme, _colorValue);
        DotnetupConfig.Write(config);
        DotnetupTheme.Reload();

        AnsiConsole.MarkupLine($"[{_colorValue}]{_colorName}[/] set to [{_colorValue}]{_colorValue}[/]");
        return 0;
    }

    private static int ResetTheme()
    {
        DotnetupConfigData config = DotnetupConfig.Read() ?? new DotnetupConfigData();
        config.Theme = null;
        DotnetupConfig.Write(config);
        DotnetupTheme.Reload();

        AnsiConsole.MarkupLine(DotnetupTheme.Success("Theme reset to defaults."));
        return 0;
    }

    private int UsePreset()
    {
        if (string.IsNullOrWhiteSpace(_presetName))
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Error("Error: Please specify a preset name."));
            AnsiConsole.MarkupLine($"Available presets: {string.Join(", ", ThemeColors.s_presets.Keys)}");
            return 1;
        }

        if (!ThemeColors.s_presets.TryGetValue(_presetName, out var preset))
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: Unknown preset '{_presetName.EscapeMarkup()}'."));
            AnsiConsole.MarkupLine($"Available presets: {string.Join(", ", ThemeColors.s_presets.Keys)}");
            return 1;
        }

        DotnetupConfigData config = DotnetupConfig.Read() ?? new DotnetupConfigData();
        config.Theme = new ThemeColors
        {
            Success = preset.Success,
            Error = preset.Error,
            Warning = preset.Warning,
            Accent = preset.Accent,
            Brand = preset.Brand,
            Dim = preset.Dim,
        };
        DotnetupConfig.Write(config);
        DotnetupTheme.Reload();

        AnsiConsole.MarkupLine(DotnetupTheme.Success($"Theme set to '{_presetName}'."));
        return 0;
    }

    private static int ListPresets()
    {
        AnsiConsole.MarkupLine("[bold]Available presets:[/]");
        AnsiConsole.WriteLine();

        foreach (var (name, preset) in ThemeColors.s_presets)
        {
            AnsiConsole.MarkupLine($"  [bold]{name}[/]");
            foreach (var (colorName, (get, _)) in ThemeColors.s_properties)
            {
                string value = get(preset);
                AnsiConsole.MarkupLine($"    [{value}]{colorName,-10}[/]  {value}");
            }
            AnsiConsole.WriteLine();
        }

        return 0;
    }
}
