// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestLogger(ITestOutputHelper? output = null) : ILogger
{
    public readonly List<string> Messages = [];

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = $"[{logLevel}] {formatter(state, exception)}";

        Messages.Add(message);
        output?.WriteLine(message);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => throw new NotImplementedException();

    public bool IsEnabled(LogLevel logLevel)
        => true;
}
