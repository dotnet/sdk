// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.DotNet.Test.MSTest.Utilities;

/// <summary>
/// An <see cref="ILoggerFactory"/> that writes log messages to an MSTest
/// <see cref="TestContext"/> (when provided) and a simple console sink. Useful for tests that
/// need a real <see cref="ILoggerFactory"/> (e.g. for components that take one in their ctor).
/// </summary>
public sealed class TestLoggerFactory : ILoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public TestLoggerFactory(TestContext? testContext = null)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

            if (testContext is not null)
            {
                builder.AddProvider(new TestContextLoggerProvider(testContext));
            }

            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
                options.IncludeScopes = true;
            });
        });
    }

    public void Dispose() => _loggerFactory.Dispose();

    public ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);

    public ILogger CreateLogger() => CreateLogger("Test Host");

    public void AddProvider(ILoggerProvider provider) => _loggerFactory.AddProvider(provider);

    private sealed class TestContextLoggerProvider(TestContext testContext) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestContextLogger(testContext, categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class TestContextLogger(TestContext testContext, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            testContext.WriteLine($"{logLevel}: {categoryName}: {formatter(state, exception)}");
            if (exception is not null)
            {
                testContext.WriteLine(exception.ToString());
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
