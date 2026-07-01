// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Tracing;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// TEMPORARY telemetry-delivery LISTENER harness (revert before merge).
///
/// Runs ONE emit per process so the AppInsights <c>OperationId</c> (== the
/// captured Activity <c>TraceId</c>) is a unique 1:1 correlation key between
/// the in-process EventListener verdict and the row that later lands (or not)
/// in <c>RawEventsTraces</c>. The whole point is to VALIDATE the listener:
/// the driver records (traceId, predictedVerdict); later we query the
/// dashboard for those exact OperationIds and join — the listener is only
/// trustworthy if predicted ⟺ actual for every row.
///
/// No-op unless DOTNETUP_TELEMETRY_LISTENER_HARNESS=1 so normal test runs
/// skip it. Invoke one-per-process:
///   dotnet exec dotnetup.Tests.dll -method "*Harness_EmitOneAndObserveTransmission*"
/// with env: DOTNETUP_TELEMETRY_LISTENER_HARNESS=1,
///   DOTNETUP_TELEMETRY_SIMULATE_ERROR=&lt;code&gt;,
///   DOTNETUP_TELEMETRY_FLUSH_TIMEOUT_MS=&lt;ms&gt;, DNUP_LEDGER=&lt;append path&gt;.
/// </summary>
public class TelemetryListenerHarness
{
    /// <summary>
    /// Captures every event emitted by the Azure Monitor exporter's
    /// <c>OpenTelemetry-AzureMonitor-Exporter</c> EventSource (v1.4.0), which
    /// fires <c>TransmissionSuccess</c> on HTTP 200 ingestion and
    /// <c>FailedToTransmit</c>/<c>TransmissionFailed</c>/<c>RequestFailed</c>/
    /// <c>PartialContent</c> on failure/partial.
    /// </summary>
    private sealed class ExporterListener : EventListener
    {
        private readonly object _lock = new();
        public List<string> Events { get; } = [];

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name.Contains("AzureMonitor", StringComparison.OrdinalIgnoreCase))
            {
                EnableEvents(source, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            lock (_lock)
            {
                var payload = e.Payload is null ? string.Empty : string.Join("~", e.Payload);
                Events.Add($"{e.EventName}:{payload}");
            }
        }

        public List<string> DistinctNames()
        {
            lock (_lock)
            {
                return Events.Select(s => s.Split(':')[0]).Distinct().ToList();
            }
        }
    }

    [Fact]
    public void Harness_EmitOneAndObserveTransmission()
    {
        if (Environment.GetEnvironmentVariable("DOTNETUP_TELEMETRY_LISTENER_HARNESS") != "1")
        {
            return; // No-op in normal test runs.
        }

        // Construct the listener BEFORE the exporter so OnEventSourceCreated
        // captures the source (also fires for already-created sources).
        using var listener = new ExporterListener();

        var flush = int.TryParse(Environment.GetEnvironmentVariable("DOTNETUP_TELEMETRY_FLUSH_TIMEOUT_MS"), out var f) ? f : 5000;

        var telemetry = DotnetupTelemetry.Instance; // builds provider + exporter here
        Assert.True(telemetry.Enabled, "telemetry must be enabled (do not opt out for the harness)");

        var op = telemetry.StartTrackedCommand("print-env-script");
        var traceId = op.Activity?.TraceId.ToHexString() ?? "NO_ACTIVITY";

        // Reuse the exact simulate-error → classified-exception mapping.
        try
        {
            TelemetryValidationHarness.ThrowIfRequested();
        }
        catch (Exception ex)
        {
            telemetry.RecordException(op, ex);
            op.Tag("exit.code", 1);
        }

        op.EnsureErrorTypeTagged();
        op.Dispose(); // emits the completion LogRecord stamped with traceId as OperationId

        // Observe transmission during the forced flush budget.
        telemetry.FlushWithTimeout(flush);
        Thread.Sleep(250); // let the EventListener drain any trailing events

        var names = listener.DistinctNames();
        var verdict = names.Contains("TransmissionSuccess") ? "DELIVERED"
            : names.Any(n => n is "FailedToTransmit" or "TransmissionFailed" or "RequestFailed" or "PartialContent") ? "FAILED"
            : "NO_SIGNAL";

        var etype = op.Activity?.GetTagItem("error.type") as string ?? string.Empty;
        var line = $"traceId={traceId}|etype={etype}|flush={flush}|verdict={verdict}|events={string.Join(',', names)}";
        Console.WriteLine("LEDGER|" + line);

        var ledgerPath = Environment.GetEnvironmentVariable("DNUP_LEDGER");
        if (!string.IsNullOrEmpty(ledgerPath))
        {
            File.AppendAllText(ledgerPath, line + Environment.NewLine);
        }
    }
}
