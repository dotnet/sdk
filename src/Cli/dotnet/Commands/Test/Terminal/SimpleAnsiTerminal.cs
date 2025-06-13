// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;


/// <summary>
/// Simple terminal that uses 4-bit ANSI for colors but does not move cursor and does not do other fancy stuff to stay compatible with CI systems like AzDO.
/// The colors are set on start of every line to properly color multiline strings in AzDO output.
/// </summary>
internal sealed class SimpleAnsiTerminal : SimpleTerminal
{
    private string? _foregroundColor;
    private bool _prependColor;

    public SimpleAnsiTerminal(IConsole console)
        : base(console)
    {
    }

    public override void Append(string value)
    {
        // Previous write appended line, so we need to prepend color.
        if (_prependColor)
        {
            Console.Write(_foregroundColor);
            // This line is not adding new line at the end, so we don't need to prepend color on next line.
            _prependColor = false;
        }

        Console.Write(SetColorPerLine(value));
    }

    public override void AppendLine(string value)
    {
        // Previous write appended line, so we need to prepend color.
        if (_prependColor)
        {
            Console.Write(_foregroundColor);
        }

        Console.WriteLine(SetColorPerLine(value));
        // This call appended new line so the next write to console needs to prepend color.
        _prependColor = true;
    }

    public override void SetColor(TerminalColor color)
    {
        string setColor = $"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}";
        _foregroundColor = setColor;
        Console.Write(setColor);
        // This call set the color for current line, no need to prepend on next write.
        _prependColor = false;
    }

    public override void ResetColor()
    {
        _foregroundColor = null;
        _prependColor = false;
        Console.Write(AnsiCodes.SetDefaultColor);
    }

    private string? SetColorPerLine(string value)
        => _foregroundColor == null ? value : value.Replace("\n", $"\n{_foregroundColor}");
}
