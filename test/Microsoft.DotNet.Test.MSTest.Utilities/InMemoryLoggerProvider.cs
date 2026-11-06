// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Test.MSTest.Utilities;

/// <summary>
/// An <see cref="ILoggerProvider"/> that appends every log entry to a caller-supplied list.
/// Useful for tests that need to assert on the exact sequence of log entries produced by a
/// component under test (without dragging in MSTest's TestContext sink).
/// </summary>
public sealed class InMemoryLoggerProvider(List<(LogLevel, string)> messagesCollection) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(messagesCollection);

    public void Dispose()
    {
    }

    private sealed class InMemoryLogger(List<(LogLevel, string)> messagesCollection) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messagesCollection.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
