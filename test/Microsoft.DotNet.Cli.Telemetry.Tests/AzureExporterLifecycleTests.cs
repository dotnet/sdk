// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class AzureExporterLifecycleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LocalShutdownPersistsQueuedActivitiesBeforeReturning()
    {
        using var settings = new ExporterSettingsScope(ci: false);
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler();
        using var provider = scope.CreateProvider(handler);
        for (int index = 0; index < 25; index++)
        {
            using var activity = scope.Emit($"session-{index}");
        }

        provider.Shutdown().Should().BeTrue();

        handler.Payloads.Should().BeEmpty();
        string[] payloads = scope.StoredFiles().Select(File.ReadAllText).ToArray();
        payloads.Should().NotBeEmpty();
        var messages = payloads.SelectMany(RecordingHandler.Parse)
            .Where(envelope => envelope.GetProperty("data").GetProperty("baseType").GetString() == "MessageData");
        messages.Select(message => message.GetProperty("data").GetProperty("baseData")
            .GetProperty("properties").GetProperty("SessionId").GetString())
            .Should().BeEquivalentTo(Enumerable.Range(0, 25).Select(index => $"session-{index}"));
    }

    [TestMethod]
    public async Task LocalShutdownDoesNotWaitForItsBackgroundUpload()
    {
        using var settings = new ExporterSettingsScope(ci: false);
        settings.EnableShutdownDrain();
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = async (payload, cancellation) =>
        {
            await release.Task.WaitAsync(cancellation);
            return RecordingHandler.Accept(payload);
        };
        using var provider = scope.CreateProvider(handler);
        using var activity = scope.Emit("local-shutdown");
        try
        {
            bool completed = await Task.Run(() => provider.Shutdown()).WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            completed.Should().BeTrue();
            scope.StoredFiles().Should().NotBeEmpty("unacknowledged telemetry must remain durable");
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task CiShutdownWaitsForTheUploadResponse()
    {
        using var settings = new ExporterSettingsScope(ci: true);
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = async (payload, cancellation) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellation);
            return RecordingHandler.Accept(payload);
        };
        using var provider = scope.CreateProvider(handler);
        using var activity = scope.Emit("ci-shutdown");
        Task<bool> shutdown = Task.Run(() => provider.Shutdown(20_000));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            shutdown.IsCompleted.Should().BeFalse();
            scope.StoredFiles().Should().BeEmpty();
        }
        finally
        {
            release.TrySetResult();
        }
        (await shutdown.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).Should().BeTrue();
        handler.Payloads.Should().Contain(payload => payload.Contains("ci-shutdown", StringComparison.Ordinal));
        scope.StoredFiles().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(HttpStatusCode.TooManyRequests)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public void CiShutdownRetainsRetryableFailures(HttpStatusCode status)
    {
        using var settings = new ExporterSettingsScope(ci: true);
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler
        {
            Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status)),
        };
        using var provider = scope.CreateProvider(handler);
        using var activity = scope.Emit("retryable-failure");

        provider.Shutdown(20_000).Should().BeTrue();

        handler.Payloads.Should().NotBeEmpty();
        scope.StoredFiles().Select(File.ReadAllText)
            .Should().Contain(payload => payload.Contains("retryable-failure", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ForceFlushAllowsSubsequentBuildRequests()
    {
        using var settings = new ExporterSettingsScope(ci: false);
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler();
        using var provider = scope.CreateProvider(handler);
        using var first = scope.Emit("first-build");

        provider.ForceFlush().Should().BeTrue();
        using var second = scope.Emit("second-build");
        provider.ForceFlush().Should().BeTrue();
        provider.Shutdown().Should().BeTrue();

        handler.Payloads.Should().BeEmpty();
        string persisted = string.Join('\n', scope.StoredFiles().Select(File.ReadAllText));
        persisted.Should().Contain("first-build").And.Contain("second-build");
    }

    [TestMethod]
    public async Task ANewExporterDrainsPreviouslyPersistedTelemetry()
    {
        using var settings = new ExporterSettingsScope(ci: false);
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler();
        using (var provider = scope.CreateProvider(handler))
        {
            using var activity = scope.Emit("previous-invocation");
            provider.Shutdown().Should().BeTrue();
        }
        scope.StoredFiles().Should().NotBeEmpty();
        var uploaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = (payload, _) =>
        {
            uploaded.TrySetResult();
            return Task.FromResult(RecordingHandler.Accept(payload));
        };
        settings.EnableEagerDrain();
        string connection = $"InstrumentationKey={scope.InstrumentationKey};IngestionEndpoint=https://telemetry.invalid/;ApplicationId={Guid.NewGuid()}";
        using var nextProvider = scope.CreateProvider(handler, connection);
        await uploaded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        SpinWait.SpinUntil(() => scope.StoredFiles().Length == 0, TimeSpan.FromSeconds(5)).Should().BeTrue();
        handler.Payloads.Should().Contain(payload => payload.Contains("previous-invocation", StringComparison.Ordinal));
    }
}