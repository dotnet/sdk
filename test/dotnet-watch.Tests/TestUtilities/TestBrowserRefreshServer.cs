// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestBrowserRefreshServer(string middlewareAssemblyPath)
    : AbstractBrowserRefreshServer(middlewareAssemblyPath, new TestLogger(), new TestLoggerFactory())
{
    public Func<WebServerHost>? CreateAndStartHostImpl;

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult((CreateAndStartHostImpl ?? throw new NotImplementedException())());

    protected override bool SuppressTimeouts => true;
}
