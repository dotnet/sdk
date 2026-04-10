// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Builds the banner panel for the dotnetup first-run screen.
/// </summary>
internal static class DotnetBotBanner
{
    /// <summary>
    /// Builds the banner panel.
    /// </summary>
    internal static Panel BuildPanel()
    {
        // Trim the commit hash from the informational version (e.g. "0.1.1-preview+abc123" -> "0.1.1-preview")
        string version = Parser.Version;
        int plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        string brand = DotnetupTheme.Current.Brand;
        string description = ".NET installation manager for developers.";

        var content = new Rows(
            new Markup($"[{brand} bold]dotnetup[/] v{version.EscapeMarkup()}"),
            new Markup($"[dim]{description.EscapeMarkup()}[/]"));

        return new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(brand),
            Padding = new Padding(1, 0),
        };
    }
}
