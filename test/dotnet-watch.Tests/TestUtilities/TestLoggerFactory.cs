// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestLoggerFactory(ITestOutputHelper? output = null) : ILoggerFactory
{
    public Func<string, ILogger>? CreateLoggerImpl;

    public ILogger CreateLogger(string categoryName)
        => CreateLoggerImpl?.Invoke(categoryName) ?? new TestLogger(output);

    public void AddProvider(ILoggerProvider provider) {}
    public void Dispose() { }
}
