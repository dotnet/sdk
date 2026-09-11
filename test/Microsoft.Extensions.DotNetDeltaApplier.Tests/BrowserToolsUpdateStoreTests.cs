// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class BrowserToolsUpdateStoreTests
{
    [TestMethod]
    public void GetReplay_InitialGeneration_ReturnsEmptyCurrentGeneration()
    {
        var store = new BrowserToolsUpdateStore();

        var generation = store.GenerationId;
        var replay = store.GetReplay(generation);

        Assert.AreNotEqual(Guid.Empty, generation);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, replay.Status);
        Assert.IsEmpty(replay.Updates);
    }

    [TestMethod]
    public void Append_IncreasingIdsWithGap_PreservesAppendOrder()
    {
        var store = new BrowserToolsUpdateStore();
        var generation = store.GenerationId;
        var firstModuleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondModuleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        store.Append(CreateBatch(generation, updateId: 0, firstModuleId, deltaSeed: 10));
        store.Append(CreateBatch(generation, updateId: 2, secondModuleId, deltaSeed: 20));

        var replay = store.GetReplay(generation);

        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, replay.Status);
        Assert.AreSequenceEqual([0, 2], replay.Updates.Select(static batch => batch.UpdateId));
        Assert.AreSequenceEqual([generation, generation], replay.Updates.Select(static batch => batch.GenerationId));
        Assert.AreSequenceEqual([firstModuleId, secondModuleId], replay.Updates.Select(static batch => batch.Deltas[0].ModuleId));
        AssertDelta(replay.Updates[0].Deltas[0], firstModuleId, expectedSeed: 10);
        AssertDelta(replay.Updates[1].Deltas[0], secondModuleId, expectedSeed: 20);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(1)]
    public void Append_NonIncreasingId_Throws(int rejectedUpdateId)
    {
        var store = new BrowserToolsUpdateStore();
        var generation = store.GenerationId;
        var retainedModuleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var rejectedModuleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        store.Append(CreateBatch(generation, updateId: 2, retainedModuleId, deltaSeed: 30));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => store.Append(CreateBatch(generation, rejectedUpdateId, rejectedModuleId, deltaSeed: 40)));

        var replay = store.GetReplay(generation);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, replay.Status);
        Assert.HasCount(1, replay.Updates);
        Assert.AreEqual(generation, replay.Updates[0].GenerationId);
        Assert.AreEqual(2, replay.Updates[0].UpdateId);
        AssertDelta(replay.Updates[0].Deltas[0], retainedModuleId, expectedSeed: 30);
    }

    [TestMethod]
    public void Append_ForeignGeneration_Throws()
    {
        var store = new BrowserToolsUpdateStore();
        var generation = store.GenerationId;

        Assert.ThrowsExactly<InvalidOperationException>(
            () => store.Append(CreateBatch(Guid.Empty, updateId: 0, Guid.Parse("55555555-5555-5555-5555-555555555555"), deltaSeed: 50)));

        var replay = store.GetReplay(generation);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, replay.Status);
        Assert.IsEmpty(replay.Updates);
    }

    [TestMethod]
    public void GetReplay_ForeignGeneration_ReturnsMismatchWithoutUpdates()
    {
        var store = new BrowserToolsUpdateStore();
        var generation = store.GenerationId;
        store.Append(CreateBatch(generation, updateId: 0, Guid.Parse("66666666-6666-6666-6666-666666666666"), deltaSeed: 60));

        var replay = store.GetReplay(Guid.Empty);

        Assert.AreEqual(BrowserToolsReplayStatus.GenerationMismatch, replay.Status);
        Assert.IsEmpty(replay.Updates);

        var currentReplay = store.GetReplay(generation);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, currentReplay.Status);
        Assert.HasCount(1, currentReplay.Updates);
        Assert.AreEqual(0, currentReplay.Updates[0].UpdateId);
        AssertDelta(
            currentReplay.Updates[0].Deltas[0],
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            expectedSeed: 60);
    }

    [TestMethod]
    public void Reset_ChangesGenerationClearsReplayAndRestartsOrdering()
    {
        var store = new BrowserToolsUpdateStore();
        var oldGeneration = store.GenerationId;
        store.Append(CreateBatch(oldGeneration, updateId: 5, Guid.Parse("77777777-7777-7777-7777-777777777777"), deltaSeed: 70));

        var newGeneration = store.Reset();

        Assert.AreNotEqual(oldGeneration, newGeneration);
        Assert.AreEqual(newGeneration, store.GenerationId);

        var oldReplay = store.GetReplay(oldGeneration);
        Assert.AreEqual(BrowserToolsReplayStatus.GenerationMismatch, oldReplay.Status);
        Assert.IsEmpty(oldReplay.Updates);

        var newReplay = store.GetReplay(newGeneration);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, newReplay.Status);
        Assert.IsEmpty(newReplay.Updates);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => store.Append(CreateBatch(oldGeneration, updateId: 6, Guid.Parse("88888888-8888-8888-8888-888888888888"), deltaSeed: 80)));
        Assert.IsEmpty(store.GetReplay(newGeneration).Updates);

        var restartedModuleId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        store.Append(CreateBatch(newGeneration, updateId: 0, restartedModuleId, deltaSeed: 90));

        var restartedReplay = store.GetReplay(newGeneration);
        Assert.AreEqual(BrowserToolsReplayStatus.CurrentGeneration, restartedReplay.Status);
        Assert.HasCount(1, restartedReplay.Updates);
        Assert.AreEqual(newGeneration, restartedReplay.Updates[0].GenerationId);
        Assert.AreEqual(0, restartedReplay.Updates[0].UpdateId);
        AssertDelta(restartedReplay.Updates[0].Deltas[0], restartedModuleId, expectedSeed: 90);
    }

    private static BrowserToolsUpdateBatch CreateBatch(Guid generationId, int updateId, Guid moduleId, byte deltaSeed)
        => new(
            generationId,
            updateId,
            [
                new BrowserToolsManagedCodeUpdate(
                    moduleId,
                    [deltaSeed, (byte)(deltaSeed + 1)],
                    [(byte)(deltaSeed + 2), (byte)(deltaSeed + 3)],
                    [(byte)(deltaSeed + 4)],
                    [deltaSeed, deltaSeed + 100])
            ]);

    private static void AssertDelta(BrowserToolsManagedCodeUpdate delta, Guid expectedModuleId, byte expectedSeed)
    {
        Assert.AreEqual(expectedModuleId, delta.ModuleId);
        Assert.AreSequenceEqual([expectedSeed, (byte)(expectedSeed + 1)], delta.MetadataDelta);
        Assert.AreSequenceEqual([(byte)(expectedSeed + 2), (byte)(expectedSeed + 3)], delta.ILDelta);
        Assert.AreSequenceEqual([(byte)(expectedSeed + 4)], delta.PdbDelta);
        Assert.AreSequenceEqual([expectedSeed, expectedSeed + 100], delta.UpdatedTypes);
    }
}
