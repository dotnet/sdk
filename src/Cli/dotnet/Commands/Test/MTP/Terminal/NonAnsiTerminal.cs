// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Non-ANSI terminal that writes text using the standard Console.Foreground color capabilities to stay compatible with
/// standard Windows command line, and other command lines that are not capable of ANSI, or when output is redirected.
/// </summary>
internal sealed class NonAnsiTerminal : SimpleTerminal
{
    private readonly ConsoleColor _defaultForegroundColor;
    private bool? _colorNotSupported;

    public NonAnsiTerminal(IConsole console)
        : base(console)
        => _defaultForegroundColor = IsForegroundColorNotSupported() ? ConsoleColor.Black : console.GetForegroundColor();

    public override void SetColor(TerminalColor color)
    {
        if (IsForegroundColorNotSupported())
        {
            return;
        }

        Console.SetForegroundColor(ToConsoleColor(color));
    }

    public override void ResetColor()
    {
        if (IsForegroundColorNotSupported())
        {
            return;
        }

        Console.SetForegroundColor(_defaultForegroundColor);
    }

    private bool IsForegroundColorNotSupported()
    {
        _colorNotSupported ??= RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));

        return _colorNotSupported.Value;
    }

    private ConsoleColor ToConsoleColor(TerminalColor color) => color switch
    {
        TerminalColor.Black => ConsoleColor.Black,
        TerminalColor.DarkRed => ConsoleColor.DarkRed,
        TerminalColor.DarkGreen => ConsoleColor.DarkGreen,
        TerminalColor.DarkYellow => ConsoleColor.DarkYellow,
        TerminalColor.DarkBlue => ConsoleColor.DarkBlue,
        TerminalColor.DarkMagenta => ConsoleColor.DarkMagenta,
        TerminalColor.DarkCyan => ConsoleColor.DarkCyan,
        TerminalColor.Gray => ConsoleColor.White,
        TerminalColor.Default => _defaultForegroundColor,
        TerminalColor.DarkGray => ConsoleColor.Gray,
        TerminalColor.Red => ConsoleColor.Red,
        TerminalColor.Green => ConsoleColor.Green,
        TerminalColor.Yellow => ConsoleColor.Yellow,
        TerminalColor.Blue => ConsoleColor.Blue,
        TerminalColor.Magenta => ConsoleColor.Magenta,
        TerminalColor.Cyan => ConsoleColor.Cyan,
        TerminalColor.White => ConsoleColor.White,
        _ => _defaultForegroundColor,
    };
}
