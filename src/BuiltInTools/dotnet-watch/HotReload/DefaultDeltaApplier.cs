// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.HotReload;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DefaultDeltaApplier(IReporter reporter) : SingleProcessDeltaApplier
    {
        private Task<ImmutableArray<string>>? _capabilitiesTask;
        private NamedPipeServerStream? _pipe;

        public override void Initialize(ProjectInfo project, string namedPipeName, CancellationToken cancellationToken)
        {
            base.Initialize(project, namedPipeName, cancellationToken);

            _pipe = new NamedPipeServerStream(namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            _capabilitiesTask = Task.Run(async () =>
            {
                reporter.Verbose($"Connecting to the application.");

                await _pipe.WaitForConnectionAsync(cancellationToken);

                // When the client connects, the first payload it sends is the initialization payload which includes the apply capabilities.

                var capabilities = ClientInitializationPayload.Read(_pipe).Capabilities;
                return capabilities.Split(' ').ToImmutableArray();
            });
        }

        public override Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken)
            => _capabilitiesTask ?? Task.FromResult(ImmutableArray<string>.Empty);

        public override async Task<ApplyStatus> Apply(ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            if (_capabilitiesTask is null || !_capabilitiesTask.IsCompletedSuccessfully || _pipe is null || !_pipe.IsConnected)
            {
                // The client isn't listening
                reporter.Verbose("No client connected to receive delta updates.");
                return ApplyStatus.Failed;
            }

            var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                return ApplyStatus.NoChangesApplied;
            }

            var payload = new UpdatePayload(applicableUpdates.Select(update => new UpdateDelta(
                update.ModuleId,
                metadataDelta: update.MetadataDelta.ToArray(),
                ilDelta: update.ILDelta.ToArray(),
                update.UpdatedTypes.ToArray())).ToArray());

            await payload.WriteAsync(_pipe, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);

            if (!await ReceiveApplyUpdateResult(cancellationToken))
            {
                return ApplyStatus.Failed;
            }

            return (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private async Task<bool> ReceiveApplyUpdateResult(CancellationToken cancellationToken)
        {
            Debug.Assert(_pipe != null);

            var bytes = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                var numBytes = await _pipe.ReadAsync(bytes, cancellationToken);
                if (numBytes != 1)
                {
                    reporter.Verbose($"Apply confirmation: Received {numBytes} bytes.");
                    return false;
                }

                if (bytes[0] != UpdatePayload.ApplySuccessValue)
                {
                    reporter.Verbose($"Apply confirmation: Received value: '{bytes[0]}'.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log it, but we'll treat this as a failed apply.
                reporter.Verbose(ex.Message);
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        public override void Dispose()
        {
            _pipe?.Dispose();
        }
    }
}
