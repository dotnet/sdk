// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class SystemConsole : IConsole
{
    private const int WriteBufferSize = 256;
    private static readonly StreamWriter CaptureConsoleOutWriter;

    /// <summary>
    /// Gets the height of the buffer area.
    /// </summary>
    public int BufferHeight => Console.BufferHeight;

    /// <summary>
    /// Gets the width of the buffer area.
    /// </summary>
    public int BufferWidth => Console.BufferWidth;

    /// <summary>
    /// Gets a value indicating whether output has been redirected from the standard output stream.
    /// </summary>
    public bool IsOutputRedirected => Console.IsOutputRedirected;

    static SystemConsole() =>
        // From https://github.com/dotnet/runtime/blob/main/src/libraries/System.Console/src/System/Console.cs#L236
        CaptureConsoleOutWriter = new StreamWriter(
            stream: Console.OpenStandardOutput(),
            encoding: Console.Out.Encoding,
            bufferSize: WriteBufferSize,
            leaveOpen: true)
        {
            AutoFlush = true,
        };

    // the following event does not make sense in the mobile scenarios, user cannot ctrl+c
    // but can just kill the app in the device via a gesture
    public event ConsoleCancelEventHandler? CancelKeyPress
    {
        add => Console.CancelKeyPress += value;
        remove => Console.CancelKeyPress -= value;
    }

    public void WriteLine()
    {
        CaptureConsoleOutWriter.WriteLine();
    }

    public void WriteLine(string? value)
    {
        CaptureConsoleOutWriter.WriteLine(value);
    }

    public void WriteLine(object? value)
    {
        CaptureConsoleOutWriter.WriteLine(value);
    }

    public void WriteLine(string format, object? arg0)
    {
        CaptureConsoleOutWriter.WriteLine(format, arg0);
    }

    public void WriteLine(string format, object? arg0, object? arg1)
    {
        CaptureConsoleOutWriter.WriteLine(format, arg0, arg1);
    }

    public void WriteLine(string format, object? arg0, object? arg1, object? arg2)
    {
        CaptureConsoleOutWriter.WriteLine(format, arg0, arg1, arg2);
    }

    public void WriteLine(string format, object?[]? args)
    {
        CaptureConsoleOutWriter.WriteLine(format, args!);
    }

    public void Write(string format, object?[]? args)
    {
        CaptureConsoleOutWriter.Write(format, args!);
    }

    public void Write(string? value)
    {
        CaptureConsoleOutWriter.Write(value);
    }

    public void Write(char value)
    {
        CaptureConsoleOutWriter.Write(value);
    }

    public void SetForegroundColor(ConsoleColor color)
        => Console.ForegroundColor = color;

    public void SetBackgroundColor(ConsoleColor color)
        => Console.BackgroundColor = color;

    public ConsoleColor GetForegroundColor()
        => Console.ForegroundColor;

    public ConsoleColor GetBackgroundColor()
        => Console.BackgroundColor;

    public void Clear() => Console.Clear();
}
