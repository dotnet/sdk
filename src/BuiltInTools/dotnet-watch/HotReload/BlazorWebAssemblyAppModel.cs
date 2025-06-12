// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Blazor client-only WebAssembly app.
/// </summary>
internal sealed class BlazorWebAssemblyAppModel(ProjectGraphNode clientProject)
    // Blazor WASM does not need agent injected as all changes are applied in the browser, the process being launched is a dev server.
    : HotReloadAppModel(agentInjectionProject: null)
{
    public override bool RequiresBrowserRefresh => true;

    public override DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, IReporter processReporter)
    {
        if (browserRefreshServer == null)
        {
            // error has been reported earlier
            return null;
        }

        return new BlazorWebAssemblyDeltaApplier(processReporter, browserRefreshServer, clientProject);
    }
}
