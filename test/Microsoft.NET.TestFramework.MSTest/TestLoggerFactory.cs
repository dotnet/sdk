// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.TestFramework;

/// <summary>
/// MSTest counterpart of the xUnit <c>TestLoggerFactory</c>. Produces an
/// <see cref="ILoggerFactory"/> that writes to the test output (via
/// <see cref="ITestOutputHelper"/>) and a simple console sink.
/// </summary>
public sealed class TestLoggerFactory : ILoggerFactory
{
    private readonly List<ILoggerProvider> _loggerProviders = new();
    private readonly List<ILoggerFactory> _factories = new();

    public TestLoggerFactory(TestContext? testContext)
        : this((ITestOutputHelper)new TestContextOutputHelper(testContext))
    {
    }

    public TestLoggerFactory(ITestOutputHelper testOutput)
    {
        // Reuses the runner-agnostic provider that is link-shared from the legacy framework.
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
}
