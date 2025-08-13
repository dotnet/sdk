// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal sealed class HotReloadClients(ImmutableArray<(HotReloadClient client, string name)> clients) : IDisposable
{
    public static readonly HotReloadClients Empty = new([]);

    public HotReloadClients(HotReloadClient client)
        : this([(client, "")])
    {
    }

    public bool IsEmpty
        => clients.IsEmpty;

    public void Dispose()
    {
        foreach (var (client, _) in clients)
        {
            client.Dispose();
        }
    }

    internal void InitiateConnection(string namedPipeName, CancellationToken cancellationToken)
    {
        foreach (var (client, _) in clients)
        {
            client.InitiateConnection(namedPipeName, cancellationToken);
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

    public async ValueTask<ApplyStatus> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
    {
        if (clients is [var (singleClient, _)])
        {
            return await singleClient.ApplyManagedCodeUpdatesAsync(updates, isProcessSuspended, cancellationToken);
        }

        // Apply to all processes.
        // The module the change is for does not need to be loaded to any of the processes, yet we still consider it successful if the application does not fail.
        // In each process we store the deltas for application when/if the module is loaded to the process later.
        // An error is only reported if the delta application fails, which would be a bug either in the runtime (applying valid delta incorrectly),
        // the compiler (producing wrong delta), or rude edit detection (the change shouldn't have been allowed).

        var results = await Task.WhenAll(clients.Select(c => c.client.ApplyManagedCodeUpdatesAsync(updates, isProcessSuspended, cancellationToken)));

        var anyFailure = false;
        var anyChangeApplied = false;
        var allChanagesApplied = false;

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
                    anyChangeApplied = true;
                    allChanagesApplied = true;
                    break;

                case ApplyStatus.SomeChangesApplied:
                    anyChangeApplied = true;
                    allChanagesApplied = false;
                    client.Logger.LogWarning("Some changes not applied to {Name} because they are not supported by the runtime.", name);
                    break;

                case ApplyStatus.NoChangesApplied:
                    allChanagesApplied = false;
                    client.Logger.LogWarning("No changes applied to {Name} because they are not supported by the runtime.", name);
                    break;
            }
        }

        return anyFailure ? ApplyStatus.Failed
            : allChanagesApplied ? ApplyStatus.AllChangesApplied
            : anyChangeApplied ? ApplyStatus.SomeChangesApplied
            : ApplyStatus.NoChangesApplied;
    }

    public async ValueTask InitialUpdatesApplied(CancellationToken cancellationToken)
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
}
