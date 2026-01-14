// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Default model.
/// </summary>
internal sealed class DefaultAppModel(ProjectGraphNode project, int hotReloadHttpPort) : HotReloadAppModel
{
    public override ValueTask<HotReloadClients?> TryCreateClientsAsync(ILogger clientLogger, ILogger agentLogger, CancellationToken cancellationToken)
    {
        HotReloadClient client;

        var isMobile = project.IsMobilePlatform();
        clientLogger.LogDebug("IsMobilePlatform={IsMobile}, TargetPlatformIdentifier={TargetPlatformIdentifier}",
            isMobile,
            project.ProjectInstance.GetPropertyValue(PropertyNames.TargetPlatformIdentifier));

        // Use HTTP transport for mobile platforms (Android, iOS, MacCatalyst)
        // Named pipes don't work over the network for remote device scenarios
        if (isMobile)
        {
            // For HTTP transport, the hot reload agent is built into the app via the platform workload,
            // so we don't pass the startup hook path.
            client = new HttpHotReloadClient(clientLogger, agentLogger, enableStaticAssetUpdates: true, hotReloadHttpPort);
        }
        else
        {
            client = new DefaultHotReloadClient(clientLogger, agentLogger, GetStartupHookPath(project), enableStaticAssetUpdates: true);
        }

        return new(new HotReloadClients(client, browserRefreshServer: null));
    }
}
