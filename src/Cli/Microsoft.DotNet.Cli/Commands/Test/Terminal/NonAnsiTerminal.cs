// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Microsoft.Testing.Platform.Helpers;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Non-ANSI terminal that writes text using the standard Console.Foreground color capabilities to stay compatible with
/// standard Windows command line, and other command lines that are not capable of ANSI, or when output is redirected.
/// </summary>
internal sealed class NonAnsiTerminal : ITerminal
{
    private readonly IConsole _console;
    private readonly ConsoleColor _defaultForegroundColor;
    private readonly StringBuilder _stringBuilder = new();
    private bool _isBatching;

    public NonAnsiTerminal(IConsole console)
    {
        _console = console;
        _defaultForegroundColor = _console.GetForegroundColor();
    }

    public int Width => _console.IsOutputRedirected ? int.MaxValue : _console.BufferWidth;

    public int Height => _console.IsOutputRedirected ? int.MaxValue : _console.BufferHeight;

    public void Append(char value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void Append(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.Append(value);
        }
        else
        {
            _console.Write(value);
        }
    }

    public void AppendLine()
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine();
        }
        else
        {
            _console.WriteLine();
        }
    }

    public void AppendLine(string value)
    {
        if (_isBatching)
        {
            _stringBuilder.AppendLine(value);
        }
        else
        {
            _console.WriteLine(value);
        }
    }

    public void AppendLink(string path, int? lineNumber)
    {
        Append(path);
        if (lineNumber.HasValue)
        {
            Append($":{lineNumber}");
        }
    }

    public void SetColor(TerminalColor color)
    {
        if (_isBatching)
        {
            _console.Write(_stringBuilder.ToString());
            _stringBuilder.Clear();
        }

        _console.SetForegroundColor(ToConsoleColor(color));
    }

    public void ResetColor()
    {
        if (_isBatching)
        {
            _console.Write(_stringBuilder.ToString());
            _stringBuilder.Clear();
        }

        _console.SetForegroundColor(_defaultForegroundColor);
    }

    public void ShowCursor()
    {
        // nop
    }

    public void HideCursor()
    {
        // nop
    }

    public void StartUpdate()
    {
        if (_isBatching)
        {
            throw new InvalidOperationException(LocalizableStrings.ConsoleIsAlreadyInBatchingMode);
        }

        _stringBuilder.Clear();
        _isBatching = true;
    }

    public void StopUpdate()
    {
        _console.Write(_stringBuilder.ToString());
        _isBatching = false;
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

    public void EraseProgress()
    {
        // nop
    }

    public void RenderProgress(TestProgressState?[] progress)
    {
        int count = 0;
        foreach (TestProgressState? p in progress)
        {
            if (p == null)
            {
                continue;
            }

            count++;

            string durationString = HumanReadableDurationFormatter.Render(p.Stopwatch.Elapsed);

            int passed = p.PassedTests;
            int failed = p.FailedTests;
            int skipped = p.SkippedTests;

            // Use just ascii here, so we don't put too many restrictions on fonts needing to
            // properly show unicode, or logs being saved in particular encoding.
            Append('[');
            SetColor(TerminalColor.DarkGreen);
            Append('+');
            Append(passed.ToString(CultureInfo.CurrentCulture));
            ResetColor();

            Append('/');

            SetColor(TerminalColor.DarkRed);
            Append('x');
            Append(failed.ToString(CultureInfo.CurrentCulture));
            ResetColor();

            Append('/');

            SetColor(TerminalColor.DarkYellow);
            Append('?');
            Append(skipped.ToString(CultureInfo.CurrentCulture));
            ResetColor();
            Append(']');

            Append(' ');
            Append(p.AssemblyName);

            if (p.TargetFramework != null || p.Architecture != null)
            {
                Append(" (");
                if (p.TargetFramework != null)
                {
                    Append(p.TargetFramework);
                    Append('|');
                }

                if (p.Architecture != null)
                {
                    Append(p.Architecture);
                }

                Append(')');
            }

            TestDetailState? activeTest = p.TestNodeResultsState?.GetRunningTasks(1).FirstOrDefault();
            if (!String.IsNullOrWhiteSpace(activeTest?.Text))
            {
                Append(" - ");
                Append(activeTest.Text);
                Append(' ');
            }

            Append(durationString);

            AppendLine();
        }

        // Do not render empty lines when there is nothing to show.
        if (count > 0)
        {
            AppendLine();
        }
    }

    public void StartBusyIndicator()
    {
        // nop
    }

    public void StopBusyIndicator()
    {
        // nop
    }
}
