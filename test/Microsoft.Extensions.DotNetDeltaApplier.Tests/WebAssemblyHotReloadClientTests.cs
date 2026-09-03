// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class WebAssemblyHotReloadClientTests
{
    [TestMethod]
    public async Task NoBrowser_RetainsAndAcceptsUpdate()
    {
        using var server = new TestBrowserRefreshServer();
        var generationId = server.BrowserToolsUpdateStore.GenerationId;
        using var client = CreateClient(server, generationId);

        var result = await await client.ApplyManagedCodeUpdatesAsync(
            [CreateUpdate()],
            CancellationToken.None,
            CancellationToken.None);

        Assert.IsTrue(result);
        var replay = server.BrowserToolsUpdateStore.GetReplay(generationId);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, replay.Status);
        Assert.HasCount(1, replay.Updates);
        Assert.HasCount(1, server.SentMessages);
        using var message = JsonDocument.Parse(server.SentMessages[0]);
        Assert.AreEqual(generationId, message.RootElement.GetProperty("generationId").GetGuid());
    }

    private static WebAssemblyHotReloadClient CreateClient(
        TestBrowserRefreshServer server,
        Guid generationId)
        => new(
            new TestLogger(),
            new TestLogger(),
            server,
            generationId,
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
