// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;

namespace Microsoft.DotNet.HotReload;

internal sealed class WebServerHost(IDisposable listener, ImmutableArray<string> endPoints, ImmutableArray<string> httpEndPoints) : IDisposable
{
    public ImmutableArray<string> EndPoints
        => endPoints;

    /// <summary>
    /// Loopback HTTP addresses the browser tools provider listens on.
    /// </summary>
    public ImmutableArray<string> HttpEndPoints
        => httpEndPoints;

    public void Dispose()
        => listener.Dispose();
}
