// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Blazor WebAssembly app hosted by an ASP.NET Core app.
/// App has a client and server projects and deltas are applied to both processes.
/// Agent is injected into the server process. The client process is updated via WebSocketScriptInjection.js injected into the browser.
/// </summary>
internal sealed class BlazorWebAssemblyHostedAppModel(ProjectGraphNode clientProject, ProjectGraphNode serverProject)
    : HotReloadAppModel(agentInjectionProject: serverProject)
{
    public override bool RequiresBrowserRefresh => true;

    public override DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, ILogger processLogger)
    {
        if (browserRefreshServer == null)
        {
            // error has been reported earlier
            return null;
        }

        return new BlazorWebAssemblyHostedDeltaApplier(processLogger, browserRefreshServer, clientProject);
    }
}
