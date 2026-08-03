// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests;

/// <summary>
/// Forwards <see cref="ILogger"/> messages to <see cref="TestContext.WriteLine(string?)"/>
/// so that shared helpers expecting an <see cref="ILogger"/> can surface their output in
/// MSTest's per-test log.
/// </summary>
internal sealed class TestContextLogger(TestContext testContext) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        testContext.WriteLine($"{logLevel}: {formatter(state, exception)}");
        if (exception is not null)
        {
            testContext.WriteLine(exception.ToString());
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
