// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Microsoft.DotNet.HotReload.WebAssembly.Browser;

/// <summary>
/// Contains methods called by interop. Intended for framework use only, not supported for use in application
/// code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "Hot Reload does not support trimming")]
internal static partial class WebAssemblyHotReload
{
    /// <summary>
    /// For framework use only.
    /// </summary>
    public readonly struct LogEntry
    {
        public string Message { get; init; }
        public int Severity { get; init; }
    }

    /// <summary>
    /// For framework use only.
    /// </summary>
    public readonly struct Delta
    {
        public string ModuleId { get; init; }
        public byte[] MetadataDelta { get; init; }
        public byte[] ILDelta { get; init; }
        public byte[] PdbDelta { get; init; }
        public int[] UpdatedTypes { get; init; }
    }

    private static HotReloadAgent? s_hotReloadAgent;

    /// <summary>
    /// Why <see cref="s_hotReloadAgent"/> is null, so that a caller that asks for a managed code update
    /// gets an actionable error instead of an update that is acknowledged but silently not applied.
    /// </summary>
    private static string? s_unavailableReason;

    [JSExport]
    [SupportedOSPlatform("browser")]
    public static Task InitializeAsync(string baseUri)
    {
        if (!OperatingSystem.IsBrowser())
        {
            s_unavailableReason = "the runtime is not running in a browser";
        }
        else if (Environment.GetEnvironmentVariable("__DOTNET_WATCH_BROWSER_TOOLS") != "true")
        {
            s_unavailableReason = "the app was not started by 'dotnet watch'";
        }
        else if (!MetadataUpdater.IsSupported)
        {
            // Reached when the app was built without the runtime assets that support metadata updates,
            // which happens with stale or mismatched build output. Applying an update would be a no-op.
            s_unavailableReason = "this build of the app does not support runtime metadata updates (System.Reflection.Metadata.MetadataUpdater.IsSupported is false). Rebuild the app and reload the browser";
        }
        else
        {
            // TODO: Implement hotReloadExceptionCreateHandler: https://github.com/dotnet/sdk/issues/51056
            var agent = new HotReloadAgent(assemblyResolvingHandler: null, hotReloadExceptionCreateHandler: null);

            var existingAgent = Interlocked.CompareExchange(ref s_hotReloadAgent, agent, null);
            if (existingAgent != null)
            {
                throw new InvalidOperationException("Hot Reload agent already initialized");
            }
        }

        // Updates applied before the browser connected are replayed by the browser tools client.
        return Task.CompletedTask;
    }

    /// <summary>
    /// The agent, or an exception explaining why managed code updates cannot be applied. Never reports
    /// success without applying, because dotnet-watch reports an update as applied when the browser
    /// acknowledges it without an error.
    /// </summary>
    private static HotReloadAgent GetRequiredAgent()
        => s_hotReloadAgent ?? throw new InvalidOperationException(
            $"Unable to apply managed code updates because {s_unavailableReason ?? "the Hot Reload agent has not been initialized"}.");

    private static LogEntry[] ApplyHotReloadDeltas(Delta[] deltas, int loggingLevel)
    {
        var agent = GetRequiredAgent();

        agent.ApplyManagedCodeUpdates(
            deltas.Select(d => new RuntimeManagedCodeUpdate(Guid.Parse(d.ModuleId, CultureInfo.InvariantCulture), d.MetadataDelta, d.ILDelta, d.PdbDelta, d.UpdatedTypes)));

        return agent.Reporter.GetAndClearLogEntries((ResponseLoggingLevel)loggingLevel)
            .Select(log => new LogEntry() { Message = log.message, Severity = (int)log.severity }).ToArray();
    }

    private static readonly WebAssemblyHotReloadJsonSerializerContext jsonContext = new(new(JsonSerializerDefaults.Web));

    [JSExport]
    [SupportedOSPlatform("browser")]
    public static string GetApplyUpdateCapabilities()
    {
        return s_hotReloadAgent?.Capabilities ?? "";
    }

    [JSExport]
    [SupportedOSPlatform("browser")]
    public static string? ApplyHotReloadDeltas(string deltasJson, int loggingLevel)
    {
        var deltas = JsonSerializer.Deserialize(deltasJson, jsonContext.DeltaArray);
        if (deltas == null)
        {
            return null;
        }

        var result = ApplyHotReloadDeltas(deltas, loggingLevel);
        return result == null ? null : JsonSerializer.Serialize(result, jsonContext.LogEntryArray);
    }
}

[JsonSerializable(typeof(WebAssemblyHotReload.Delta[]))]
[JsonSerializable(typeof(WebAssemblyHotReload.LogEntry[]))]
internal sealed partial class WebAssemblyHotReloadJsonSerializerContext : JsonSerializerContext
{
}
