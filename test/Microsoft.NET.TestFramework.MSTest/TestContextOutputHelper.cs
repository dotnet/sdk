// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.TestFramework;

/// <summary>
/// MSTest-backed implementation of <see cref="ITestOutputHelper"/>. Echoes output to the
/// supplied <see cref="TestContext"/> (so it shows up in the test results) while also
/// capturing it in memory (exposed via <see cref="Output"/>).
/// </summary>
public sealed class TestContextOutputHelper : ITestOutputHelper
{
    private readonly TestContext? _testContext;
    private readonly StringBuilder _output = new();

    public TestContextOutputHelper(TestContext? testContext)
    {
        _testContext = testContext;
    }

    public string Output => _output.ToString();

    public void Write(string message)
    {
        _output.Append(message);
        // TestContext only exposes WriteLine; appending without a trailing newline is the
        // closest approximation for partial writes.
        _testContext?.Write(message);
    }

    public void Write(string format, params object[] args)
        => Write(string.Format(format, args));

    public void WriteLine(string message)
    {
        _output.AppendLine(message);
        _testContext?.WriteLine("{0}", message);
    }

    public void WriteLine(string format, params object[] args)
        => WriteLine(string.Format(format, args));

    public override string ToString() => _output.ToString();
}
