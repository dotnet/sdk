// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;
using Spectre.Console.Rendering;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Builds the .NET bot banner panel for the dotnetup first-run screen.
/// </summary>
internal static class DotnetBotBanner
{
    /// <summary>
    /// Minimum terminal width needed to render the bot graphic without wrapping.
    /// Art (8 chars) + text + padding + border = ~55 columns.
    /// </summary>
    private const int MinWidthForBotArt = 55;

    /// <summary>
    /// Builds the banner panel for the given terminal width.
    /// </summary>
    internal static Panel BuildPanel(int terminalWidth)
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

        // Build a two-column table so the bot art and text align independently.
        // This avoids Spectre.Console miscounting Unicode block-character widths
        // which causes the right panel border to shift when using Expand or Rows.
        IRenderable content;
        if (terminalWidth >= MinWidthForBotArt)
        {
            var table = new Table { Border = TableBorder.None, ShowHeaders = false };
            table.AddColumn(new TableColumn("art").NoWrap().Width(9));
            table.AddColumn(new TableColumn("text").NoWrap());
            table.AddRow(new Markup($"[{brand}]   \u2022[/]"), new Markup(string.Empty));
            table.AddRow(new Markup($"[{brand}]  \u2584\u2588\u2584[/]"), new Markup(string.Empty));
            table.AddRow(new Markup($"[{brand}] \u2588[/] \u2022\u2022 [{brand}]\u2588[/][white]\u25a0[/]"), new Markup($"[{brand} bold]dotnetup[/] v{version.EscapeMarkup()}"));
            table.AddRow(new Markup($"[{brand}]  \u2580\u2588\u2580[/]"), new Markup($"[dim]{description.EscapeMarkup()}[/]"));
            content = table;
        }
        else
        {
            content = new Rows(
                new Markup($"[{brand} bold]dotnetup[/] v{version.EscapeMarkup()}"),
                new Markup($"[dim]{description.EscapeMarkup()}[/]"));
        }

        return new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.FromHex(brand)),
            Padding = new Padding(1, 0),
        };
    }
}
