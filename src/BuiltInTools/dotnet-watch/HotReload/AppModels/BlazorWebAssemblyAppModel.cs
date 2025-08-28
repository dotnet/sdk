// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Blazor client-only WebAssembly app.
/// </summary>
internal sealed class BlazorWebAssemblyAppModel(ProjectGraphNode clientProject, EnvironmentOptions environmentOptions)
    // Blazor WASM does not need agent injected as all changes are applied in the browser, the process being launched is a dev server.
    : WebApplicationAppModel(agentInjectionProject: null)
{
    public override bool RequiresBrowserRefresh => true;

    public override HotReloadClients CreateClients(BrowserRefreshServer? browserRefreshServer, ILogger clientLogger, ILogger agentLogger)
    {
        if (browserRefreshServer == null)
        {
            // error has been reported earlier
            return HotReloadClients.Empty;
        }

        return new(new BlazorWebAssemblyHotReloadClient(clientLogger, agentLogger, browserRefreshServer, environmentOptions, clientProject));
    }
}
