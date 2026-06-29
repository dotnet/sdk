// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

internal sealed class CapturingConsole : IConsole
{
    private readonly StringBuilder _output = new();
    private ConsoleColor _foreground = ConsoleColor.Gray;
    private ConsoleColor _background = ConsoleColor.Black;

#pragma warning disable CS0067 // Event is never used; required by IConsole contract.
    public event ConsoleCancelEventHandler? CancelKeyPress;
#pragma warning restore CS0067

    public int BufferHeight => 30;

    public int BufferWidth => 120;

    public bool IsOutputRedirected => true;

    public string GetOutput() => _output.ToString();

    public void SetForegroundColor(ConsoleColor color) => _foreground = color;

    public void SetBackgroundColor(ConsoleColor color) => _background = color;

    public ConsoleColor GetForegroundColor() => _foreground;

    public ConsoleColor GetBackgroundColor() => _background;

    public void WriteLine() => _output.AppendLine();

    public void WriteLine(string? value) => _output.AppendLine(value);

    public void WriteLine(object? value) => _output.AppendLine(value?.ToString());

    public void WriteLine(string format, object? arg0) => _output.AppendLine(string.Format(format, arg0));

    public void WriteLine(string format, object? arg0, object? arg1) => _output.AppendLine(string.Format(format, arg0, arg1));

    public void WriteLine(string format, object? arg0, object? arg1, object? arg2) => _output.AppendLine(string.Format(format, arg0, arg1, arg2));

    public void WriteLine(string format, object?[]? args) => _output.AppendLine(string.Format(format, args ?? Array.Empty<object?>()));

    public void Write(string format, object?[]? args) => _output.Append(string.Format(format, args ?? Array.Empty<object?>()));

    public void Write(string? value) => _output.Append(value);

    public void Write(char value) => _output.Append(value);

    public void Clear() => _output.Clear();
}
