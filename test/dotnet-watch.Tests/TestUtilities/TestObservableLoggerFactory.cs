// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestObservableLoggerFactory(TestEventObserver observer, ILoggerFactory underlyingFactory) : ILoggerFactory
{
    private class Logger(TestEventObserver observer, ILogger underlyingLogger) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            observer.Observe(eventId);
            underlyingLogger.Log(logLevel, eventId, state, exception, formatter);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => underlyingLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => underlyingLogger.IsEnabled(logLevel);
    }

    public ILogger CreateLogger(string categoryName)
        => new Logger(observer, underlyingFactory.CreateLogger(categoryName));

    public void AddProvider(ILoggerProvider provider)
        => underlyingFactory.AddProvider(provider);

    public void Dispose()
        => underlyingFactory.Dispose();
}
