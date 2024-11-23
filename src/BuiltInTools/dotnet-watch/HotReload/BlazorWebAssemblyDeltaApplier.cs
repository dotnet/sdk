// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watch
{
    internal sealed class BlazorWebAssemblyDeltaApplier(IReporter reporter, BrowserRefreshServer browserRefreshServer, ProjectGraphNode project) : SingleProcessDeltaApplier(reporter)
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

        public override void Dispose()
        {
            // Do nothing.
        }

        public override void CreateConnection(string namedPipeName, CancellationToken cancellationToken)
        {
        }

        public override async Task WaitForProcessRunningAsync(CancellationToken cancellationToken)
            // Wait for the browser connection to be established as an indication that the process has started.
            // Alternatively, we could inject agent into blazor-devserver.dll and establish a connection on the named pipe.
            => await browserRefreshServer.WaitForClientConnectionAsync(cancellationToken);

        public override Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        {
            var capabilities = project.GetWebAssemblyCapabilities();

            if (capabilities.IsEmpty)
            {
                var targetFramework = project.GetTargetFrameworkVersion();

                Reporter.Verbose($"Using capabilities based on target framework: '{targetFramework}'.");

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
                Reporter.Verbose($"Project specifies capabilities.");
            }

            return Task.FromResult(capabilities);
        }

        public override async Task<ApplyStatus> Apply(ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                return ApplyStatus.NoChangesApplied;
            }

            if (browserRefreshServer.Options.TestFlags.HasFlag(TestFlags.MockBrowser))
            {
                // When testing abstract away the browser and pretend all changes have been applied:
                return ApplyStatus.AllChangesApplied;
            }

            var anySuccess = false;
            var anyFailure = false;

            // Make sure to send the same update to all browsers, the only difference is the shared secret.

            var updateId = _updateId++;
            var deltas = updates.Select(update => new JsonDelta
            {
                ModuleId = update.ModuleId,
                MetadataDelta = [.. update.MetadataDelta],
                ILDelta = [.. update.ILDelta],
                PdbDelta = [.. update.PdbDelta],
                UpdatedTypes = [.. update.UpdatedTypes],
            }).ToArray();

            var loggingLevel = Reporter.IsVerbose ? ResponseLoggingLevel.Verbose : ResponseLoggingLevel.WarningsAndErrors;

            await browserRefreshServer.SendAndReceiveAsync(
                request: sharedSecret => new JsonApplyHotReloadDeltasRequest
                {
                    SharedSecret = sharedSecret,
                    UpdateId = updateId,
                    Deltas = deltas,
                    ResponseLoggingLevel = (int)loggingLevel
                },
                response: (value, reporter) =>
                {
                    var data = BrowserRefreshServer.DeserializeJson<JsonApplyDeltasResponse>(value);

                    if (data.Success)
                    {
                        anySuccess = true;
                    }
                    else
                    {
                        anyFailure = true;
                    }

                    ReportLog(reporter, data.Log.Select(entry => (entry.Message, (AgentMessageSeverity)entry.Severity)));
                },
                cancellationToken);

            // If no browser is connected we assume the changes have been applied.
            // If at least one browser suceeds we consider the changes successfully applied.
            // TODO: 
            // The refresh server should remember the deltas and apply them to browsers connected in future.
            // Currently the changes are remembered on the dev server and sent over there from the browser.
            // If no browser is connected the changes are not sent though.
            return (!anySuccess && anyFailure) ? ApplyStatus.Failed : (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

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
