// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Enumerates the text colors supported by VT100 terminal.
/// </summary>
internal enum TerminalColor
{
    /// <summary>
    /// Black.
    /// </summary>
    Black = 30,

    /// <summary>
    /// DarkRed.
    /// </summary>
    DarkRed = 31,

    /// <summary>
    /// DarkGreen.
    /// </summary>
    DarkGreen = 32,

    /// <summary>
    /// DarkYellow.
    /// </summary>
    DarkYellow = 33,

    /// <summary>
    /// DarkBlue.
    /// </summary>
    DarkBlue = 34,

    /// <summary>
    /// DarkMagenta.
    /// </summary>
    DarkMagenta = 35,

    /// <summary>
    /// DarkCyan.
    /// </summary>
    DarkCyan = 36,

    /// <summary>
    /// Gray. This entry looks out of order, but in reality 37 is dark white, which is lighter than bright black = Dark Gray in Console colors.
    /// </summary>
    Gray = 37,

    /// <summary>
    /// Default.
    /// </summary>
    Default = 39,

    /// <summary>
    /// DarkGray. This entry looks out of order, but in reality 90 is bright black, which is darker than dark white = Gray in Console colors.
    /// </summary>
    DarkGray = 90,

    /// <summary>
    /// Red.
    /// </summary>
    Red = 91,

    /// <summary>
    /// Green.
    /// </summary>
    Green = 92,

    /// <summary>
    /// Yellow.
    /// </summary>
    Yellow = 93,

    /// <summary>
    /// Blue.
    /// </summary>
    Blue = 94,

    /// <summary>
    /// Magenta.
    /// </summary>
    Magenta = 95,

    /// <summary>
    /// Cyan.
    /// </summary>
    Cyan = 96,

    /// <summary>
    /// White.
    /// </summary>
    White = 97,
}
