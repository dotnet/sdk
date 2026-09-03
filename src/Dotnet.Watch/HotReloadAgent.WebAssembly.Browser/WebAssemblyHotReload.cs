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

    private static bool s_initialized;
    private static HotReloadAgent? s_hotReloadAgent;

    [JSExport]
    [SupportedOSPlatform("browser")]
    public static Task InitializeAsync(string baseUri)
    {
        if (MetadataUpdater.IsSupported && Environment.GetEnvironmentVariable("__DOTNET_WATCH_BROWSER_TOOLS") == "true" &&
            OperatingSystem.IsBrowser())
        {
            s_initialized = true;

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

    private static HotReloadAgent? GetAgent()
        => s_hotReloadAgent ?? (s_initialized ? throw new InvalidOperationException("Hot Reload agent not initialized") : null);

    private static LogEntry[] ApplyHotReloadDeltas(Delta[] deltas, int loggingLevel)
    {
        var agent = GetAgent();
        if (agent == null)
        {
            return [];
        }

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
        return GetAgent()?.Capabilities ?? "";
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
