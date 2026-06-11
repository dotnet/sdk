// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers.UnitTests;

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
