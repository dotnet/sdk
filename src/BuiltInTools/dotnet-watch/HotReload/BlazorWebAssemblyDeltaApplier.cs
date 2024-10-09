// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.WebSockets;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class BlazorWebAssemblyDeltaApplier(IReporter reporter, BrowserRefreshServer browserRefreshServer, Version? targetFrameworkVersion) : SingleProcessDeltaApplier(reporter)
    {
        private const string DefaultCapabilities60 = "Baseline";
        private const string DefaultCapabilities70 = "Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes";
        private const string DefaultCapabilities80 = "Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes AddInstanceFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod UpdateParameters GenericAddFieldToExistingType";

        private ImmutableArray<string> _cachedCapabilities;
        private readonly SemaphoreSlim _capabilityRetrievalSemaphore = new(initialCount: 1);
        private int _sequenceId;

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

                        await browserRefreshServer.SendAndReceive(
                            request: _ => default(BlazorRequestApplyUpdateCapabilities),
                            response: value =>
                            {
                                var str = Encoding.UTF8.GetString(value);
                                if (str.StartsWith('!'))
                                {
                                    Reporter.Verbose($"Exception while reading WASM runtime capabilities: {str[1..]}");
                                }
                                else if (str.Length == 0)
                                {
                                    Reporter.Verbose($"Unable to read WASM runtime capabilities");
                                }
                                else if (capabilityString == null)
                                {
                                    capabilityString = str;
                                }
                                else if (capabilityString != str)
                                {
                                    Reporter.Verbose($"Received different capabilities from different browsers:{Environment.NewLine}'{str}'{Environment.NewLine}'{capabilityString}'");
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

            await browserRefreshServer.SendAndReceive(
                request: sharedSecret => new UpdatePayload
                {
                    SharedSecret = sharedSecret,
                    Deltas = updates.Select(update => new UpdateDelta
                    {
                        SequenceId = _sequenceId++,
                        ModuleId = update.ModuleId,
                        MetadataDelta = update.MetadataDelta.ToArray(),
                        ILDelta = update.ILDelta.ToArray(),
                        UpdatedTypes = update.UpdatedTypes.ToArray(),
                    })
                },
                response: value =>
                {
                    if (value is [])
                    {
                        Reporter.Verbose($"Unexpected response length: {value.Length}");
                        anyFailure = true;
                        return;
                    }

                    var status = value[0];
                    if (status == 1)
                    {
                        if (value.Length > 1)
                        {
                            Reporter.Verbose($"Unexpected response length: {value.Length}");
                        }

                        anySuccess = true;
                        return;
                    }

                    if (value.Length > 1)
                    {
                        Reporter.Error("Browser failed to apply the change and reported error:");
                        Reporter.Error(Encoding.UTF8.GetString(value[1..]).Replace("\0", Environment.NewLine));
                    }

                    anyFailure = true;
                },
                cancellationToken);

            // If no browser is connected we assume the changes have been applied.
            // If at least one browser suceeds we consider the changes successfully applied.
            // TODO: The refresh server should remember the deltas and apply them to browsers connected in future.
            return (!anySuccess && anyFailure) ? ApplyStatus.Failed : (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private readonly struct UpdatePayload
        {
            public string Type => "BlazorHotReloadDeltav2";
            public string? SharedSecret { get; init; }
            public IEnumerable<UpdateDelta> Deltas { get; init; }
        }

        private readonly struct UpdateDelta
        {
            public int SequenceId { get; init; }
            public string ServerId { get; init; }
            public Guid ModuleId { get; init; }
            public byte[] MetadataDelta { get; init; }
            public byte[] ILDelta { get; init; }
            public int[] UpdatedTypes { get; init; }
        }

        private readonly struct BlazorRequestApplyUpdateCapabilities
        {
            public string Type => "BlazorRequestApplyUpdateCapabilities2";
        }
    }
}
