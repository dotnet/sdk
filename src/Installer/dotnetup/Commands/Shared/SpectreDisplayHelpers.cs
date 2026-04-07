// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;
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

/// <summary>
/// The logical action a keypress maps to inside a scroll loop.
/// Pure data — no side effects, no nullable-string discrimination.
/// </summary>
internal enum ScrollAction
{
    None,
    ScrollUp,
    ScrollDown,
    Accept,
    Decline,
    NeverAskAgain,
}

internal static class SpectreDisplayHelpers
{
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
                ? string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n/([bold]p[/])lease never ask again)[/] ", confirmPrompt, brand)
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
    /// Uses Spectre.Console's LiveDisplay for reliable rendering without manual ANSI cursor management.
    /// </summary>
    private static ConfirmResult RunInteractiveScrollLoop(List<string> items, int visibleCount, string? confirmPrompt, bool allowNeverAsk = false)
    {
        int offset = 0;
        int maxOffset = items.Count - visibleCount;
        bool done = false;
        ConfirmResult result = ConfirmResult.Yes;

        SpectreAnsiConsole.Live(BuildScrollRenderable(items, offset, visibleCount, confirmPrompt, allowNeverAsk))
            .AutoClear(true)
            .Start(ctx =>
            {
                ctx.Refresh();

                while (!done)
                {
                    // Poll for input rather than blocking on ReadKey, so the
                    // LiveDisplay can continue refreshing between keypresses.
                    if (!Console.KeyAvailable)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var key = Console.ReadKey(intercept: true);
                    var action = confirmPrompt is not null
                        ? MapConfirmScrollKey(key, allowNeverAsk)
                        : MapPlainScrollKey(key);

                    (done, result, offset) = ApplyScrollAction(action, offset, maxOffset, items, visibleCount, confirmPrompt, allowNeverAsk, ctx);
                }
            });

        // Render final collapsed view after LiveDisplay clears its region
        RenderFinalScrollView(items, confirmPrompt, result);

        return result;
    }

    /// <summary>
    /// Applies a <see cref="ScrollAction"/> to the current scroll state and returns the updated state.
    /// </summary>
    private static (bool Done, ConfirmResult Result, int Offset) ApplyScrollAction(
        ScrollAction action, int offset, int maxOffset, List<string> items, int visibleCount, string? confirmPrompt, bool allowNeverAsk, LiveDisplayContext ctx)
    {
        switch (action)
        {
            case ScrollAction.ScrollUp:
                if (offset > 0)
                {
                    offset--;
                    ctx.UpdateTarget(BuildScrollRenderable(items, offset, visibleCount, confirmPrompt, allowNeverAsk));
                }

                return (false, ConfirmResult.Yes, offset);
            case ScrollAction.ScrollDown:
                if (offset < maxOffset)
                {
                    offset++;
                    ctx.UpdateTarget(BuildScrollRenderable(items, offset, visibleCount, confirmPrompt, allowNeverAsk));
                }

                return (false, ConfirmResult.Yes, offset);
            case ScrollAction.Accept:
                return (true, ConfirmResult.Yes, offset);
            case ScrollAction.Decline:
                return (true, ConfirmResult.No, offset);
            case ScrollAction.NeverAskAgain:
                return (true, ConfirmResult.NeverAskAgain, offset);
            default:
                return (false, ConfirmResult.Yes, offset);
        }
    }

    /// <summary>
    /// Maps a keypress to a <see cref="ScrollAction"/> for a plain (non-confirm) scrollable list.
    /// Only arrows and Enter are meaningful.
    /// </summary>
    internal static ScrollAction MapPlainScrollKey(ConsoleKeyInfo key)
    {
        return key.Key switch
        {
            ConsoleKey.UpArrow => ScrollAction.ScrollUp,
            ConsoleKey.DownArrow => ScrollAction.ScrollDown,
            ConsoleKey.Enter => ScrollAction.Accept,
            _ => ScrollAction.None,
        };
    }

    /// <summary>
    /// Maps a keypress to a <see cref="ScrollAction"/> for a scrollable list with a Y/N confirmation prompt.
    /// </summary>
    internal static ScrollAction MapConfirmScrollKey(ConsoleKeyInfo key, bool allowNeverAsk)
    {
        return key.Key switch
        {
            ConsoleKey.UpArrow => ScrollAction.ScrollUp,
            ConsoleKey.DownArrow => ScrollAction.ScrollDown,
            ConsoleKey.Enter => ScrollAction.Accept,
            ConsoleKey.Y => ScrollAction.Accept,
            ConsoleKey.N => ScrollAction.Decline,
            ConsoleKey.P when allowNeverAsk => ScrollAction.NeverAskAgain,
            _ => ScrollAction.None,
        };
    }

    /// <summary>
    /// Builds a Spectre <see cref="Rows"/> renderable for the current scroll window.
    /// </summary>
    private static Rows BuildScrollRenderable(List<string> items, int offset, int visibleCount, string? confirmPrompt, bool allowNeverAsk)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        var rows = new List<IRenderable>();

        if (offset > 0)
        {
            rows.Add(new Markup(string.Format(CultureInfo.InvariantCulture, "  [{0}]{1} {2} more above[/]", dim, Constants.Symbols.UpTriangle, offset)));
        }
        else
        {
            rows.Add(Text.Empty);
        }

        for (int i = offset; i < offset + visibleCount && i < items.Count; i++)
        {
            rows.Add(new Markup(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, items[i].EscapeMarkup())));
        }

        int remaining = items.Count - offset - visibleCount;
        if (remaining > 0)
        {
            rows.Add(new Markup(string.Format(CultureInfo.InvariantCulture, "  [{0}]{1} {2} more below (use {3}{4} arrows)[/]", dim, Constants.Symbols.DownTriangle, remaining, Constants.Symbols.UpArrow, Constants.Symbols.DownArrow)));
        }
        else
        {
            rows.Add(Text.Empty);
        }

        if (confirmPrompt is not null)
        {
            string promptHint = allowNeverAsk
                ? string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n/([bold]p[/])lease never ask again)[/]", confirmPrompt, DotnetupTheme.Current.Brand)
                : string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/]", confirmPrompt, DotnetupTheme.Current.Brand);
            rows.Add(new Markup(promptHint));
        }
        else if (remaining <= 0)
        {
            rows.Add(new Markup(string.Format(CultureInfo.InvariantCulture, "  [{0}](Press Enter to continue)[/]", dim)));
        }

        return new Rows(rows);
    }

    /// <summary>
    /// Renders the final collapsed view after the user makes a choice.
    /// </summary>
    private static void RenderFinalScrollView(List<string> items, string? confirmPrompt, ConfirmResult result)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        string brand = DotnetupTheme.Current.Brand;

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
}
