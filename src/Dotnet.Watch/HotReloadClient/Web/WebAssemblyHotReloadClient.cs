// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal sealed class WebAssemblyHotReloadClient(
    ILogger logger,
    ILogger agentLogger,
    AbstractBrowserRefreshServer browserRefreshServer,
    int baselineEpoch,
    ImmutableArray<string> projectHotReloadCapabilities,
    Version projectTargetFrameworkVersion,
    bool suppressBrowserRequestsForTesting)
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

    private readonly ImmutableArray<string> _capabilities = GetUpdateCapabilities(logger, projectHotReloadCapabilities, projectTargetFrameworkVersion);

    private static ImmutableArray<string> GetUpdateCapabilities(ILogger logger, ImmutableArray<string> projectHotReloadCapabilities, Version projectTargetFrameworkVersion)
    {
        var capabilities = projectHotReloadCapabilities.IsEmpty
            ? projectTargetFrameworkVersion.Major switch
            {
                9 => s_defaultCapabilities90,
                8 => s_defaultCapabilities80,
                7 => s_defaultCapabilities70,
                6 => s_defaultCapabilities60,
                _ => [],
            }
            : projectHotReloadCapabilities;

        if (capabilities is not [])
        {
            capabilities = AddImplicitCapabilities(capabilities);
        }

        var capabilitiesStr = string.Join(", ", capabilities);
        if (projectHotReloadCapabilities.IsEmpty)
        {
            // Note that this is not possible with SDK 10+ since the WASM SDK always defines the capabilities in the project,
            // but the code is shared with VS and CDK which might not use the latest SDK.
            logger.Log(LogEvents.UsingCapabilitiesBasedOnTargetFrameworkVersion, projectTargetFrameworkVersion, capabilitiesStr);
        }
        else
        {
            logger.Log(LogEvents.ProjectSpecifiesCapabilities, capabilitiesStr);
        }

        return capabilities;
    }

    public override void Dispose()
    {
        // Do nothing.
    }

    public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
    {
        // Note:
        // Microsoft.AspNetCore.Components.WebAssembly.Server.ComponentWebAssemblyConventions expects
        // DOTNET_MODIFIABLE_ASSEMBLIES to be set in the blazor-devserver process, even though we are not performing
        // Hot Reload in this process. The value is converted to the DOTNET-MODIFIABLE-ASSEMBLIES header, which is in
        // turn converted back to an environment variable in the Mono browser runtime loader:
        // https://github.com/dotnet/runtime/blob/342936c5a88653f0f622e9d6cb727a0e59279b31/src/mono/browser/runtime/loader/config.ts#L330
        // .NET 10+ apps set the variable from the Hot Reload agent JS module instead.
        environmentBuilder[AgentEnvironmentVariables.DotNetModifiableAssemblies] = "debug";
    }

    public override void InitiateConnection(CancellationToken cancellationToken)
    {
    }

    public override async Task WaitForConnectionEstablishedAsync(CancellationToken cancellationToken)
        // Wait for the browser connection to be established. Currently we need the browser to be running in order to apply changes.
        => await browserRefreshServer.WaitForClientConnectionAsync(cancellationToken);

    public override Task<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        => Task.FromResult(_capabilities);

    public override async Task<Task<bool>> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, CancellationToken applyOperationCancellationToken, CancellationToken cancellationToken)
    {
        var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
        if (applicableUpdates.Count == 0)
        {
            return Task.FromResult(true);
        }

        // Make sure to send the same update to all browsers, the only difference is the shared secret.
        var deltas = applicableUpdates.Select(static update => new BrowserToolsManagedCodeUpdate(
            update.ModuleId,
            ImmutableCollectionsMarshal.AsArray(update.MetadataDelta)!,
            ImmutableCollectionsMarshal.AsArray(update.ILDelta)!,
            ImmutableCollectionsMarshal.AsArray(update.PdbDelta)!,
            ImmutableCollectionsMarshal.AsArray(update.UpdatedTypes)!)).ToArray();

        var loggingLevel = Logger.IsEnabled(LogLevel.Debug) ? ResponseLoggingLevel.Verbose : ResponseLoggingLevel.WarningsAndErrors;

        return QueueUpdateBatch(
            sendAndReceive: async batchId =>
            {
                // When testing abstract away the browser and pretend all changes have been applied:
                if (suppressBrowserRequestsForTesting)
                {
                    return true;
                }

                var success = await browserRefreshServer.SendManagedCodeUpdateAsync(
                    baselineEpoch,
                    new BrowserToolsUpdateBatch([.. deltas]),
                    request: sharedSecret => new JsonApplyManagedCodeUpdatesRequest
                    {
                        SharedSecret = sharedSecret,
                        Deltas = deltas,
                        ResponseLoggingLevel = (int)loggingLevel
                    },
                    applyOperationCancellationToken);

                Logger.Log(success ? LogEvents.UpdateBatchCompleted : LogEvents.UpdateBatchFailed, batchId);
                return success;
            },
            applyOperationCancellationToken);
    }

    public override Task<Task<bool>> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, CancellationToken applyOperationCancellationToken, CancellationToken cancellationToken)
        // static asset updates are handled by browser refresh server:
        => Task.FromResult(Task.FromResult(true));

    public override Task InitialUpdatesAppliedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private readonly struct JsonApplyManagedCodeUpdatesRequest
    {
        public string Type => "ApplyManagedCodeUpdates";
        public string? SharedSecret { get; init; }

        public BrowserToolsManagedCodeUpdate[] Deltas { get; init; }
        public int ResponseLoggingLevel { get; init; }
    }
}
