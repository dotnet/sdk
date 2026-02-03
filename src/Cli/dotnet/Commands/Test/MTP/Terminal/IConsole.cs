// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// Wraps the static System.Console to be isolatable in tests.
/// </summary>
internal interface IConsole
{
    event ConsoleCancelEventHandler? CancelKeyPress;

    public int BufferHeight { get; }

    public int BufferWidth { get; }

    public bool IsOutputRedirected { get; }

    void SetForegroundColor(ConsoleColor color);

    void SetBackgroundColor(ConsoleColor color);

    ConsoleColor GetForegroundColor();

    ConsoleColor GetBackgroundColor();

    void WriteLine();

    void WriteLine(string? value);

    void WriteLine(object? value);

    void WriteLine(string format, object? arg0);

    void WriteLine(string format, object? arg0, object? arg1);

    void WriteLine(string format, object? arg0, object? arg1, object? arg2);

    void WriteLine(string format, object?[]? args);

    void Write(string format, object?[]? args);

    void Write(string? value);

    void Write(char value);

    void Clear();
}
