// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

internal sealed class WebServerHost(IAsyncDisposable listener, ImmutableArray<string> webSocketEndpoints, ImmutableArray<string> httpEndpoints) : IAsyncDisposable
{
    /// <summary>
    /// Web socket endpoints.
    /// </summary>
    public ImmutableArray<string> WebSocketEndpoints
        => webSocketEndpoints;

    /// <summary>
    /// Loopback HTTP addresses the browser tools provider listens on.
    /// </summary>
    public ImmutableArray<string> HttpEndpoints
        => httpEndpoints;

    public ValueTask DisposeAsync()
        => listener.DisposeAsync();
}
