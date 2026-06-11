// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers.UnitTests;

public sealed class TestLoggerFactory : ILoggerFactory
{
    private readonly List<ILoggerProvider> _loggerProviders = new();
    private readonly List<ILoggerFactory> _factories = new();

    public TestLoggerFactory(TestContext? testContext = null)
    {
        if (testContext is not null)
        {
            _loggerProviders.Add(new TestContextLoggerProvider(testContext));
        }
    }

    public void Dispose()
    {
        while (_factories.Count > 0)
        {
            ILoggerFactory factory = _factories[0];
            _factories.RemoveAt(0);
            factory.Dispose();
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

            foreach (ILoggerProvider loggerProvider in _loggerProviders)
            {
                builder.AddProvider(loggerProvider);
            }

            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
                options.IncludeScopes = true;
            });
        });

        _factories.Add(loggerFactory);
        return loggerFactory.CreateLogger(categoryName);
    }

    public ILogger CreateLogger() => CreateLogger("Test Host");

    public void AddProvider(ILoggerProvider provider) => _loggerProviders.Add(provider);

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
