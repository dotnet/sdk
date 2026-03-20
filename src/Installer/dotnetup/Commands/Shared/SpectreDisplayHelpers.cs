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
/// <summary>
/// Result of a confirm prompt that supports Y/N/P (never ask again).
/// </summary>
internal enum ConfirmResult
{
    Yes,
    No,
    NeverAskAgain,
}

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
    internal static ConfirmResult RenderScrollableListWithConfirm(List<string> items, int visibleCount, string confirmPrompt, bool allowNeverAsk = false)
    {
        if (items.Count == 0)
        {
            return ConfirmResult.Yes;
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

            string promptSuffix = allowNeverAsk
                ? string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n/[bold]p[/] = never ask again)[/] ", confirmPrompt, brand)
                : string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/] ", confirmPrompt, brand);
            SpectreAnsiConsole.Markup(promptSuffix);
            var result = ReadConfirm(defaultValue: ConfirmResult.Yes, allowNeverAsk: allowNeverAsk);
            SpectreAnsiConsole.WriteLine();
            return result;
        }

        return RunInteractiveScrollLoop(items, visibleCount, confirmPrompt, allowNeverAsk);
    }

    /// <summary>
    /// Returns true (accept) or false (decline) when <paramref name="confirmPrompt"/> is set;
    /// always returns true when <paramref name="confirmPrompt"/> is null (plain scroll).
    /// </summary>
    private static ConfirmResult RunInteractiveScrollLoop(List<string> items, int visibleCount, string? confirmPrompt, bool allowNeverAsk = false)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        int offset = 0;
        int maxOffset = items.Count - visibleCount;
        int lastLineCount = 0;

        Console.Write(Constants.Ansi.HideCursor);
        try
        {
            lastLineCount = RenderListWindow(items, offset, visibleCount, lastLineCount, firstRender: true, confirmPrompt: confirmPrompt, allowNeverAsk: allowNeverAsk);

            while (true)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var result = HandleScrollKey(items, visibleCount, confirmPrompt, allowNeverAsk, dim, accent, ref offset, maxOffset, ref lastLineCount);
                if (result is not null)
                {
                    return result.Value;
                }
            }
        }
        finally
        {
            Console.Write(Constants.Ansi.ShowCursor);
        }
    }

    /// <summary>
    /// Processes a single keypress during the interactive scroll loop.
    /// Returns the <see cref="ConfirmResult"/> when the user makes a final choice, or <c>null</c> to keep looping.
    /// </summary>
    private static ConfirmResult? HandleScrollKey(
        List<string> items, int visibleCount, string? confirmPrompt, bool allowNeverAsk,
        string dim, string accent, ref int offset, int maxOffset, ref int lastLineCount)
    {
        var key = Console.ReadKey(intercept: true);
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (offset > 0)
                {
                    offset--;
                    lastLineCount = RenderListWindow(items, offset, visibleCount, lastLineCount, firstRender: false, confirmPrompt: confirmPrompt, allowNeverAsk: allowNeverAsk);
                }

                return null;
            case ConsoleKey.DownArrow:
                if (offset < maxOffset)
                {
                    offset++;
                    lastLineCount = RenderListWindow(items, offset, visibleCount, lastLineCount, firstRender: false, confirmPrompt: confirmPrompt, allowNeverAsk: allowNeverAsk);
                }

                return null;
            case ConsoleKey.Enter:
                CollapseToFinalView(items, lastLineCount, dim, accent, confirmPrompt, ConfirmResult.Yes);
                return ConfirmResult.Yes;
            case ConsoleKey.N:
                if (confirmPrompt is not null)
                {
                    CollapseToFinalView(items, lastLineCount, dim, accent, confirmPrompt, ConfirmResult.No);
                    return ConfirmResult.No;
                }

                return null;
            case ConsoleKey.P:
                if (confirmPrompt is not null && allowNeverAsk)
                {
                    CollapseToFinalView(items, lastLineCount, dim, accent, confirmPrompt, ConfirmResult.NeverAskAgain);
                    return ConfirmResult.NeverAskAgain;
                }

                return null;
            default:
                return null;
        }
    }

    private static void CollapseToFinalView(List<string> items, int lastLineCount, string dim, string accent, string? confirmPrompt, ConfirmResult result)
    {
        string brand = DotnetupTheme.Current.Brand;

        // Move up by the number of lines rendered and clear everything below
        if (lastLineCount > 0)
        {
            Console.Write(string.Format(CultureInfo.InvariantCulture, "\x1b[{0}A\r\x1b[J", lastLineCount));
        }

        if (result == ConfirmResult.Yes)
        {
            foreach (var item in items)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
            }
        }

        if (confirmPrompt is not null)
        {
            string answer = result switch
            {
                ConfirmResult.Yes => "Yes",
                ConfirmResult.NeverAskAgain => "No (won't ask again)",
                _ => "No",
            };
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", confirmPrompt, brand, answer));
            SpectreAnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Reads a single y/n keypress. Returns <paramref name="defaultValue"/> on Enter.
    /// </summary>
    private static ConfirmResult ReadConfirm(ConfirmResult defaultValue, bool allowNeverAsk)
    {
        string brand = DotnetupTheme.Current.Brand;
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    string defaultLabel = defaultValue == ConfirmResult.Yes ? "Yes" : "No";
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]{1}[/]", brand, defaultLabel));
                    return defaultValue;
                case ConsoleKey.Y:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]Yes[/]", brand));
                    return ConfirmResult.Yes;
                case ConsoleKey.N:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]No[/]", brand));
                    return ConfirmResult.No;
                case ConsoleKey.P:
                    if (allowNeverAsk)
                    {
                        SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]No (won't ask again)[/]", brand));
                        return ConfirmResult.NeverAskAgain;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Renders the visible window of items and returns the number of lines written.
    /// Uses relative cursor movement for reliable re-rendering.
    /// </summary>
    private static int RenderListWindow(List<string> items, int offset, int visibleCount, int previousLineCount, bool firstRender, string? confirmPrompt, bool allowNeverAsk = false)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;

        if (!firstRender && previousLineCount > 0)
        {
            // Move cursor up by the number of lines from the last render, then clear to end of screen
            Console.Write(string.Format(CultureInfo.InvariantCulture, "\x1b[{0}A\r\x1b[J", previousLineCount));
        }

        int lineCount = 0;

        if (offset > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]{1} {2} more above[/]", dim, Constants.Symbols.UpTriangle, offset));
            lineCount++;
        }

        for (int i = offset; i < offset + visibleCount && i < items.Count; i++)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, items[i].EscapeMarkup()));
            lineCount++;
        }

        int remaining = items.Count - offset - visibleCount;
        if (remaining > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]{1} {2} more below (use {3}{4} arrows)[/]", dim, Constants.Symbols.DownTriangle, remaining, Constants.Symbols.UpArrow, Constants.Symbols.DownArrow));
            lineCount++;
        }

        if (confirmPrompt is not null)
        {
            string promptHint = allowNeverAsk
                ? string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n/[bold]p[/] = never ask again)[/]", confirmPrompt, DotnetupTheme.Current.Brand)
                : string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/]", confirmPrompt, DotnetupTheme.Current.Brand);
            SpectreAnsiConsole.MarkupLine(promptHint);
            lineCount++;
        }
        else if (remaining <= 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}](Press Enter to continue)[/]", dim));
            lineCount++;
        }

        return lineCount;
    }
}
