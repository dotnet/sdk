// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Default model.
/// </summary>
internal sealed class DefaultAppModel(ProjectGraphNode project)
    : HotReloadAppModel(agentInjectionProject: project)
{
    public override bool RequiresBrowserRefresh => false;

    public override DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, IReporter processReporter)
        => new DefaultDeltaApplier(processReporter);
}
