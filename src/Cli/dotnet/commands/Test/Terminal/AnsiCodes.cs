// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// A collection of standard ANSI/VT100 control codes.
/// </summary>
internal static class AnsiCodes
{
    /// <summary>
    ///  Escape character.
    /// </summary>
    public const string Esc = "\x1b";

    /// <summary>
    /// The control sequence introducer.
    /// </summary>
    public const string CSI = $"{Esc}[";

    /// <summary>
    /// Select graphic rendition.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>color-code<see cref="SetColor"/> to change text color.
    /// </remarks>
    public const string SetColor = "m";

    /// <summary>
    /// Select graphic rendition - set bold mode.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/><see cref="SetBold"/> to change text to bold.
    /// </remarks>
    public const string SetBold = "1m";

    /// <summary>
    /// A shortcut to reset color back to normal.
    /// </summary>
    public const string SetDefaultColor = CSI + "m";

    /// <summary>
    /// Non-xterm extension to render a hyperlink.
    /// </summary>
    /// <remarks>
    /// Print <see cref="LinkPrefix"/>url<see cref="LinkInfix"/>text<see cref="LinkSuffix"/> to render a hyperlink.
    /// </remarks>
    public const string LinkPrefix = $"{Esc}]8;;";

    /// <summary>
    /// <see cref="LinkPrefix"/>.
    /// </summary>
    public const string LinkInfix = $"{Esc}\\";

    /// <summary>
    /// <see cref="LinkPrefix"/>.
    /// </summary>
    public const string LinkSuffix = $"{Esc}]8;;{Esc}\\";

    /// <summary>
    /// Moves up the specified number of lines and puts cursor at the beginning of the line.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>N<see cref="MoveUpToLineStart"/> to move N lines up.
    /// </remarks>
    public const string MoveUpToLineStart = "F";

    /// <summary>
    /// Moves forward (to the right) the specified number of characters.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>N<see cref="MoveForward"/> to move N characters forward.
    /// </remarks>
    public const string MoveForward = "C";

    /// <summary>
    /// Moves backward (to the left) the specified number of characters.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>N<see cref="MoveBackward"/> to move N characters backward.
    /// </remarks>
    public const string MoveBackward = "D";

    /// <summary>
    /// Clears everything from cursor to end of screen.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/><see cref="EraseInDisplay"/> to clear.
    /// </remarks>
    public const string EraseInDisplay = "J";

    /// <summary>
    /// Clears everything from cursor to the end of the current line.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/><see cref="EraseInLine"/> to clear.
    /// </remarks>
    public const string EraseInLine = "K";

    /// <summary>
    /// Hides the cursor.
    /// </summary>
    public const string HideCursor = $"{Esc}[?25l";

    /// <summary>
    /// Shows/restores the cursor.
    /// </summary>
    public const string ShowCursor = $"{Esc}[?25h";

    /// <summary>
    /// Set progress state to a busy spinner. <br/>
    /// Note: this code works only on ConEmu terminals, and conflicts with push a notification code on iTerm2.
    /// </summary>
    /// <remarks>
    /// <see href="https://conemu.github.io/en/AnsiEscapeCodes.html#ConEmu_specific_OSC">ConEmu specific OSC codes.</see><br/>
    /// <see href="https://iterm2.com/documentation-escape-codes.html">iTerm2 proprietary escape codes.</see>
    /// </remarks>
    public const string SetBusySpinner = $"{Esc}]9;4;3;{Esc}\\";

    /// <summary>
    /// Remove progress state, restoring taskbar status to normal. <br/>
    /// Note: this code works only on ConEmu terminals, and conflicts with push a notification code on iTerm2.
    /// </summary>
    /// <remarks>
    /// <see href="https://conemu.github.io/en/AnsiEscapeCodes.html#ConEmu_specific_OSC">ConEmu specific OSC codes.</see><br/>
    /// <see href="https://iterm2.com/documentation-escape-codes.html">iTerm2 proprietary escape codes.</see>
    /// </remarks>
    public const string RemoveBusySpinner = $"{Esc}]9;4;0;{Esc}\\";

    public static string Colorize(string? s, TerminalColor color)
        => String.IsNullOrWhiteSpace(s) ? s ?? string.Empty : $"{CSI}{(int)color}{SetColor}{s}{SetDefaultColor}";

    public static string MakeBold(string? s)
        => String.IsNullOrWhiteSpace(s) ? s ?? string.Empty : $"{CSI}{SetBold}{s}{SetDefaultColor}";

    public static string MoveCursorBackward(int count) => $"{CSI}{count}{MoveBackward}";

    /// <summary>
    /// Moves cursor to the specified column, or the rightmost column if <paramref name="column"/> is greater than the width of the terminal.
    /// </summary>
    /// <param name="column">Column index.</param>
    /// <returns>Control codes to set the desired position.</returns>
    public static string SetCursorHorizontal(int column) => $"{CSI}{column}G";
}
