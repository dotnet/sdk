// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

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
