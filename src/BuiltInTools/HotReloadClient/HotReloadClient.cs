// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal abstract class HotReloadClient(ILogger logger, ILogger agentLogger) : IDisposable
{
    /// <summary>
    /// List of modules that can't receive changes anymore.
    /// A module is added when a change is requested for it that is not supported by the runtime.
    /// </summary>
    private readonly HashSet<Guid> _frozenModules = [];

    public readonly ILogger Logger = logger;
    public readonly ILogger AgentLogger = agentLogger;

    private int _updateBatchId;

    /// <summary>
    /// Updates that were sent over to the agent while the process has been suspended.
    /// </summary>
    private readonly object _pendingUpdatesGate = new();
    private Task _pendingUpdates = Task.CompletedTask;

    // for testing
    internal Task PendingUpdates
        => _pendingUpdates;

    public abstract void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder);

    /// <summary>
    /// Initiates connection with the agent in the target process.
    /// </summary>
    public abstract void InitiateConnection(CancellationToken cancellationToken);

    /// <summary>
    /// Waits until the connection with the agent is established.
    /// </summary>
    public abstract Task WaitForConnectionEstablishedAsync(CancellationToken cancellationToken);

    public abstract Task<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken);

    public abstract Task<ApplyStatus> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken);
    public abstract Task<ApplyStatus> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken);

    /// <summary>
    /// Notifies the agent that the initial set of updates has been applied and the user code in the process can start executing.
    /// </summary>
    public abstract Task InitialUpdatesAppliedAsync(CancellationToken cancellationToken);

    public abstract void Dispose();

    public static void ReportLogEntry(ILogger logger, string message, AgentMessageSeverity severity)
    {
        var level = severity switch
        {
            AgentMessageSeverity.Error => LogLevel.Error,
            AgentMessageSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Debug
        };

        logger.Log(level, message);
    }

    public async Task<IReadOnlyList<HotReloadManagedCodeUpdate>> FilterApplicableUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, CancellationToken cancellationToken)
    {
        var availableCapabilities = await GetUpdateCapabilitiesAsync(cancellationToken);
        var applicableUpdates = new List<HotReloadManagedCodeUpdate>();

        foreach (var update in updates)
        {
            if (_frozenModules.Contains(update.ModuleId))
            {
                // can't update frozen module:
                continue;
            }

            if (update.RequiredCapabilities.Except(availableCapabilities).Any())
            {
                // required capability not available:
                _frozenModules.Add(update.ModuleId);
            }
            else
            {
                applicableUpdates.Add(update);
            }
        }

        return applicableUpdates;
    }

    protected async ValueTask<TResult> SendAndReceiveUpdateAsync<TResult>(
        Func<int, CancellationToken, ValueTask<TResult>> send,
        bool isProcessSuspended,
        TResult suspendedResult,
        CancellationToken cancellationToken)
        where TResult : struct
    {
        var batchId = _updateBatchId++;

        Task previous;
        lock (_pendingUpdatesGate)
        {
            previous = _pendingUpdates;

            if (isProcessSuspended)
            {
                _pendingUpdates = Task.Run(async () =>
                {
                    await previous;
                    _ = await send(batchId, cancellationToken);
                }, cancellationToken);

                return suspendedResult;
            }
        }

        await previous;
        return await send(batchId, cancellationToken);
    }
}
