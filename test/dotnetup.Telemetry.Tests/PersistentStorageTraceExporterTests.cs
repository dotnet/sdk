// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class PersistentStorageTraceExporterTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task Export_PersistsBeforeRequestingBackgroundDrain()
    {
        var storage = new RecordingBlobStorage();
        var exporter = new PersistentStorageTraceExporter(
            storage,
            Guid.NewGuid().ToString(),
            new Uri("http://127.0.0.1:1/"),
            leasePeriodMilliseconds: 30_000,
            maxBlobsPerDrain: 200);
        using var activity = new Activity("test").Start();
        activity.Stop();
        var batch = new Batch<Activity>([activity], 1);

        exporter.Export(in batch).Should().Be(ExportResult.Success);
        await storage.DrainRequested.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        storage.PersistSequence.Should().Be(1);
        storage.DrainSequence.Should().Be(2);
        exporter.Shutdown(1_000).Should().BeTrue();
    }

    [TestMethod]
    public async Task Export_RequestsBackgroundDrainWhenPersistFails()
    {
        var storage = new RecordingBlobStorage(persistResult: false);
        var exporter = new PersistentStorageTraceExporter(
            storage,
            Guid.NewGuid().ToString(),
            new Uri("http://127.0.0.1:1/"),
            leasePeriodMilliseconds: 30_000,
            maxBlobsPerDrain: 200);
        using var activity = new Activity("test").Start();
        activity.Stop();
        var batch = new Batch<Activity>([activity], 1);

        exporter.Export(in batch).Should().Be(ExportResult.Failure);
        await storage.DrainRequested.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        storage.DrainSequence.Should().BeGreaterThan(storage.PersistSequence);
        exporter.Shutdown(1_000).Should().BeTrue();
    }

    private sealed class RecordingBlobStorage(bool persistResult = true) : ITelemetryBlobStorage
    {
        private int _sequence;

        public TaskCompletionSource DrainRequested { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DrainSequence { get; private set; }

        public int PersistSequence { get; private set; }

        public IEnumerable<ITelemetryBlob> GetBlobs()
        {
            DrainSequence = Interlocked.Increment(ref _sequence);
            DrainRequested.SetResult();
            return [];
        }

        public bool TryPersist(byte[] data)
        {
            PersistSequence = Interlocked.Increment(ref _sequence);
            return persistResult;
        }
    }
}
