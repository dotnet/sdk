// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.DotNet.HotReload;

internal sealed class WebServerHost : IDisposable
{
    private readonly IDisposable _listener;

    public WebServerHost(IDisposable listener, ImmutableArray<string> endPoints, string virtualDirectory)
        : this(listener, endPoints, [.. endPoints.Select(GetHttpEndpoint)], virtualDirectory)
    {
    }

    public WebServerHost(
        IDisposable listener,
        ImmutableArray<string> webSocketEndpoints,
        ImmutableArray<string> httpEndpoints,
        string virtualDirectory)
    {
        _listener = listener;
        EndPoints = webSocketEndpoints;
        HttpEndPoints = httpEndpoints;
        VirtualDirectory = virtualDirectory;
    }

    public ImmutableArray<string> EndPoints
    {
        get;
    }

    public ImmutableArray<string> HttpEndPoints
    {
        get;
    }

    public string VirtualDirectory
    {
        get;
    }

    public void Dispose()
        => _listener.Dispose();

    private static string GetHttpEndpoint(string endpoint)
    {
        var builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.StartsWith("wss:", StringComparison.OrdinalIgnoreCase) ? "https" : "http"
        };

        return builder.Uri.ToString().TrimEnd('/');
    }
}
