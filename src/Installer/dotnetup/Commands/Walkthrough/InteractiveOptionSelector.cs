// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Represents an option in the interactive selector with a title, description, and hover tooltip.
/// </summary>
internal record SelectableOption(string Key, string Title, string Description, string Tooltip);

/// <summary>
/// A custom interactive option selector that uses Spectre.Console's LiveDisplay
/// for flicker-free rendering. Shows all options with a slowly flashing arrow
/// indicator on the selected item. Supports up/down arrow navigation.
/// </summary>
internal static class InteractiveOptionSelector
{
    // Arrow flash interval in milliseconds.
    private const int FlashIntervalMs = 600;

    /// <summary>
    /// Displays the interactive selector and returns the index of the chosen option.
    /// </summary>
    public static int Show(string title, IReadOnlyList<SelectableOption> options, int defaultIndex = 0)
    {
        if (options.Count == 0)
        {
            throw new ArgumentException("At least one option is required.", nameof(options));
        }

        if (Console.IsInputRedirected)
        {
            // Fallback for non-interactive/redirected input: render once and return default
            AnsiConsole.Write(BuildRenderable(title, options, defaultIndex, showArrow: true));
            return defaultIndex;
        }

        return RunInteractive(title, options, defaultIndex);
    }

    private static int RunInteractive(string title, IReadOnlyList<SelectableOption> options, int defaultIndex)
    {
        int selectedIndex = defaultIndex;
        bool showArrow = true;
        long lastToggle = Environment.TickCount64;
        bool done = false;

        AnsiConsole.Live(BuildRenderable(title, options, selectedIndex, showArrow))
            .AutoClear(true)
            .Start(ctx =>
            {
                while (!done)
                {
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(intercept: true);
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.UpArrow:
                                selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                                showArrow = true;
                                lastToggle = Environment.TickCount64;
                                break;

                            case ConsoleKey.DownArrow:
                                selectedIndex = (selectedIndex + 1) % options.Count;
                                showArrow = true;
                                lastToggle = Environment.TickCount64;
                                break;

                            case ConsoleKey.Enter:
                                done = true;
                                return;
                        }

                        ctx.UpdateTarget(BuildRenderable(title, options, selectedIndex, showArrow));
                        continue;
                    }

                    long now = Environment.TickCount64;
                    if (now - lastToggle >= FlashIntervalMs)
                    {
                        lastToggle = now;
                        showArrow = !showArrow;
                        ctx.UpdateTarget(BuildRenderable(title, options, selectedIndex, showArrow));
                    }

                    Thread.Sleep(50);
                }
            });

        // Render final compact result after LiveDisplay clears its region
        RenderFinal(title, options, selectedIndex);

        return selectedIndex;
    }

    private static Rows BuildRenderable(string title, IReadOnlyList<SelectableOption> options,
        int selectedIndex, bool showArrow)
    {
        var theme = DotnetupTheme.Current;

        var rows = new List<IRenderable>
        {
            new Markup($"[bold {theme.Brand}]{title.EscapeMarkup()}[/]"),
            Text.Empty,
        };

        for (int i = 0; i < options.Count; i++)
        {
            bool isSelected = i == selectedIndex;

            if (isSelected)
            {
                string prefix = showArrow ? "> " : "  ";
                rows.Add(new Markup(string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}]{1}[bold]{2}[/][/]",
                    theme.Brand,
                    prefix,
                    options[i].Title.EscapeMarkup())));
                rows.Add(new Markup(string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0}",
                    options[i].Description.EscapeMarkup())));
            }
            else
            {
                rows.Add(new Markup(string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}]  {1}[/]",
                    theme.Dim,
                    options[i].Title.EscapeMarkup())));
                rows.Add(new Markup(string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}]    {1}[/]",
                    theme.Dim,
                    options[i].Description.EscapeMarkup())));
            }

            rows.Add(Text.Empty);
        }

        // Tooltip for selected option
        rows.Add(new Markup(string.Format(
            CultureInfo.InvariantCulture,
            "  {0}",
            options[selectedIndex].Tooltip.EscapeMarkup())));

        return new Rows(rows);
    }

    private static void RenderFinal(string title, IReadOnlyList<SelectableOption> options, int selectedIndex)
    {
        AnsiConsole.MarkupLine($"[bold {DotnetupTheme.Current.Brand}]{title.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}[/]",
            DotnetupTheme.Current.Dim,
            options[selectedIndex].Title.EscapeMarkup()));
    }
}

