// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;

namespace Microsoft.DotNet.HotReload;

internal sealed class WebServerHost(IHost host, ImmutableArray<string> endPoints)
    : AbstractWebServerHost(endPoints, virtualDirectory: "/")
{
    public override void Dispose()
        => host.Dispose();

    public override Task StartAsync(CancellationToken cancellation)
        => host.StartAsync(cancellation);
}
