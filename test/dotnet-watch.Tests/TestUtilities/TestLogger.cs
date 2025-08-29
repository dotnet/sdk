// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestLogger(ITestOutputHelper? output = null) : ILogger
{
    public readonly Lock Guard = new();
    private readonly List<string> _messages = [];

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = $"[{logLevel}] {formatter(state, exception)}";

        lock (Guard)
        {
            _messages.Add(message);
            output?.WriteLine(message);
        }
    }

    public ImmutableArray<string> GetAndClearMessages()
    {
        lock (Guard)
        {
            var result = _messages.ToImmutableArray();
            _messages.Clear();
            return result;
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => throw new NotImplementedException();

    public bool IsEnabled(LogLevel logLevel)
        => true;
}
