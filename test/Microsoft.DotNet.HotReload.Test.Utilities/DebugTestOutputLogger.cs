// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace Microsoft.DotNet.Watch.UnitTests;

public class DebugTestOutputLogger(ITestOutputHelper logger) : ITestOutputHelper
{
    private readonly StringBuilder _output = new();

    public event Action<string>? OnMessage;

    public string Output => _output.ToString();

    public void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => WriteLine($"[TEST {Path.GetFileName(testPath)}:{testLine}] {message}");

    public void Write(string message)
    {
        _output.Append(message);
        Debug.Write(message);
        logger.Write(message);
        OnMessage?.Invoke(message);
    }

    public void Write(string format, params object[] args)
        => Write(string.Format(format, args));

    public void WriteLine(string message)
    {
        _output.AppendLine(message);
        Debug.WriteLine(message);
        logger.WriteLine(message);
        OnMessage?.Invoke(message);
    }

    public void WriteLine(string format, params object[] args)
        =>  WriteLine(string.Format(format, args));
}
