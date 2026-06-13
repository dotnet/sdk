// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    /// <summary>
    /// Forwards <see cref="ILogger"/> messages to <see cref="TestContext.WriteLine(string?)"/>
    /// so that helpers expecting an <see cref="ILogger"/> can surface their output in MSTest's
    /// per-test log. Used as a drop-in replacement for the xUnit-based
    /// <c>Microsoft.TemplateEngine.TestHelper.XunitLoggerProvider</c>.
    /// </summary>
    internal sealed class TestContextLogger : ILogger
    {
        private readonly TestContext _testContext;

        public TestContextLogger(TestContext testContext)
        {
            _testContext = testContext;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _testContext.WriteLine($"{logLevel}: {formatter(state, exception)}");
            if (exception is not null)
            {
                _testContext.WriteLine(exception.ToString());
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
}
