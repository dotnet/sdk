// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watch;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload
{
    internal sealed class DefaultHotReloadClient(ILogger logger, ILogger agentLogger, string startupHookPath, bool enableStaticAssetUpdates)
        : HotReloadClient(logger, agentLogger)
    {
        private readonly string _namedPipeName = Guid.NewGuid().ToString("N");

        private Task<ImmutableArray<string>>? _capabilitiesTask;
        private NamedPipeServerStream? _pipe;
        private bool _managedCodeUpdateFailedOrCancelled;

        private int _updateBatchId;

        /// <summary>
        /// Updates that were sent over to the agent while the process has been suspended.
        /// </summary>
        private readonly object _pendingUpdatesGate = new();
        private Task _pendingUpdates = Task.CompletedTask;

        public override void Dispose()
        {
            DisposePipe();
        }

        private void DisposePipe()
        {
            Logger.LogDebug("Disposing agent communication pipe");
            _pipe?.Dispose();
            _pipe = null;
        }

        // for testing
        internal Task PendingUpdates
            => _pendingUpdates;

        // for testing
        internal string NamedPipeName
            => _namedPipeName;

        public override void InitiateConnection(CancellationToken cancellationToken)
        {
#if NET
            var options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
#else
            var options = PipeOptions.Asynchronous;
#endif
            _pipe = new NamedPipeServerStream(_namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options);

            // It is important to establish the connection (WaitForConnectionAsync) before we return,
            // otherwise the client wouldn't be able to connect.
            // However, we don't want to wait for the task to complete, so that we can start the client process.
            _capabilitiesTask = ConnectAsync();

            async Task<ImmutableArray<string>> ConnectAsync()
            {
                try
                {
                    Logger.LogDebug("Waiting for application to connect to pipe {NamedPipeName}.", _namedPipeName);

                    await _pipe.WaitForConnectionAsync(cancellationToken);

                    // When the client connects, the first payload it sends is the initialization payload which includes the apply capabilities.

                    var capabilities = (await ClientInitializationResponse.ReadAsync(_pipe, cancellationToken)).Capabilities;
                    Logger.Log(LogEvents.Capabilities, capabilities);
                    return [.. capabilities.Split(' ')];
                }
                catch (EndOfStreamException)
                {
                    // process terminated before capabilities sent:
                    return [];
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // pipe might throw another exception when forcibly closed on process termination:
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogError("Failed to read capabilities: {Message}", e.Message);
                    }

                    return [];
                }
            }
        }

        [MemberNotNull(nameof(_capabilitiesTask))]
        private Task<ImmutableArray<string>> GetCapabilitiesTask()
            => _capabilitiesTask ?? throw new InvalidOperationException();

        [MemberNotNull(nameof(_pipe))]
        [MemberNotNull(nameof(_capabilitiesTask))]
        private void RequireReadyForUpdates()
        {
            // should only be called after connection has been created:
            _ = GetCapabilitiesTask();

            if (_pipe == null)
                throw new InvalidOperationException("Pipe has been disposed.");
        }

        public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
        {
            environmentBuilder[AgentEnvironmentVariables.DotNetModifiableAssemblies] = "debug";

            // HotReload startup hook should be loaded before any other startup hooks:
            environmentBuilder.InsertListItem(AgentEnvironmentVariables.DotNetStartupHooks, startupHookPath, Path.PathSeparator);

            environmentBuilder[AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName] = _namedPipeName;
        }

        public override Task WaitForConnectionEstablishedAsync(CancellationToken cancellationToken)
            => GetCapabilitiesTask();

        public override Task<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken)
            => GetCapabilitiesTask();

        private ResponseLoggingLevel ResponseLoggingLevel
            => Logger.IsEnabled(LogLevel.Debug) ? ResponseLoggingLevel.Verbose : ResponseLoggingLevel.WarningsAndErrors;

        public override async Task<ApplyStatus> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
        {
            RequireReadyForUpdates();

            if (_managedCodeUpdateFailedOrCancelled)
            {
                Logger.LogDebug("Previous changes failed to apply. Further changes are not applied to this process.");
                return ApplyStatus.Failed;
            }

            var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                Logger.LogDebug("No updates applicable to this process");
                return ApplyStatus.NoChangesApplied;
            }

            var request = new ManagedCodeUpdateRequest(ToRuntimeUpdates(applicableUpdates), ResponseLoggingLevel);

            var success = false;
            try
            {
                success = await SendAndReceiveUpdateAsync(request, isProcessSuspended, cancellationToken);
            }
            finally
            {
                if (!success)
                {
                    Logger.LogWarning("Further changes won't be applied to this process.");
                    _managedCodeUpdateFailedOrCancelled = true;
                    DisposePipe();
                }
            }

            if (success)
            {
                Logger.Log(LogEvents.UpdatesApplied, applicableUpdates.Count, updates.Length);
            }

            return
                !success ? ApplyStatus.Failed :
                (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;

            static ImmutableArray<RuntimeManagedCodeUpdate> ToRuntimeUpdates(IEnumerable<HotReloadManagedCodeUpdate> updates)
                => [.. updates.Select(static update => new RuntimeManagedCodeUpdate(update.ModuleId,
                   ImmutableCollectionsMarshal.AsArray(update.MetadataDelta)!,
                   ImmutableCollectionsMarshal.AsArray(update.ILDelta)!,
                   ImmutableCollectionsMarshal.AsArray(update.PdbDelta)!,
                   ImmutableCollectionsMarshal.AsArray(update.UpdatedTypes)!))];
        }

        public async override Task<ApplyStatus> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
        {
            if (!enableStaticAssetUpdates)
            {
                // The client has no concept of static assets.
                return ApplyStatus.AllChangesApplied;
            }

            RequireReadyForUpdates();

            var appliedUpdateCount = 0;

            foreach (var update in updates)
            {
                var request = new StaticAssetUpdateRequest(
                    new RuntimeStaticAssetUpdate(
                        update.AssemblyName,
                        update.RelativePath,
                        ImmutableCollectionsMarshal.AsArray(update.Content)!,
                        update.IsApplicationProject),
                    ResponseLoggingLevel);

                Logger.LogDebug("Sending static file update request for asset '{Url}'.", update.RelativePath);

                var success = await SendAndReceiveUpdateAsync(request, isProcessSuspended, cancellationToken);
                if (success)
                {
                    appliedUpdateCount++;
                }
            }

            Logger.Log(LogEvents.UpdatesApplied, appliedUpdateCount, updates.Length);

            return
                (appliedUpdateCount == 0) ? ApplyStatus.Failed :
                (appliedUpdateCount < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private async ValueTask<bool> SendAndReceiveUpdateAsync<TRequest>(TRequest request, bool isProcessSuspended, CancellationToken cancellationToken)
            where TRequest : IUpdateRequest
        {
            // Should not be disposed:
            Debug.Assert(_pipe != null);

            var batchId = _updateBatchId++;

            if (!isProcessSuspended)
            {
                return await SendAndReceiveAsync(batchId, cancellationToken);
            }

            lock (_pendingUpdatesGate)
            {
                var previous = _pendingUpdates;

                _pendingUpdates = Task.Run(async () =>
                {
                    await previous;
                    await SendAndReceiveAsync(batchId, cancellationToken);
                }, cancellationToken);
            }

            return true;

            async ValueTask<bool> SendAndReceiveAsync(int batchId, CancellationToken cancellationToken)
            {
                Logger.LogDebug("Sending update batch #{UpdateId}", batchId);

                try
                {
                    await WriteRequestAsync(cancellationToken);

                    if (await ReceiveUpdateResponseAsync(cancellationToken))
                    {
                        Logger.LogDebug("Update batch #{UpdateId} completed.", batchId);
                        return true;
                    }

                    Logger.LogDebug("Update batch #{UpdateId} failed.", batchId);
                }
                catch (Exception e) when (e is not OperationCanceledException || isProcessSuspended)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogDebug("Update batch #{UpdateId} canceled.", batchId);
                    }
                    else
                    {
                        Logger.LogError("Update batch #{UpdateId} failed with error: {Message}", batchId, e.Message);
                        Logger.LogDebug("Update batch #{UpdateId} exception stack trace: {StackTrace}", batchId, e.StackTrace);
                    }
                }

                return false;
            }

            async ValueTask WriteRequestAsync(CancellationToken cancellationToken)
            {
                await _pipe.WriteAsync((byte)request.Type, cancellationToken);
                await request.WriteAsync(_pipe, cancellationToken);
                await _pipe.FlushAsync(cancellationToken);
            }
        }

        private async ValueTask<bool> ReceiveUpdateResponseAsync(CancellationToken cancellationToken)
        {
            // Should not be disposed:
            Debug.Assert(_pipe != null);

            var (success, log) = await UpdateResponse.ReadAsync(_pipe, cancellationToken);

            await foreach (var (message, severity) in log)
            {
                ReportLogEntry(AgentLogger, message, severity);
            }

            return success;
        }

        public override async Task InitialUpdatesAppliedAsync(CancellationToken cancellationToken)
        {
            RequireReadyForUpdates();

            if (_managedCodeUpdateFailedOrCancelled)
            {
                return;
            }

            try
            {
                await _pipe.WriteAsync((byte)RequestType.InitialUpdatesCompleted, cancellationToken);
                await _pipe.FlushAsync(cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // pipe might throw another exception when forcibly closed on process termination:
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogError("Failed to send InitialUpdatesCompleted: {Message}", e.Message);
                }
            }
        }
    }
}
