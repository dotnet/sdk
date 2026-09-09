// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Adapter over <see cref="Microsoft.NET.TestFramework.ITestOutputHelper"/> that adds a
/// <see cref="Log"/> helper and can be passed both to the SDK test framework utilities
/// (TestConsole, TestReporter, DotnetCommand) and to the watch test utilities (WatchableApp,
/// DebugTestOutputLogger).
/// </summary>
internal sealed class DualOutputHelper(MSTestFramework::Microsoft.NET.TestFramework.ITestOutputHelper inner)
    : MSTestFramework::Microsoft.NET.TestFramework.ITestOutputHelper
{
    public string Output => inner.Output;
    public void Write(string message) => inner.Write(message);
    public void Write(string format, params object[] args) => inner.Write(format, args);
    public void WriteLine(string message) => inner.WriteLine(message);
    public void WriteLine(string format, params object[] args) => inner.WriteLine(format, args);

    public void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => WriteLine($"[TEST {Path.GetFileName(testPath)}:{testLine}] {message}");
}
