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
    internal sealed class BlazorWebAssemblyDeltaApplier(IReporter reporter, BrowserRefreshServer browserRefreshServer) : SingleProcessDeltaApplier
    {
        private const string DefaultCapabilities60 = "Baseline";
        private const string DefaultCapabilities70 = "Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes";
        private const string DefaultCapabilities80 = "Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes AddInstanceFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod UpdateParameters GenericAddFieldToExistingType";

        private static Task<ImmutableArray<string>>? s_cachedCapabilties;
        private Version? _targetFrameworkVersion;
        private int _sequenceId;

        public override void Initialize(ProjectInfo project, string namedPipeName, CancellationToken cancellationToken)
        {
            base.Initialize(project, namedPipeName, cancellationToken);
            _targetFrameworkVersion = project.TargetFrameworkVersion;
        }

        public override Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        {
            return s_cachedCapabilties ??= GetApplyUpdateCapabilitiesCoreAsync();

            async Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesCoreAsync()
            {
                reporter.Verbose("Connecting to the browser.");

                await browserRefreshServer.WaitForClientConnectionAsync(cancellationToken);
                await browserRefreshServer.SendJsonSerlialized(default(BlazorRequestApplyUpdateCapabilities), cancellationToken);

                var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);
                try
                {
                    // We'll query the browser and ask it send capabilities.
                    var response = await browserRefreshServer.ReceiveAsync(buffer, cancellationToken);
                    if (!response.HasValue || !response.Value.EndOfMessage || response.Value.MessageType != WebSocketMessageType.Text)
                    {
                        throw new ApplicationException("Unable to connect to the browser refresh server.");
                    }

                    var capabilities = Encoding.UTF8.GetString(buffer.AsSpan(0, response.Value.Count));
                    var shouldFallBackToDefaultCapabilities = false;

                    // error while fetching capabilities from WASM:
                    if (capabilities.StartsWith("!"))
                    {
                        reporter.Verbose($"Exception while reading WASM runtime capabilities: {capabilities[1..]}");
                        shouldFallBackToDefaultCapabilities = true;
                    }
                    else if (capabilities.Length == 0)
                    {
                        reporter.Verbose($"Unable to read WASM runtime capabilities");
                        shouldFallBackToDefaultCapabilities = true;
                    }

                    if (shouldFallBackToDefaultCapabilities)
                    {
                        capabilities = GetDefaultCapabilities(_targetFrameworkVersion);
                        reporter.Verbose($"Falling back to default WASM capabilities: '{capabilities}'");
                    }

                    // Capabilities are expressed a space-separated string.
                    // e.g. https://github.com/dotnet/runtime/blob/14343bdc281102bf6fffa1ecdd920221d46761bc/src/coreclr/System.Private.CoreLib/src/System/Reflection/Metadata/AssemblyExtensions.cs#L87
                    return capabilities.Split(' ').ToImmutableArray();
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
            if (browserRefreshServer is null)
            {
                reporter.Verbose("Unable to send deltas because the browser refresh server is unavailable.");
                return ApplyStatus.Failed;
            }

            var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
            if (applicableUpdates.Count == 0)
            {
                return ApplyStatus.NoChangesApplied;
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
                reporter.Verbose("Apply confirmation: No browser is connected");
                return false;
            }

            if (result is { Count: 1, EndOfMessage: true })
            {
                return buffer[0] == 1;
            }

            reporter.Verbose("Browser failed to apply the change and reported error:");

            buffer = new byte[1024];
            var messageStream = new MemoryStream();

            while (true)
            {
                result = await browserRefresh.ReceiveAsync(buffer, cancellationToken);
                if (result is not { MessageType: WebSocketMessageType.Binary })
                {
                    reporter.Verbose("Failed to receive error message");
                    break;
                }

                messageStream.Write(buffer, 0, result.Value.Count);

                if (result is { EndOfMessage: true })
                {
                    // message and stack trace are separated by '\0'
                    reporter.Verbose(Encoding.UTF8.GetString(messageStream.ToArray()).Replace("\0", Environment.NewLine));
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
