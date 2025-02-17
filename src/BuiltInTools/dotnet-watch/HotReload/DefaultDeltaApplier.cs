// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch
{
    internal sealed class DefaultDeltaApplier(IReporter reporter) : SingleProcessDeltaApplier(reporter)
    {
        private Task<ImmutableArray<string>>? _capabilitiesTask;
        private NamedPipeServerStream? _pipe;
        private bool _changeApplicationErrorFailed;

        public override void CreateConnection(string namedPipeName, CancellationToken cancellationToken)
        {
            _pipe = new NamedPipeServerStream(namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            // It is important to establish the connection (WaitForConnectionAsync) before we return,
            // otherwise the client wouldn't be able to connect.
            // However, we don't want to wait for the task to complete, so that we can start the client process.
            _capabilitiesTask = ConnectAsync();

            async Task<ImmutableArray<string>> ConnectAsync()
            {
                try
                {
                    Reporter.Verbose($"Waiting for application to connect to pipe {namedPipeName}.");

                    await _pipe.WaitForConnectionAsync(cancellationToken);

                    // When the client connects, the first payload it sends is the initialization payload which includes the apply capabilities.

                    var capabilities = (await ClientInitializationRequest.ReadAsync(_pipe, cancellationToken)).Capabilities;
                    Reporter.Verbose($"Capabilities: '{capabilities}'");
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
                        Reporter.Error($"Failed to read capabilities: {e.Message}");
                    }

                    return [];
                }
            }
        }

        public override Task WaitForProcessRunningAsync(CancellationToken cancellationToken)
            // Should only be called after CreateConnection
            => _capabilitiesTask ?? throw new InvalidOperationException();

        public override Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken)
            // Should only be called after CreateConnection
            => _capabilitiesTask ?? throw new InvalidOperationException();

        public override async Task<ApplyStatus> Apply(ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            // Should only be called after CreateConnection
            Debug.Assert(_capabilitiesTask != null);

            // Should not be disposed:
            Debug.Assert(_pipe != null);

            if (_changeApplicationErrorFailed)
            {
                Reporter.Verbose("Previous changes failed to apply. Further changes are not applied to this process.", "🔥");
                return ApplyStatus.Failed;
            }

            var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                Reporter.Verbose("No updates applicable to this process", "🔥");
                return ApplyStatus.NoChangesApplied;
            }

            var request = new ManagedCodeUpdateRequest(
                deltas: [.. applicableUpdates.Select(update => new UpdateDelta(
                    update.ModuleId,
                    metadataDelta: [.. update.MetadataDelta],
                    ilDelta: [.. update.ILDelta],
                    pdbDelta: [.. update.PdbDelta],
                    updatedTypes: [.. update.UpdatedTypes]))],
                responseLoggingLevel: Reporter.IsVerbose ? ResponseLoggingLevel.Verbose : ResponseLoggingLevel.WarningsAndErrors);

            var success = false;
            var canceled = false;
            try
            {
                await request.WriteAsync(_pipe, cancellationToken);
                await _pipe.FlushAsync(cancellationToken);

                (success, var log) = await UpdateResponse.ReadAsync(_pipe, cancellationToken);

                await foreach (var (message, severity) in log)
                {
                    ReportLogEntry(Reporter, message, severity);
                }
            }
            catch (OperationCanceledException) when (!(canceled = true))
            {
                // unreachable
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                success = false;
                Reporter.Error($"Change failed to apply (error: '{e.Message}'). Further changes won't be applied to this process.");
                Reporter.Verbose($"Exception stack trace: {e.StackTrace}", "❌");
            }
            finally
            {
                if (!success)
                {
                    if (canceled)
                    {
                        Reporter.Verbose("Change application cancelled. Further changes won't be applied to this process.", "🔥");
                    }

                    _changeApplicationErrorFailed = true;

                    DisposePipe();
                }
            }

            Reporter.Report(MessageDescriptor.UpdatesApplied, applicableUpdates.Count, updates.Length);

            return
                !success ? ApplyStatus.Failed :
                (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private void DisposePipe()
        {
            Reporter.Verbose("Disposing agent communication pipe");
            _pipe?.Dispose();
            _pipe = null;
        }

        public override void Dispose()
        {
            DisposePipe();
        }
    }
}
