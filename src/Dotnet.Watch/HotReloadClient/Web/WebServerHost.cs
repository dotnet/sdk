// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

internal sealed class WebServerHost(IAsyncDisposable listener, ImmutableArray<string> webSocketEndPoints, ImmutableArray<string> httpEndPoints) : IAsyncDisposable
{
    /// <summary>
    /// Web socket endpoints.
    /// </summary>
    public ImmutableArray<string> WebSocketEndPoints
        => webSocketEndPoints;

    /// <summary>
    /// Loopback HTTP addresses the browser tools provider listens on.
    /// </summary>
    public ImmutableArray<string> HttpEndPoints
        => httpEndPoints;

    public ValueTask DisposeAsync()
        => listener.DisposeAsync();
}
