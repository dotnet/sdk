// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestBrowserRefreshServer(
    string middlewareAssemblyPath,
    bool useGatewayProxy,
    Func<SharedSecretProvider?>? sessionKeyFactory = null)
    : BrowserRefreshServer(
        logger: new TestLogger(),
        connectionServerLoggerFactory: _ => new TestLogger(),
        connectionAgentLoggerFactory: _ => new TestLogger(),
        sessionKeyFactory ?? new(static () => new SharedSecretProvider()),
        middlewareAssemblyPath,
        useGatewayProxy,
        suppressTimeouts: true)
{
    public Func<WebSocketConfig, WebServerHost>? CreateAndStartHostImpl;

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(WebSocketConfig webSocketConfig, CancellationToken cancellationToken)
        => ValueTask.FromResult((CreateAndStartHostImpl ?? throw new NotImplementedException())(webSocketConfig));
}
