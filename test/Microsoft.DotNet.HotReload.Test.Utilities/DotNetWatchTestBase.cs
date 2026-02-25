// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Base class for all tests that create a single dotnet watch process.
/// </summary>
public abstract class DotNetWatchTestBase : WatchSdkTest, IDisposable
{
    internal WatchableApp App { get; }

    public DotNetWatchTestBase(ITestOutputHelper logger)
        : base(logger)
    {
        App = WatchableApp.CreateDotnetWatchApp(Logger);
    }

    public void Dispose()
        => App.Dispose();
}
