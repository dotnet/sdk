// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public sealed class BufferedImageArchiveTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task KeepsSmallArchivesInMemory()
    {
        await using MemoryStream source = new(new byte[128]);
        await using BufferedImageArchive archive = await BufferedImageArchive.CreateAsync(
            source,
            memoryThreshold: 256,
            TestContext.CancellationToken);

        Assert.IsFalse(archive.IsFileBacked);
        Assert.IsInstanceOfType<MemoryStream>(archive.Content);
        Assert.AreEqual(128, archive.Content.Length);
    }

    [TestMethod]
    public async Task SpillsLargeArchivesToADeletedTemporaryFile()
    {
        await using MemoryStream source = new(new byte[256]);
        string path;
        await using (BufferedImageArchive archive = await BufferedImageArchive.CreateAsync(
            source,
            memoryThreshold: 128,
            TestContext.CancellationToken))
        {
            Assert.IsTrue(archive.IsFileBacked);
            FileStream file = Assert.IsInstanceOfType<FileStream>(archive.Content);
            path = file.Name;
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(256, archive.Content.Length);
        }

        Assert.IsFalse(File.Exists(path));
    }
}
