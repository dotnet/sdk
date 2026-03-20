// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Semantic color names used throughout dotnetup output.
/// All values are standard ANSI color names supported by Spectre.Console
/// (e.g. "green", "red", "blue", "magenta", "yellow", "grey").
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
    public string Accent { get; set; } = "magenta";

    /// <summary>Color for the dotnet bot banner and branding elements.</summary>
    public string Brand { get; set; } = "magenta";

    /// <summary>Color for completion/finished highlights (e.g. progress bar at 100%).</summary>
    public string SuccessAlt { get; set; } = "green";

    /// <summary>Color for secondary/de-emphasized text.</summary>
    public string Dim { get; set; } = "grey";

    /// <summary>Color for success emphasis (installed versions/paths in success messages).</summary>
    public string SuccessAccent { get; set; } = "green";
}

/// <summary>
/// Provides access to the current theme and helpers for building Spectre markup strings.
/// </summary>
internal static class DotnetupTheme
{
    /// <summary>
    /// Gets the current theme colors (default theme).
    /// </summary>
    public static ThemeColors Current { get; private set; } = new ThemeColors();

    /// <summary>
    /// Resets to the default theme.
    /// </summary>
    public static void Reload() => Current = new ThemeColors();

    // ── Markup helpers ──────────────────────────────────────────────

    /// <summary>Wraps text in the theme's success color markup.</summary>
    public static string Success(string text) => $"[{Current.Success}]{text}[/]";

    /// <summary>Wraps text in the theme's error color markup.</summary>
    public static string Error(string text) => $"[{Current.Error}]{text}[/]";

    /// <summary>Wraps text in the theme's warning color markup.</summary>
    public static string Warning(string text) => $"[{Current.Warning}]{text}[/]";

    /// <summary>Wraps text in the theme's accent color markup (versions, paths).</summary>
    public static string Accent(string text) => $"[{Current.Accent}]{text}[/]";

    /// <summary>Wraps text in the theme's brand color markup.</summary>
    public static string Brand(string text) => $"[{Current.Brand}]{text}[/]";

    /// <summary>Wraps text in the theme's dim/secondary color markup.</summary>
    public static string Dim(string text) => $"[{Current.Dim}]{text}[/]";

    /// <summary>Wraps text in the theme's success-accent color markup (versions/paths in success messages).</summary>
    public static string SuccessAccent(string text) => $"[{Current.SuccessAccent}]{text}[/]";
}
