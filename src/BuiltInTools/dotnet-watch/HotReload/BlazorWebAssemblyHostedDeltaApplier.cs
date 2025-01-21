// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watch
{
    internal sealed class BlazorWebAssemblyHostedDeltaApplier(IReporter reporter, BrowserRefreshServer browserRefreshServer, ProjectGraphNode project) : DeltaApplier(reporter)
    {
        private readonly BlazorWebAssemblyDeltaApplier _wasmApplier = new(reporter, browserRefreshServer, project);
        private readonly DefaultDeltaApplier _hostApplier = new(reporter);

        public override void Dispose()
        {
            _hostApplier.Dispose();
            _wasmApplier.Dispose();
        }

        public override void CreateConnection(string namedPipeName, CancellationToken cancellationToken)
        {
            _wasmApplier.CreateConnection(namedPipeName, cancellationToken);
            _hostApplier.CreateConnection(namedPipeName, cancellationToken);
        }

        public override Task WaitForProcessRunningAsync(CancellationToken cancellationToken)
            // We only need to wait for any of the app processes to start, so wait for the host.
            // We do not need to wait for the browser connection to be established.
            => _hostApplier.WaitForProcessRunningAsync(cancellationToken);

        public override async Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        {
            var result = await Task.WhenAll(
                _wasmApplier.GetApplyUpdateCapabilitiesAsync(cancellationToken),
                _hostApplier.GetApplyUpdateCapabilitiesAsync(cancellationToken));

            // Allow updates that are supported by at least one process.
            // When applying changes we will filter updates applied to a specific process based on their required capabilities.
            return result[0].Union(result[1], StringComparer.OrdinalIgnoreCase).ToImmutableArray();
        }

        public override async Task<ApplyStatus> ApplyManagedCodeUpdates(ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            // Apply to both processes.
            // The module the change is for does not need to be loaded in either of the processes, yet we still consider it successful if the application does not fail.
            // In each process we store the deltas for application when/if the module is loaded to the process later.
            // An error is only reported if the delta application fails, which would be a bug either in the runtime (applying valid delta incorrectly),
            // the compiler (producing wrong delta), or rude edit detection (the change shouldn't have been allowed).

            var result = await Task.WhenAll(
                _wasmApplier.ApplyManagedCodeUpdates(updates, cancellationToken),
                _hostApplier.ApplyManagedCodeUpdates(updates, cancellationToken));

            var wasmResult = result[0];
            var hostResult = result[1];

            ReportStatus(wasmResult, "client");
            ReportStatus(hostResult, "host");

            return (wasmResult, hostResult) switch
            {
                (ApplyStatus.Failed, _) or (_, ApplyStatus.Failed) => ApplyStatus.Failed,
                (ApplyStatus.NoChangesApplied, ApplyStatus.NoChangesApplied) => ApplyStatus.NoChangesApplied,
                (ApplyStatus.AllChangesApplied, ApplyStatus.AllChangesApplied) => ApplyStatus.AllChangesApplied,
                _ => ApplyStatus.SomeChangesApplied,
            };

            void ReportStatus(ApplyStatus status, string target)
            {
                if (status == ApplyStatus.NoChangesApplied)
                {
                    Reporter.Warn($"No changes applied to {target} because they are not supported by the runtime.");
                }
                else if (status == ApplyStatus.SomeChangesApplied)
                {
                    Reporter.Verbose($"Some changes not applied to {target} because they are not supported by the runtime.");
                }
            }
        }

        public override Task<ApplyStatus> ApplyStaticAssetUpdates(ImmutableArray<StaticAssetUpdate> updates, CancellationToken cancellationToken)
            // static asset updates are handled by browser refresh server:
            => Task.FromResult(ApplyStatus.NoChangesApplied);

        public override Task InitialUpdatesApplied(CancellationToken cancellationToken)
            => _hostApplier.InitialUpdatesApplied(cancellationToken);

        public override Task<bool> TryTerminateProcessAsync(CancellationToken cancellationToken)
            => _hostApplier.TryTerminateProcessAsync(cancellationToken);
    }
}
