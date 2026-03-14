// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Represents an option in the interactive selector with a title and description.
/// </summary>
internal record SelectableOption(string Key, string Title, string Description);

/// <summary>
/// A custom interactive option selector that shows all options simultaneously,
/// highlights the currently selected option, and supports keyboard navigation
/// (up/down arrows) and type-ahead selection (number keys or option name prefix).
/// Grey autocomplete text is shown for a partial text match to the selected option.
/// </summary>
internal static class InteractiveOptionSelector
{
    /// <summary>
    /// Displays the interactive selector and returns the index of the chosen option.
    /// </summary>
    /// <param name="title">The prompt title displayed above the options.</param>
    /// <param name="options">The list of options to display.</param>
    /// <param name="defaultIndex">The initially selected option index (0-based).</param>
    /// <returns>The index of the selected option.</returns>
    public static int Show(string title, IReadOnlyList<SelectableOption> options, int defaultIndex = 0)
    {
        if (options.Count == 0)
        {
            throw new ArgumentException("At least one option is required.", nameof(options));
        }

        if (!Console.IsInputRedirected)
        {
            return RunInteractive(title, options, defaultIndex);
        }

        // Fallback for non-interactive/redirected input: just return the default
        Render(title, options, defaultIndex, "", firstRender: true);
        return defaultIndex;
    }

    private static int RunInteractive(string title, IReadOnlyList<SelectableOption> options, int defaultIndex)
    {
        int selectedIndex = defaultIndex;
        string typedText = "";
        bool firstRender = true;

        // Initial render
        Render(title, options, selectedIndex, typedText, firstRender);
        firstRender = false;

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                    typedText = "";
                    break;

                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % options.Count;
                    typedText = "";
                    break;

                case ConsoleKey.Enter:
                    ClearAndRenderFinal(title, options, selectedIndex);
                    return selectedIndex;

                case ConsoleKey.Backspace:
                    if (typedText.Length > 0)
                    {
                        typedText = typedText[..^1];
                        var match = FindMatch(options, typedText);
                        if (match >= 0)
                        {
                            selectedIndex = match;
                        }
                    }
                    break;

                case ConsoleKey.Escape:
                    typedText = "";
                    break;

                default:
                    if (keyInfo.KeyChar != '\0' && !char.IsControl(keyInfo.KeyChar))
                    {
                        string newTyped = typedText + keyInfo.KeyChar;

                        // Check for number key shortcuts (1, 2, 3, etc.)
                        if (typedText.Length == 0 && char.IsDigit(keyInfo.KeyChar))
                        {
                            int num = keyInfo.KeyChar - '0';
                            if (num >= 1 && num <= options.Count)
                            {
                                selectedIndex = num - 1;
                                typedText = "";
                                break;
                            }
                        }

                        // Try text matching against option titles
                        var match = FindMatch(options, newTyped);
                        if (match >= 0)
                        {
                            typedText = newTyped;
                            selectedIndex = match;
                        }
                        // If no match, ignore the keystroke
                    }
                    break;
            }

            Render(title, options, selectedIndex, typedText, firstRender);
        }
    }

    private static int FindMatch(IReadOnlyList<SelectableOption> options, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return -1;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Title.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                options[i].Key.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void Render(string title, IReadOnlyList<SelectableOption> options, int selectedIndex, string typedText, bool firstRender)
    {
        // Calculate total lines we need: title + blank line + (2 lines per option: title + description) + input line
        int totalLines = 1 + 1 + (options.Count * 2) + 1;

        // Move cursor up to overwrite previous render (skip on first render)
        if (!firstRender)
        {
            Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - totalLines));
        }

        // Title
        Console.ForegroundColor = ConsoleColor.White;
        ClearAndWriteLine(title);
        ClearAndWriteLine("");
        Console.ResetColor();

        // Options
        for (int i = 0; i < options.Count; i++)
        {
            bool isSelected = i == selectedIndex;
            string prefix = isSelected ? "> " : "  ";
            string numberLabel = string.Format(CultureInfo.InvariantCulture, "{0}. {1}", i + 1, options[i].Title);

            if (isSelected)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                ClearAndWriteLine(string.Format(CultureInfo.InvariantCulture, "{0}{1}", prefix, numberLabel));
                Console.ForegroundColor = ConsoleColor.Gray;
                ClearAndWriteLine(string.Format(CultureInfo.InvariantCulture, "     {0}", options[i].Description));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                ClearAndWriteLine(string.Format(CultureInfo.InvariantCulture, "{0}{1}", prefix, numberLabel));
                ClearAndWriteLine(string.Format(CultureInfo.InvariantCulture, "     {0}", options[i].Description));
            }

            Console.ResetColor();
        }

        // Input line with autocomplete ghost text
        Console.ResetColor();
        string inputPrefix = "> ";
        Console.Write(inputPrefix);

        if (typedText.Length > 0)
        {
            Console.Write(typedText);
            // Show ghost autocomplete text for the selected option
            string selectedTitle = options[selectedIndex].Title;
            if (selectedTitle.StartsWith(typedText, StringComparison.OrdinalIgnoreCase) && typedText.Length < selectedTitle.Length)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(selectedTitle[typedText.Length..]);
                Console.ResetColor();
            }
        }
        else
        {
            // Show current selection name as ghost text
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(options[selectedIndex].Title);
            Console.ResetColor();
        }

        ClearToEndOfLine();
        Console.WriteLine();
    }

    private static void ClearAndRenderFinal(string title, IReadOnlyList<SelectableOption> options, int selectedIndex)
    {
        // Calculate total lines we need to clear
        int totalLines = 1 + 1 + (options.Count * 2) + 1;

        if (Console.CursorTop >= totalLines)
        {
            Console.SetCursorPosition(0, Console.CursorTop - totalLines);
        }

        // Clear all the lines
        for (int i = 0; i < totalLines; i++)
        {
            ClearAndWriteLine("");
        }

        // Move back up
        Console.SetCursorPosition(0, Console.CursorTop - totalLines);

        // Write final selection
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(title);
        Console.ResetColor();
        Console.Write(" ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(options[selectedIndex].Title);
        Console.ResetColor();
    }

    private static void ClearAndWriteLine(string text)
    {
        Console.Write(text);
        ClearToEndOfLine();
        Console.WriteLine();
    }

    private static void ClearToEndOfLine()
    {
        try
        {
            int remaining = Console.BufferWidth - Console.CursorLeft - 1;
            if (remaining > 0)
            {
                Console.Write(new string(' ', remaining));
            }
        }
        catch (IOException)
        {
            // BufferWidth may not be available when output is redirected
        }
    }
}
