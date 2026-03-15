// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// <see cref="ILoggerFactory"/> to be used with Xunit tests.
    /// Adds <see cref="XunitLoggerProvider"/> and SimpleConsole logger by default.
    /// Additional logger providers are supported via <see cref="TestLoggerFactory.AddProvider(ILoggerProvider)"/> method.
    /// </summary>
    public sealed class TestLoggerFactory : ILoggerFactory
    {
        private readonly List<ILoggerProvider> _loggerProviders = new();

        private readonly List<ILoggerFactory> _factories = new();

        public TestLoggerFactory(IMessageSink? messageSink = null)
        {
            if (messageSink != null)
            {
                SharedTestOutputHelper testOutputHelper = new(messageSink);
                _loggerProviders.Add(new XunitLoggerProvider(testOutputHelper));
            }
        }

        public TestLoggerFactory(ITestOutputHelper testOutput)
        {
            _loggerProviders.Add(new XunitLoggerProvider(testOutput));
        }

        public void Dispose()
        {
            while (_factories.Count > 0)
            {
                ILoggerFactory factory = _factories[0];
                _factories.RemoveAt(0);

                factory?.Dispose();
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace);
                if (_loggerProviders?.Any() ?? false)
                {
                    foreach (ILoggerProvider loggerProvider in _loggerProviders)
                    {
                        builder.AddProvider(loggerProvider);
                    }
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

        public void AddProvider(ILoggerProvider provider)
        {
            _loggerProviders.Add(provider);
        }
    }
}
