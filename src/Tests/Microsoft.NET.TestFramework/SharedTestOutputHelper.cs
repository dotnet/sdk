// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.NET.TestFramework;

/// <summary>
/// This is an abstraction so we can pass <see cref="ITestOutputHelper"/> to <see cref="TestCommand"/> constructor.
/// when calling from class fixture.
/// </summary>
public class SharedTestOutputHelper : ITestOutputHelper
{
    private readonly IMessageSink _sink;

    public SharedTestOutputHelper(IMessageSink sink)
    {
        this._sink = sink;
    }

    public void WriteLine(string message)
    {
        _sink.OnMessage(new DiagnosticMessage(message));
    }

    public void WriteLine(string format, params object[] args)
    {
        _sink.OnMessage(new DiagnosticMessage(format, args));
    }
}
