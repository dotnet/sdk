// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Watch;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal sealed class HotReloadClients(ImmutableArray<(HotReloadClient client, string name)> clients, BrowserRefreshServer? browserRefreshServer) : IDisposable
{
    public HotReloadClients(HotReloadClient client, BrowserRefreshServer? browserRefreshServer)
        : this([(client, "")], browserRefreshServer)
    {
    }

    public void Dispose()
    {
        foreach (var (client, _) in clients)
        {
            client.Dispose();
        }
    }

    public BrowserRefreshServer? BrowserRefreshServer
        => browserRefreshServer;

    /// <summary>
    /// All clients share the same loggers.
    /// </summary>
    public ILogger ClientLogger
        => clients.First().client.Logger;

    /// <summary>
    /// All clients share the same loggers.
    /// </summary>
    public ILogger AgentLogger
        => clients.First().client.AgentLogger;

    internal void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
    {
        foreach (var (client, _) in clients)
        {
            client.ConfigureLaunchEnvironment(environmentBuilder);
        }

        browserRefreshServer?.ConfigureLaunchEnvironment(environmentBuilder, enableHotReload: true);
    }

    internal void InitiateConnection(CancellationToken cancellationToken)
    {
        foreach (var (client, _) in clients)
        {
            client.InitiateConnection(cancellationToken);
        }
    }

    internal async ValueTask WaitForConnectionEstablishedAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(clients.Select(c => c.client.WaitForConnectionEstablishedAsync(cancellationToken)));
    }

    public async ValueTask<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (clients is [var (singleClient, _)])
        {
            return await singleClient.GetUpdateCapabilitiesAsync(cancellationToken);
        }

        var results = await Task.WhenAll(clients.Select(c => c.client.GetUpdateCapabilitiesAsync(cancellationToken)));

        // Allow updates that are supported by at least one process.
        // When applying changes we will filter updates applied to a specific process based on their required capabilities.
        return [.. results.SelectMany(r => r).Distinct(StringComparer.Ordinal).OrderBy(c => c)];
    }

    public async ValueTask ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
    {
        var anyFailure = false;

        if (clients is [var (singleClient, _)])
        {
            anyFailure = await singleClient.ApplyManagedCodeUpdatesAsync(updates, isProcessSuspended, cancellationToken) == ApplyStatus.Failed;
        }
        else
        {
            // Apply to all processes.
            // The module the change is for does not need to be loaded to any of the processes, yet we still consider it successful if the application does not fail.
            // In each process we store the deltas for application when/if the module is loaded to the process later.
            // An error is only reported if the delta application fails, which would be a bug either in the runtime (applying valid delta incorrectly),
            // the compiler (producing wrong delta), or rude edit detection (the change shouldn't have been allowed).

            var results = await Task.WhenAll(clients.Select(c => c.client.ApplyManagedCodeUpdatesAsync(updates, isProcessSuspended, cancellationToken)));

            var index = 0;
            foreach (var status in results)
            {
                var (client, name) = clients[index++];

                switch (status)
                {
                    case ApplyStatus.Failed:
                        anyFailure = true;
                        break;

                    case ApplyStatus.AllChangesApplied:
                        break;

                    case ApplyStatus.SomeChangesApplied:
                        client.Logger.LogWarning("Some changes not applied to {Name} because they are not supported by the runtime.", name);
                        break;

                    case ApplyStatus.NoChangesApplied:
                        client.Logger.LogWarning("No changes applied to {Name} because they are not supported by the runtime.", name);
                        break;
                }
            }
        }

        if (!anyFailure)
        {
            // all clients share the same loggers, pick any:
            var logger = clients[0].client.Logger;
            logger.Log(LogEvents.HotReloadSucceeded);

            if (browserRefreshServer != null)
            {
                await browserRefreshServer.RefreshBrowserAsync(cancellationToken);
            }
        }
    }

    public async ValueTask InitialUpdatesAppliedAsync(CancellationToken cancellationToken)
    {
        if (clients is [var (singleClient, _)])
        {
            await singleClient.InitialUpdatesAppliedAsync(cancellationToken);
        }
        else
        {
            await Task.WhenAll(clients.Select(c => c.client.InitialUpdatesAppliedAsync(cancellationToken)));
        }
    }

    public async Task ApplyStaticAssetUpdatesAsync(IEnumerable<(string filePath, string relativeUrl, string assemblyName, bool isApplicationProject)> assets, CancellationToken cancellationToken)
    {
        if (browserRefreshServer != null)
        {
            await browserRefreshServer.UpdateStaticAssetsAsync(assets.Select(static a => a.relativeUrl), cancellationToken);
        }
        else
        {
            var updates = new List<HotReloadStaticAssetUpdate>();

            foreach (var (filePath, relativeUrl, assemblyName, isApplicationProject) in assets)
            {
                ImmutableArray<byte> content;
                try
                {
                    content = ImmutableCollectionsMarshal.AsImmutableArray(await File.ReadAllBytesAsync(filePath, cancellationToken));
                }
                catch (Exception e)
                {
                    ClientLogger.LogError("Failed to read file {FilePath}: {Message}", filePath, e.Message);
                    continue;
                }

                updates.Add(new HotReloadStaticAssetUpdate(
                    assemblyName: assemblyName,
                    relativePath: relativeUrl,
                    content: content,
                    isApplicationProject));
            }

            await ApplyStaticAssetUpdatesAsync([.. updates], isProcessSuspended: false, cancellationToken);
        }
    }

    public async ValueTask ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
    {
        if (clients is [var (singleClient, _)])
        {
            await singleClient.ApplyStaticAssetUpdatesAsync(updates, isProcessSuspended, cancellationToken);
        }
        else
        {
            await Task.WhenAll(clients.Select(c => c.client.ApplyStaticAssetUpdatesAsync(updates, isProcessSuspended, cancellationToken)));
        }
    }

    public ValueTask ReportCompilationErrorsInApplicationAsync(ImmutableArray<string> compilationErrors, CancellationToken cancellationToken)
        => browserRefreshServer?.ReportCompilationErrorsInBrowserAsync(compilationErrors, cancellationToken) ?? ValueTask.CompletedTask;
}
