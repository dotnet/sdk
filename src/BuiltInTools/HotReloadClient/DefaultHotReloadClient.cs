// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
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
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload
{
    internal sealed class DefaultHotReloadClient(ILogger logger, ILogger agentLogger, bool enableStaticAssetUpdates) : HotReloadClient(logger, agentLogger)
    {
        private Task<ImmutableArray<string>>? _capabilitiesTask;
        private NamedPipeServerStream? _pipe;
        private bool _managedCodeUpdateFailedOrCancelled;

        private int _updateId;

        /// <summary>
        /// Updates that were sent over to the agent while the process has been suspended.
        /// </summary>
        private readonly Queue<int> _pendingUpdates = [];

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

        public override void InitiateConnection(string namedPipeName, CancellationToken cancellationToken)
        {
#if NET
            var options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
#else
            var options = PipeOptions.Asynchronous;
#endif
            _pipe = new NamedPipeServerStream(namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options);

            // It is important to establish the connection (WaitForConnectionAsync) before we return,
            // otherwise the client wouldn't be able to connect.
            // However, we don't want to wait for the task to complete, so that we can start the client process.
            _capabilitiesTask = ConnectAsync();

            async Task<ImmutableArray<string>> ConnectAsync()
            {
                try
                {
                    Logger.LogDebug("Waiting for application to connect to pipe {NamedPipeName}.", namedPipeName);

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

            if (!isProcessSuspended)
            {
                await ProcessPendingUpdatesAsync(cancellationToken);
            }

            var request = new ManagedCodeUpdateRequest(ToRuntimeUpdates(applicableUpdates), ResponseLoggingLevel);
        
            var success = false;
            var canceled = false;
            try
            {
                success = await SendAndReceiveUpdate(request, isProcessSuspended, cancellationToken);
            }
            catch (OperationCanceledException) when (!(canceled = true))
            {
                // unreachable
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                success = false;
                Logger.LogError("Change failed to apply (error: '{Message}'). Further changes won't be applied to this process.", e.Message);
                Logger.LogDebug("Exception stack trace: {StackTrace}", e.StackTrace);
            }
            finally
            {
                if (!success)
                {
                    if (canceled)
                    {
                        Logger.LogDebug("Change application cancelled. Further changes won't be applied to this process.");
                    }

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

            var updateId = _updateId++;

            foreach (var update in updates)
            {
                var request = new StaticAssetUpdateRequest(
                    new RuntimeStaticAssetUpdate(
                        update.AssemblyName,
                        update.RelativePath,
                        ImmutableCollectionsMarshal.AsArray(update.Content)!,
                        update.IsApplicationProject),
                    ResponseLoggingLevel);

                var success = false;
                var canceled = false;
                try
                {
                    success = await SendAndReceiveUpdate(request, isProcessSuspended, cancellationToken);
                }
                catch (OperationCanceledException) when (!(canceled = true))
                {
                    // unreachable
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    success = false;
                    Logger.LogError("Change failed to apply (error: '{Message}').", e.Message);
                    Logger.LogDebug("Exception stack trace: {StackTrace}", e.StackTrace);
                }
                finally
                {
                    if (canceled)
                    {
                        Logger.LogDebug("Change application cancelled.");
                    }
                }

                if (success)
                {
                    appliedUpdateCount++;
                }
            }

            Logger.LogDebug("Updates applied: {AppliedCount} out of {TotalCount}.", appliedUpdateCount, updates.Length);

            return
                (appliedUpdateCount == 0) ? ApplyStatus.Failed :
                (appliedUpdateCount < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private async ValueTask ProcessPendingUpdatesAsync(CancellationToken cancellationToken)
        {
            while (_pendingUpdates.Count > 0)
            {
                var updateId = _pendingUpdates.Dequeue();
                var success = await ReceiveUpdateResponse(cancellationToken);

                if (success)
                {
                    Logger.LogDebug("Update #{UpdateId} completed.", updateId);
                }
                else
                {
                    Logger.LogDebug("Update #{UpdateId} failed.", updateId);
                }
            }
        }

        private async ValueTask<bool> SendAndReceiveUpdate<TRequest>(TRequest request, bool isProcessSuspended, CancellationToken cancellationToken)
            where TRequest : IUpdateRequest
        {
            // Should not be disposed:
            Debug.Assert(_pipe != null);

            var updateId = _updateId++;
            Logger.LogDebug("Sending update #{UpdateId}", updateId);

            await _pipe.WriteAsync((byte)request.Type, cancellationToken);
            await request.WriteAsync(_pipe, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);

            if (isProcessSuspended)
            {
                Logger.LogDebug("Update #{UpdateId} will be completed after app resumes.", updateId);
                _pendingUpdates.Enqueue(updateId);
                return true;
            }

            return await ReceiveUpdateResponse(cancellationToken);
        }

        private async ValueTask<bool> ReceiveUpdateResponse(CancellationToken cancellationToken)
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

            await _pipe.WriteAsync((byte)RequestType.InitialUpdatesCompleted, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);
        }
    }
}
