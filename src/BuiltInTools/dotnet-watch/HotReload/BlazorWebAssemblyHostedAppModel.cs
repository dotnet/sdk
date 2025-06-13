// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal abstract partial class HotReloadAppModel
{
    /// <summary>
    /// Blazor WebAssembly app hosted by an ASP.NET Core app.
    /// App has a client and server projects and deltas are applied to both processes.
    /// </summary>
    internal sealed class BlazorWebAssemblyHostedAppModel(ProjectGraphNode clientProject) : HotReloadAppModel
    {
        public override bool RequiresBrowserRefresh => true;
        public override bool InjectDeltaApplier => true;

        public override DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, IReporter processReporter)
        {
            if (browserRefreshServer == null)
            {
                // error has been reported earlier
                return null;
            }

            return new BlazorWebAssemblyHostedDeltaApplier(processReporter, browserRefreshServer, clientProject);
        }
    }
}
