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
    /// A standalone WebAssembly app is served by the Blazor Gateway (blazor-gateway), a YARP based
    /// host that does not activate ASP.NET Core hosting startups. Reserve only the two provider
    /// endpoints on it; the settings route must be served by Static Web Assets. The hosting startup
    /// configuration from the base implementation is still applied because older target frameworks
    /// are served by blazor-devserver, which is a regular ASP.NET Core host.
    /// </summary>
    internal override void ConfigureBrowserToolsLaunchEnvironment(IDictionary<string, string> environment, AbstractBrowserRefreshServer browserRefreshServer)
    {
        base.ConfigureBrowserToolsLaunchEnvironment(environment, browserRefreshServer);
        AddGatewayProxyEnvironment(environment, browserRefreshServer);
    }

    internal static void AddGatewayProxyEnvironment(IDictionary<string, string> environment, AbstractBrowserRefreshServer browserRefreshServer)
    {
        const string clusterName = "dotnet-browser-tools";
        foreach (var route in new[] { BrowserToolsProtocol.ConnectPath, BrowserToolsProtocol.ClearCachePath })
        {
            var routeName = clusterName + "-" + route.TrimStart('/');
            environment[$"ReverseProxy__Routes__{routeName}__ClusterId"] = clusterName;
            environment[$"ReverseProxy__Routes__{routeName}__Order"] = "-1000";
            environment[$"ReverseProxy__Routes__{routeName}__Match__Path"] = BrowserToolsProtocol.RoutePrefix + route;
        }

        environment[$"ReverseProxy__Clusters__{clusterName}__Destinations__provider__Address"] = browserRefreshServer.ProviderAddress.AbsoluteUri;
    }
}
