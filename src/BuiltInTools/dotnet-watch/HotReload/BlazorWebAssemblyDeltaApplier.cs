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
                        await browserRefreshServer.SendJsonSerlialized(default(BlazorRequestApplyUpdateCapabilities), cancellationToken);

                        // We'll query the browser and ask it send capabilities.
                        var response = await browserRefreshServer.ReceiveAsync(buffer, cancellationToken);
                        if (!response.HasValue || !response.Value.EndOfMessage || response.Value.MessageType != WebSocketMessageType.Text)
                        {
                            throw new ApplicationException("Unable to connect to the browser refresh server.");
                        }

                        capabilities = Encoding.UTF8.GetString(buffer.AsSpan(0, response.Value.Count));

                        var shouldFallBackToDefaultCapabilities = false;

                        // error while fetching capabilities from WASM:
                        if (capabilities.StartsWith('!'))
                        {
                            Reporter.Verbose($"Exception while reading WASM runtime capabilities: {capabilities[1..]}");
                            shouldFallBackToDefaultCapabilities = true;
                        }
                        else if (capabilities.Length == 0)
                        {
                            Reporter.Verbose($"Unable to read WASM runtime capabilities");
                            shouldFallBackToDefaultCapabilities = true;
                        }

                        if (shouldFallBackToDefaultCapabilities)
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

            await browserRefreshServer.SendJsonWithSecret(sharedSecret => new UpdatePayload
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
            }, cancellationToken);

            bool result = await ReceiveApplyUpdateResult(browserRefreshServer, cancellationToken);

            return !result ? ApplyStatus.Failed : (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
        }

        private async Task<bool> ReceiveApplyUpdateResult(BrowserRefreshServer browserRefresh, CancellationToken cancellationToken)
        {
            var buffer = new byte[1];

            var result = await browserRefresh.ReceiveAsync(buffer, cancellationToken);
            if (result is not { MessageType: WebSocketMessageType.Binary })
            {
                // A null result indicates no clients are connected. No deltas could have been applied in this state.
                Reporter.Verbose("Apply confirmation: No browser is connected");
                return false;
            }

            if (result is { Count: 1, EndOfMessage: true })
            {
                return buffer[0] == 1;
            }

            Reporter.Verbose("Browser failed to apply the change and reported error:");

            buffer = new byte[1024];
            var messageStream = new MemoryStream();

            while (true)
            {
                result = await browserRefresh.ReceiveAsync(buffer, cancellationToken);
                if (result is not { MessageType: WebSocketMessageType.Binary })
                {
                    Reporter.Verbose("Failed to receive error message");
                    break;
                }

                messageStream.Write(buffer, 0, result.Value.Count);

                if (result is { EndOfMessage: true })
                {
                    // message and stack trace are separated by '\0'
                    Reporter.Verbose(Encoding.UTF8.GetString(messageStream.ToArray()).Replace("\0", Environment.NewLine));
                    break;
                }
            }

            return false;
        }

        public override void Dispose()
        {
            // Do nothing.
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
