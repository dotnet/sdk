// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Semantic color names used throughout dotnetup output.
/// All values are Spectre.Console color strings (named colors like "green",
/// hex values like "#9780E5", or RGB like "rgb(151,128,229)").
/// </summary>
internal sealed class ThemeColors
{
    /// <summary>Color for success messages (installs, completions).</summary>
    public string Success { get; set; } = "green";

    /// <summary>Color for error messages.</summary>
    public string Error { get; set; } = "red";

    /// <summary>Color for warning messages.</summary>
    public string Warning { get; set; } = "yellow";

    /// <summary>Color for emphasis on versions, paths, and key values.</summary>
    public string Accent { get; set; } = "#9780E5";

    /// <summary>Color for the dotnet bot banner and branding elements.</summary>
    public string Brand { get; set; } = "#9780E5";

    /// <summary>Color for secondary/de-emphasized text.</summary>
    public string Dim { get; set; } = "dim";

    /// <summary>Built-in preset themes keyed by name.</summary>
    internal static readonly IReadOnlyDictionary<string, ThemeColors> s_presets =
        new Dictionary<string, ThemeColors>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ThemeColors(),
            ["standard"] = new ThemeColors
            {
                Success = "green",
                Error = "red",
                Warning = "yellow",
                Accent = "blue",
                Brand = "blue",
                Dim = "dim",
            },
            ["monokai"] = new ThemeColors
            {
                Success = "#A6E22E",
                Error = "#F92672",
                Warning = "#FD971F",
                Accent = "#66D9EF",
                Brand = "#AE81FF",
                Dim = "#75715E",
            },
        };

    /// <summary>All recognized color property names and their getters/setters.</summary>
    internal static readonly IReadOnlyDictionary<string, (Func<ThemeColors, string> Get, Action<ThemeColors, string> Set)> s_properties =
        new Dictionary<string, (Func<ThemeColors, string>, Action<ThemeColors, string>)>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = (t => t.Success, (t, v) => t.Success = v),
            ["error"] = (t => t.Error, (t, v) => t.Error = v),
            ["warning"] = (t => t.Warning, (t, v) => t.Warning = v),
            ["accent"] = (t => t.Accent, (t, v) => t.Accent = v),
            ["brand"] = (t => t.Brand, (t, v) => t.Brand = v),
            ["dim"] = (t => t.Dim, (t, v) => t.Dim = v),
        };
}

/// <summary>
/// Provides access to the current theme and helpers for building Spectre markup strings.
/// Thread-safe singleton; loads from config on first access.
/// </summary>
internal static class DotnetupTheme
{
    /// <summary>
    /// Gets the current theme colors. Loaded from the config file on first access;
    /// falls back to defaults if the config is missing or has no theme section.
    /// </summary>
    public static ThemeColors Current { get; private set; } = Load();

    /// <summary>
    /// Forces a reload from the config file (e.g., after the theme command modifies it).
    /// </summary>
    public static void Reload() => Current = Load();

    private static ThemeColors Load()
    {
        var config = DotnetupConfig.Read();
        return config?.Theme ?? new ThemeColors();
    }

    // ── Markup helpers ──────────────────────────────────────────────

    /// <summary>Wraps text in success color markup: <c>[green]text[/]</c></summary>
    public static string Success(string text) => $"[{Current.Success}]{text}[/]";

    /// <summary>Wraps text in error color markup.</summary>
    public static string Error(string text) => $"[{Current.Error}]{text}[/]";

    /// <summary>Wraps text in warning color markup.</summary>
    public static string Warning(string text) => $"[{Current.Warning}]{text}[/]";

    /// <summary>Wraps text in accent color markup (versions, paths).</summary>
    public static string Accent(string text) => $"[{Current.Accent}]{text}[/]";

    /// <summary>Wraps text in brand color markup (dotnet purple).</summary>
    public static string Brand(string text) => $"[{Current.Brand}]{text}[/]";

    /// <summary>Wraps text in dim/secondary color markup.</summary>
    public static string Dim(string text) => $"[{Current.Dim}]{text}[/]";

    /// <summary>
    /// Validates that a color string is acceptable for Spectre.Console markup.
    /// Accepts named colors (green, red, blue, dim, bold, ...), hex (#RRGGBB), and rgb().
    /// </summary>
    public static bool IsValidColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        // Hex: #RRGGBB
        if (color.StartsWith('#'))
        {
            return Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");
        }

        // rgb(r,g,b)
        if (color.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(color, @"^rgb\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\)$", RegexOptions.IgnoreCase);
        }

        // Named color / style: must be a simple word or underscore-separated (e.g. "mediumpurple1", "bold")
        return Regex.IsMatch(color, "^[a-zA-Z][a-zA-Z0-9_]*$");
    }
}
