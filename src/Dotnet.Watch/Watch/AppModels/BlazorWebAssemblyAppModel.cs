// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Blazor client-only WebAssembly app.
/// </summary>
internal sealed class BlazorWebAssemblyAppModel(DotNetWatchContext context, ProjectGraphNode clientProject)
    : WebApplicationAppModel(context)
{
    public override ProjectGraphNode LaunchingProject => clientProject;

    public override bool ManagedHotReloadRequiresBrowserRefresh => true;

    protected override ImmutableArray<HotReloadClient> CreateManagedClients(ILogger clientLogger, ILogger agentLogger, BrowserRefreshServer? browserRefreshServer)
    {
        Debug.Assert(browserRefreshServer != null);
        return [CreateWebAssemblyClient(clientLogger, agentLogger, browserRefreshServer, clientProject)];
    }

    /// <summary>
    /// The Gateway (blazor-devserver) already hosts a YARP proxy. Reserve a route on it that forwards
    /// <see cref="BrowserToolsProtocol.RoutePrefix"/> to the provider instead of injecting a hosting startup.
    /// </summary>
    internal override void ConfigureBrowserToolsLaunchEnvironment(IDictionary<string, string> environment, AbstractBrowserRefreshServer browserRefreshServer)
    {
        const string routeAndClusterName = "dotnet-browser-tools";

        environment[$"ReverseProxy__Routes__{routeAndClusterName}__ClusterId"] = routeAndClusterName;
        environment[$"ReverseProxy__Routes__{routeAndClusterName}__Order"] = "-1000";
        environment[$"ReverseProxy__Routes__{routeAndClusterName}__Match__Path"] = BrowserToolsProtocol.RoutePrefix + "/{**catch-all}";
        environment[$"ReverseProxy__Clusters__{routeAndClusterName}__Destinations__provider__Address"] = browserRefreshServer.ProviderAddress.AbsoluteUri;
    }
}
