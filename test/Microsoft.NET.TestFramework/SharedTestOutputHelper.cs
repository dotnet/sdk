// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Sdk;
using Xunit.v3;

namespace Microsoft.NET.TestFramework;

/// <summary>
/// This is an abstraction so we can pass <see cref="ITestOutputHelper"/> to <see cref="TestCommand"/> constructor.
/// when calling from class fixture.
/// </summary>
public class SharedTestOutputHelper : ITestOutputHelper
{
    private readonly IMessageSink _sink;
    private readonly StringBuilder _output = new();

    public SharedTestOutputHelper(IMessageSink sink)
    {
        _sink = sink;
    }

    public string Output => _output.ToString();

    public void Write(string message)
    {
        _output.Append(message);
        _sink.OnMessage(new DiagnosticMessage(message));
    }

    public void Write(string format, params object[] args)
    {
        var formatted = string.Format(format, args);
        _output.Append(formatted);
        _sink.OnMessage(new DiagnosticMessage(formatted));
    }

    public void WriteLine(string message)
    {
        _output.AppendLine(message);
        _sink.OnMessage(new DiagnosticMessage(message));
    }

    public void WriteLine(string format, params object[] args)
    {
        var formatted = string.Format(format, args);
        _output.AppendLine(formatted);
        _sink.OnMessage(new DiagnosticMessage(formatted));
    }
}
