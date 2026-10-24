// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class WebAssemblyHotReloadClientTests
{
    /// <summary>
    /// Updates produced while no browser is connected are retained so that a browser connecting
    /// later replays them over the authenticated WebSocket.
    /// </summary>
    [TestMethod]
    public async Task NoBrowser_RetainsAndAcceptsUpdate()
    {
        using var server = new TestBrowserRefreshServer();
        var epoch = server.ResetUpdates();
        using var client = CreateClient(server, epoch);

        var result = await await client.ApplyManagedCodeUpdatesAsync(
            [CreateUpdate()],
            CancellationToken.None,
            CancellationToken.None);

        Assert.IsTrue(result);
        Assert.HasCount(1, server.GetRetainedUpdates());
        Assert.HasCount(1, server.SentMessages);

        using var message = JsonDocument.Parse(server.SentMessages[0]);
        Assert.AreEqual("ApplyManagedCodeUpdates", message.RootElement.GetProperty("type").GetString());

        // The wire contract carries no session, generation or update identity: the authenticated
        // connection lifecycle is what establishes which baseline a message belongs to.
        Assert.IsFalse(message.RootElement.TryGetProperty("generationId", out _));
        Assert.IsFalse(message.RootElement.TryGetProperty("updateId", out _));
    }

    /// <summary>
    /// A full baseline rebuild clears the replay state. Updates still in flight from the client of
    /// the superseded baseline must not reach a browser or pollute the new baseline's replay.
    /// </summary>
    [TestMethod]
    public async Task BaselineReset_DropsUpdatesFromSupersededBaseline()
    {
        using var server = new TestBrowserRefreshServer();
        var staleEpoch = server.ResetUpdates();
        using var staleClient = CreateClient(server, staleEpoch);

        var newEpoch = server.ResetUpdates();
        Assert.AreNotEqual(staleEpoch, newEpoch);

        var result = await await staleClient.ApplyManagedCodeUpdatesAsync(
            [CreateUpdate()],
            CancellationToken.None,
            CancellationToken.None);

        // The batch is dropped rather than failed: the new baseline already contains the change.
        Assert.IsTrue(result);
        Assert.IsEmpty(server.GetRetainedUpdates());
        Assert.IsEmpty(server.SentMessages);
    }

    [TestMethod]
    public async Task BaselineReset_ClearsRetainedUpdates()
    {
        using var server = new TestBrowserRefreshServer();
        var epoch = server.ResetUpdates();
        using var client = CreateClient(server, epoch);

        await await client.ApplyManagedCodeUpdatesAsync(
            [CreateUpdate()],
            CancellationToken.None,
            CancellationToken.None);

        Assert.HasCount(1, server.GetRetainedUpdates());

        server.ResetUpdates();

        Assert.IsEmpty(server.GetRetainedUpdates());
    }

    private static WebAssemblyHotReloadClient CreateClient(TestBrowserRefreshServer server, int baselineEpoch)
        => new(
            new TestLogger(),
            new TestLogger(),
            server,
            baselineEpoch,
            ["Baseline"],
            new Version(11, 0),
            suppressBrowserRequestsForTesting: false);

    private static HotReloadManagedCodeUpdate CreateUpdate()
        => new(
            moduleId: Guid.NewGuid(),
            metadataDelta: [1],
            ilDelta: [2],
            pdbDelta: [3],
            updatedTypes: [],
            requiredCapabilities: ImmutableArray.Create("Baseline"));
}
