// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Reusable Spectre.Console display helpers for interactive scrollable lists and confirmations.
/// Extracted to keep UI rendering separate from decision logic.
/// </summary>
internal static class SpectreDisplayHelpers
{
    /// <summary>
    /// Renders a list of items with only <paramref name="visibleCount"/> shown initially.
    /// When running interactively, the user can scroll with arrow keys to see more.
    /// Falls back to a static truncated list when input is redirected.
    /// </summary>
    internal static void RenderScrollableList(List<string> items, int visibleCount)
    {
        if (items.Count == 0)
        {
            return;
        }

        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;

        if (items.Count <= visibleCount || Console.IsInputRedirected)
        {
            // All items fit or non-interactive — just print them all
            foreach (var item in items)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
            }

            return;
        }

        // Interactive scrollable list
        RunInteractiveScrollLoop(items, visibleCount, confirmPrompt: null);
    }

    /// <summary>
    /// Renders a scrollable list with an inline confirmation prompt.
    /// The prompt is shown below the list and Enter accepts the default (yes).
    /// </summary>
    internal static bool RenderScrollableListWithConfirm(List<string> items, int visibleCount, string confirmPrompt)
    {
        if (items.Count == 0)
        {
            return true;
        }

        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        string brand = DotnetupTheme.Current.Brand;

        if (items.Count <= visibleCount || Console.IsInputRedirected)
        {
            foreach (var item in items)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
            }

            SpectreAnsiConsole.Markup(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/] ", confirmPrompt, brand));
            return ReadYesNo(defaultValue: true);
        }

        return RunInteractiveScrollLoop(items, visibleCount, confirmPrompt);
    }

    /// <summary>
    /// Returns true (accept) or false (decline) when <paramref name="confirmPrompt"/> is set;
    /// always returns true when <paramref name="confirmPrompt"/> is null (plain scroll).
    /// </summary>
    private static bool RunInteractiveScrollLoop(List<string> items, int visibleCount, string? confirmPrompt)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        int offset = 0;
        int maxOffset = items.Count - visibleCount;

        Console.Write(Constants.Ansi.HideCursor);
        try
        {
            int startRow = Console.CursorTop;
            RenderListWindow(items, offset, visibleCount, startRow, firstRender: true, confirmPrompt: confirmPrompt);

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            if (offset > 0)
                            {
                                offset--;
                                RenderListWindow(items, offset, visibleCount, startRow, firstRender: false, confirmPrompt: confirmPrompt);
                            }

                            break;
                        case ConsoleKey.DownArrow:
                            if (offset < maxOffset)
                            {
                                offset++;
                                RenderListWindow(items, offset, visibleCount, startRow, firstRender: false, confirmPrompt: confirmPrompt);
                            }

                            break;
                        case ConsoleKey.Enter:
                            // Collapse to final static view and exit — Enter means "yes" when confirming
                            CollapseToFinalView(items, startRow, dim, accent, confirmPrompt, accepted: true);
                            return true;
                        case ConsoleKey.N:
                            if (confirmPrompt is not null)
                            {
                                CollapseToFinalView(items, startRow, dim, accent, confirmPrompt, accepted: false);
                                return false;
                            }

                            break;
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }
        finally
        {
            Console.Write(Constants.Ansi.ShowCursor);
        }
    }

    private static void CollapseToFinalView(List<string> items, int startRow, string dim, string accent, string? confirmPrompt, bool accepted)
    {
        string brand = DotnetupTheme.Current.Brand;
        Console.SetCursorPosition(0, startRow);
        Console.Write(Constants.Ansi.ClearToEnd);
        foreach (var item in items)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
        }

        if (confirmPrompt is not null)
        {
            string answer = accepted ? "Yes" : "No";
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", confirmPrompt, brand, answer));
        }
    }

    /// <summary>
    /// Reads a single y/n keypress. Returns <paramref name="defaultValue"/> on Enter.
    /// </summary>
    private static bool ReadYesNo(bool defaultValue)
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]{1}[/]", DotnetupTheme.Current.Brand, defaultValue ? "Yes" : "No"));
                    return defaultValue;
                case ConsoleKey.Y:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]Yes[/]", DotnetupTheme.Current.Brand));
                    return true;
                case ConsoleKey.N:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]No[/]", DotnetupTheme.Current.Brand));
                    return false;
            }
        }
    }

    private static void RenderListWindow(List<string> items, int offset, int visibleCount, int startRow, bool firstRender, string? confirmPrompt)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;

        if (!firstRender)
        {
            Console.SetCursorPosition(0, startRow);
            Console.Write(Constants.Ansi.ClearToEnd);
        }

        if (offset > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]{1} {2} more above[/]", dim, Constants.Symbols.UpTriangle, offset));
        }

        for (int i = offset; i < offset + visibleCount && i < items.Count; i++)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, items[i].EscapeMarkup()));
        }

        int remaining = items.Count - offset - visibleCount;
        if (remaining > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]{1} {2} more below (use {3}{4} arrows)[/]", dim, Constants.Symbols.DownTriangle, remaining, Constants.Symbols.UpArrow, Constants.Symbols.DownArrow));
        }

        if (confirmPrompt is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/]", confirmPrompt, DotnetupTheme.Current.Brand));
        }
        else if (remaining <= 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}](Press Enter to continue)[/]", dim));
        }
    }
}
