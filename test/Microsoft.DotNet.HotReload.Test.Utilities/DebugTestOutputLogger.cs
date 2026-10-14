// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class DebugTestOutputLogger(ITestOutputHelper logger) : ITestOutputHelper
{
    public event Action<string>? OnMessage;

    public void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => WriteLine($"[TEST {Path.GetFileName(testPath)}:{testLine}] {message}");

    public void WriteLine(string message)
    {
        Debug.WriteLine(message);

        try
        {
            logger.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            // test output might have been disposed
        }

        OnMessage?.Invoke(message);
    }

    public void WriteLine(string format, params object[] args)
        => WriteLine(string.Format(format, args));
}
