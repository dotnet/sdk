// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class MobileAppModel(DotNetWatchContext context, ProjectGraphNode project) : HotReloadAppModel
{
    public override ValueTask<HotReloadClients?> TryCreateClientsAsync(ILogger clientLogger, ILogger agentLogger, CancellationToken cancellationToken)
        // Use WebSocket transport for mobile platforms (Android, iOS)
        // Named pipes don't work over the network for remote device scenarios.
        // Pass the startup hook path so it can be included in the environment variables
        // passed via `dotnet run -e` as @(RuntimeEnvironmentVariable) items.
        => new(new HotReloadClients(new MobileHotReloadClient(clientLogger, agentLogger, context.EnvironmentOptions.HotReloadHttpPort, GetStartupHookPath(project)), browserRefreshServer: null));
}
