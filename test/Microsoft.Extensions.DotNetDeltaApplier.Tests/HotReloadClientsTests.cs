// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class HotReloadClientsTests
{
    [TestMethod]
    public async Task ApplyManagedCodeUpdates()
    {
        var client1Apply = new TaskCompletionSource<bool>();
        var client2Apply = new TaskCompletionSource<bool>();

        var client1 = new TestHotReloadClient { ApplyImpl = _ => client1Apply.Task };
        var client2 = new TestHotReloadClient { ApplyImpl = _ => client2Apply.Task };
        using var refreshServer = new TestBrowserRefreshServer();

        using var clients = new HotReloadClients([client1, client2], refreshServer, useRefreshServerToApplyStaticAssets: false);

        var update1 = CreateUpdate();
        var update2 = CreateUpdate();

        var applyTask = await clients.ApplyManagedCodeUpdatesAsync([[update1], [update2]], CancellationToken.None, CancellationToken.None);

        Assert.HasCount(1, client1.ReceivedUpdates);
        Assert.AreEqual(update1.ModuleId, client1.ReceivedUpdates[0].ModuleId);
        Assert.HasCount(1, client2.ReceivedUpdates);
        Assert.AreEqual(update2.ModuleId, client2.ReceivedUpdates[0].ModuleId);
        Assert.IsFalse(applyTask.IsCompleted);
        Assert.IsEmpty(refreshServer.SentMessages);

        client1Apply.SetResult(true);
        Assert.IsEmpty(refreshServer.SentMessages);

        client2Apply.SetResult(true);
        await applyTask;

        Assert.HasCount(1, refreshServer.SentMessages);
        Assert.Contains("RefreshBrowser", refreshServer.SentMessages[0]);
    }

    private static HotReloadManagedCodeUpdate CreateUpdate()
        => new(
            moduleId: Guid.NewGuid(),
            metadataDelta: [1],
            ilDelta: [],
            pdbDelta: [],
            updatedTypes: [],
            requiredCapabilities: ["Baseline"]);
}
