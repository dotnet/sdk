// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    internal sealed class BlazorWebAssemblyHotReloadClient(ILogger logger, ILogger agentLogger, BrowserRefreshServer browserRefreshServer, EnvironmentOptions environmentOptions, ProjectGraphNode project)
        : HotReloadClient(logger, agentLogger)
    {
        private static readonly ImmutableArray<string> s_defaultCapabilities60 =
            ["Baseline"];

        private static readonly ImmutableArray<string> s_defaultCapabilities70 =
            ["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType", "NewTypeDefinition", "ChangeCustomAttributes"];

        private static readonly ImmutableArray<string> s_defaultCapabilities80 =
            ["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType", "NewTypeDefinition", "ChangeCustomAttributes",
             "AddInstanceFieldToExistingType", "GenericAddMethodToExistingType", "GenericUpdateMethod", "UpdateParameters", "GenericAddFieldToExistingType"];

        private static readonly ImmutableArray<string> s_defaultCapabilities90 =
            s_defaultCapabilities80;

        private int _updateId;

        /// <summary>
        /// Updates that were sent over to the agent while the process has been suspended.
        /// </summary>
        private readonly Queue<int> _pendingUpdates = [];

        public override void Dispose()
        {
            // Do nothing.
        }

        public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
        {
            // the environment is configued via browser refesh server
        }

        public override void InitiateConnection(CancellationToken cancellationToken)
        {
        }

        public override async Task WaitForConnectionEstablishedAsync(CancellationToken cancellationToken)
            // Wait for the browser connection to be established. Currently we need the browser to be running in order to apply changes.
            => await browserRefreshServer.WaitForClientConnectionAsync(cancellationToken);

        public override Task<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        {
            var capabilities = project.GetWebAssemblyCapabilities().ToImmutableArray();

            if (capabilities.IsEmpty)
            {
                var targetFramework = project.GetTargetFrameworkVersion();

                Logger.LogDebug("Using capabilities based on project target framework: '{TargetFramework}'.", targetFramework);

                capabilities = targetFramework?.Major switch
                {
                    9 => s_defaultCapabilities90,
                    8 => s_defaultCapabilities80,
                    7 => s_defaultCapabilities70,
                    6 => s_defaultCapabilities60,
                    _ => [],
                };
            }
            else
            {
                Logger.LogDebug("Project specifies capabilities: '{Capabilities}'", string.Join(' ', capabilities));
            }

            return Task.FromResult(capabilities);
        }

        public override async Task<ApplyStatus> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
        {
            var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                return ApplyStatus.NoChangesApplied;
            }

            if (environmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser))
            {
                // When testing abstract away the browser and pretend all changes have been applied:
                return ApplyStatus.AllChangesApplied;
            }

            if (!isProcessSuspended)
            {
                await ProcessPendingUpdatesAsync(cancellationToken);
            }

            var anySuccess = false;
            var anyFailure = false;

            // Make sure to send the same update to all browsers, the only difference is the shared secret.

            var updateId = _updateId++;
            Logger.LogDebug("Sending update #{UpdateId}", updateId);

            var deltas = updates.Select(static update => new JsonDelta
            {
                ModuleId = update.ModuleId,
                MetadataDelta = ImmutableCollectionsMarshal.AsArray(update.MetadataDelta)!,
                ILDelta = ImmutableCollectionsMarshal.AsArray(update.ILDelta)!,
                PdbDelta = ImmutableCollectionsMarshal.AsArray(update.PdbDelta)!,
                UpdatedTypes = ImmutableCollectionsMarshal.AsArray(update.UpdatedTypes)!,
            }).ToArray();

            var loggingLevel = Logger.IsEnabled(LogLevel.Debug) ? ResponseLoggingLevel.Verbose : ResponseLoggingLevel.WarningsAndErrors;

            await browserRefreshServer.SendAndReceiveAsync(
                request: sharedSecret => new JsonApplyHotReloadDeltasRequest
                {
                    SharedSecret = sharedSecret,
                    UpdateId = updateId,
                    Deltas = deltas,
                    ResponseLoggingLevel = (int)loggingLevel
                },
                response: isProcessSuspended ? null : (value, logger) =>
                {
                    if (ProcessUpdateResponse(value, logger))
                    {
                        anySuccess = true;
                    }
                    else
                    {
                        anyFailure = true;
                    }
                },
                cancellationToken);

            if (isProcessSuspended)
            {
                Logger.LogDebug("Update #{UpdateId} will be completed after app resumes.", updateId);
                _pendingUpdates.Enqueue(updateId);
            }

            // If no browser is connected we assume the changes have been applied.
            // If at least one browser suceeds we consider the changes successfully applied.
            // TODO: 
            // The refresh server should remember the deltas and apply them to browsers connected in future.
            // Currently the changes are remembered on the dev server and sent over there from the browser.
            // If no browser is connected the changes are not sent though.
            return (!anySuccess && anyFailure) ? ApplyStatus.Failed : (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        public override Task<ApplyStatus> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
            // static asset updates are handled by browser refresh server:
            => Task.FromResult(ApplyStatus.NoChangesApplied);

        private async ValueTask ProcessPendingUpdatesAsync(CancellationToken cancellationToken)
        {
            while (_pendingUpdates.Count > 0)
            {
                var updateId = _pendingUpdates.Dequeue();
                var success = false;

                await browserRefreshServer.SendAndReceiveAsync<object?>(
                    request: null,
                    response: (value, logger) => success = ProcessUpdateResponse(value, logger),
                    cancellationToken);

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

        private static bool ProcessUpdateResponse(ReadOnlySpan<byte> value, ILogger logger)
        {
            var data = BrowserRefreshServer.DeserializeJson<JsonApplyDeltasResponse>(value);

            foreach (var entry in data.Log)
            {
                ReportLogEntry(logger, entry.Message, (AgentMessageSeverity)entry.Severity);
            }

            return data.Success;
        }

        public override Task InitialUpdatesAppliedAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        private readonly struct JsonApplyHotReloadDeltasRequest
        {
            public string Type => "BlazorHotReloadDeltav3";
            public string? SharedSecret { get; init; }

            public int UpdateId { get; init; }
            public JsonDelta[] Deltas { get; init; }
            public int ResponseLoggingLevel { get; init; }
        }

        private readonly struct JsonDelta
        {
            public Guid ModuleId { get; init; }
            public byte[] MetadataDelta { get; init; }
            public byte[] ILDelta { get; init; }
            public byte[] PdbDelta { get; init; }
            public int[] UpdatedTypes { get; init; }
        }

        private readonly struct JsonApplyDeltasResponse
        {
            public bool Success { get; init; }
            public IEnumerable<JsonLogEntry> Log { get; init; }
        }

        private readonly struct JsonLogEntry
        {
            public string Message { get; init; }
            public int Severity { get; init; }
        }

        private readonly struct JsonGetApplyUpdateCapabilitiesRequest
        {
            public string Type => "BlazorRequestApplyUpdateCapabilities2";
        }
    }
}
