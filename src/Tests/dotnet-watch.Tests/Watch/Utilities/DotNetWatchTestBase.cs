// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tests;

public abstract class DotNetWatchTestBase : IDisposable
{
    internal TestAssetsManager TestAssets { get; }
    internal WatchableApp App { get; }

    public DotNetWatchTestBase(ITestOutputHelper logger)
    {
        App = new WatchableApp(logger);
        TestAssets = new TestAssetsManager(logger);
    }

    public ITestOutputHelper Logger => App.Logger;

    public void Dispose()
    {
        App.Dispose();
    }
}
