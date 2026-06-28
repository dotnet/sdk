// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Bridge adapter that implements both MSTest's <see cref="Microsoft.NET.TestFramework.ITestOutputHelper"/>
/// and xunit's <see cref="Xunit.ITestOutputHelper"/>, enabling it to be passed to utilities that
/// still expect the xunit type (WatchableApp, DebugTestOutputLogger) while also serving the MSTest-based
/// utilities (TestConsole, TestReporter, DotnetCommand) in the test project.
/// </summary>
internal sealed class DualOutputHelper(MSTestFramework::Microsoft.NET.TestFramework.ITestOutputHelper inner)
    : MSTestFramework::Microsoft.NET.TestFramework.ITestOutputHelper, Xunit.ITestOutputHelper
{
    public string Output => inner.Output;
    public void Write(string message) => inner.Write(message);
    public void Write(string format, params object[] args) => inner.Write(format, args);
    public void WriteLine(string message) => inner.WriteLine(message);
    public void WriteLine(string format, params object[] args) => inner.WriteLine(format, args);

    public void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => WriteLine($"[TEST {Path.GetFileName(testPath)}:{testLine}] {message}");
}
