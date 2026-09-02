// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Blazor WebAssembly app hosted by an ASP.NET Core app.
/// App has a client and server projects and deltas are applied to both processes.
/// Agent is injected into the server process. The client process is updated via WebSocketScriptInjection.js injected into the browser.
/// </summary>
internal sealed class BlazorWebAssemblyHostedAppModel(DotNetWatchContext context, ProjectGraphNode clientProject, ProjectGraphNode serverProject)
    : WebApplicationAppModel(context)
{
    public override ProjectGraphNode LaunchingProject => serverProject;

    public override bool ManagedHotReloadRequiresBrowserRefresh => true;

    protected override ImmutableArray<HotReloadClient> CreateManagedClients(ILogger clientLogger, ILogger agentLogger, BrowserRefreshServer? browserRefreshServer)
    {
        Debug.Assert(browserRefreshServer != null);
        return
        [
            CreateWebAssemblyClient(
                clientLogger,
                agentLogger,
                browserRefreshServer,
                clientProject,
                enableBrowserToolsReplay: UsesBrowserToolsProvider),
            new DefaultHotReloadClient(clientLogger, agentLogger, GetStartupHookPath(serverProject), handlesStaticAssetUpdates: false, new NamedPipeClientTransport(clientLogger))
        ];
    }

    internal override bool UsesBrowserToolsProvider
        => clientProject.IsNetCoreApp(Versions.Version11_0) &&
           serverProject.IsNetCoreApp(Versions.Version11_0);

    internal override IBrowserToolsLaunchConfigurator CreateBrowserToolsLaunchConfigurator(
        AbstractBrowserRefreshServer browserRefreshServer,
        bool enableManagedHotReload)
        => UsesBrowserToolsProvider
            ? base.CreateBrowserToolsLaunchConfigurator(browserRefreshServer, enableManagedHotReload)
            : CreateLegacyBrowserToolsLaunchConfigurator(browserRefreshServer, enableManagedHotReload);
}
