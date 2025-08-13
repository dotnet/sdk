// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Default model.
/// </summary>
internal sealed class DefaultAppModel(ProjectGraphNode project)
    : HotReloadAppModel(agentInjectionProject: project)
{
    public override bool RequiresBrowserRefresh => false;

    public override HotReloadClients CreateClients(BrowserRefreshServer? browserRefreshServer, ILogger processLogger)
        => new(new DefaultHotReloadClient(processLogger, enableStaticAssetUpdates: true));
}
