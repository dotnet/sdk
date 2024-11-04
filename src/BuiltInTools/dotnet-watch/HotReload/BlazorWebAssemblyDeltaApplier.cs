// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watch
{
    internal sealed class BlazorWebAssemblyDeltaApplier(IReporter reporter, BrowserRefreshServer browserRefreshServer, Version? targetFrameworkVersion) : SingleProcessDeltaApplier(reporter)
    {
        private const string DefaultCapabilities60 = "Baseline";
        private const string DefaultCapabilities70 = "Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes";
        private const string DefaultCapabilities80 = "Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes AddInstanceFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod UpdateParameters GenericAddFieldToExistingType";

        private ImmutableArray<string> _cachedCapabilities;
        private readonly SemaphoreSlim _capabilityRetrievalSemaphore = new(initialCount: 1);
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

        public override async Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        {
            var cachedCapabilities = _cachedCapabilities;
            if (!cachedCapabilities.IsDefault)
            {
                return cachedCapabilities;
            }

            await _capabilityRetrievalSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cachedCapabilities.IsDefault)
                {
                    _cachedCapabilities = await RetrieveAsync(cancellationToken);
                }
            }
            finally
            {
                _capabilityRetrievalSemaphore.Release();
            }

            return _cachedCapabilities;

            async Task<ImmutableArray<string>> RetrieveAsync(CancellationToken cancellationToken)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);

                try
                {
                    Reporter.Verbose("Connecting to the browser.");

                    await browserRefreshServer.WaitForClientConnectionAsync(cancellationToken);

                    string capabilities;
                    if (browserRefreshServer.Options.TestFlags.HasFlag(TestFlags.MockBrowser))
                    {
                        // When testing return default capabilities without connecting to an actual browser.
                        capabilities = GetDefaultCapabilities(targetFrameworkVersion);
                    }
                    else
                    {
                        string? capabilityString = null;

                        await browserRefreshServer.SendAndReceiveAsync(
                            request: _ => default(JsonGetApplyUpdateCapabilitiesRequest),
                            response: (value, reporter) =>
                            {
                                var str = Encoding.UTF8.GetString(value);
                                if (str.StartsWith('!'))
                                {
                                    reporter.Verbose($"Exception while reading WASM runtime capabilities: {str[1..]}");
                                }
                                else if (str.Length == 0)
                                {
                                    reporter.Verbose($"Unable to read WASM runtime capabilities");
                                }
                                else if (capabilityString == null)
                                {
                                    capabilityString = str;
                                }
                                else if (capabilityString != str)
                                {
                                    reporter.Verbose($"Received different capabilities from different browsers:{Environment.NewLine}'{str}'{Environment.NewLine}'{capabilityString}'");
                                }
                            },
                            cancellationToken);

                        if (capabilityString != null)
                        {
                            capabilities = capabilityString;
                        }
                        else
                        {
                            capabilities = GetDefaultCapabilities(targetFrameworkVersion);
                            Reporter.Verbose($"Falling back to default WASM capabilities: '{capabilities}'");
                        }
                    }

                    // Capabilities are expressed a space-separated string.
                    // e.g. https://github.com/dotnet/runtime/blob/14343bdc281102bf6fffa1ecdd920221d46761bc/src/coreclr/System.Private.CoreLib/src/System/Reflection/Metadata/AssemblyExtensions.cs#L87
                    return capabilities.Split(' ').ToImmutableArray();
                }
                catch (Exception e) when (!cancellationToken.IsCancellationRequested)
                {
                    Reporter.Error($"Failed to read capabilities: {e.Message}");

                    // Do not attempt to retrieve capabilities again if it fails once, unless the operation is canceled.
                    return [];
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            static string GetDefaultCapabilities(Version? targetFrameworkVersion)
                => targetFrameworkVersion?.Major switch
                {
                    >= 8 => DefaultCapabilities80,
                    >= 7 => DefaultCapabilities70,
                    >= 6 => DefaultCapabilities60,
                    _ => string.Empty,
                };
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
