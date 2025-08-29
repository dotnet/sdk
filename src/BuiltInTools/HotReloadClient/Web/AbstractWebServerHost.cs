// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

internal abstract class AbstractWebServerHost(ImmutableArray<string> endPoints, string virtualDirectory) : IDisposable
{
    public ImmutableArray<string> EndPoints { get; } = endPoints;
    public string VirtualDirectory => virtualDirectory;

    public abstract Task StartAsync(CancellationToken cancellation);

    public abstract void Dispose();
}
