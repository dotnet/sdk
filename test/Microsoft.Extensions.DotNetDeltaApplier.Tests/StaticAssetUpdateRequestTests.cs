// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class StaticAssetUpdateRequestTests
{
    [TestMethod]
    public async Task Roundtrip()
    {
        var initial = new StaticAssetUpdateRequest(
            new RuntimeStaticAssetUpdate(
                assemblyName: "assembly name",
                relativePath: "some path",
                [1, 2, 3],
                isApplicationProject: true),
            responseLoggingLevel: ResponseLoggingLevel.WarningsAndErrors);

        using var stream = new MemoryStream();
        await initial.WriteAsync(stream, CancellationToken.None);

        stream.Position = 0;
        var read = await StaticAssetUpdateRequest.ReadAsync(stream, CancellationToken.None);

        AssertEqual(initial, read);
    }

    private static void AssertEqual(StaticAssetUpdateRequest initial, StaticAssetUpdateRequest read)
    {
        Assert.AreEqual(initial.Update.AssemblyName, read.Update.AssemblyName);
        Assert.AreEqual(initial.Update.RelativePath, read.Update.RelativePath);
        Assert.AreEqual(initial.Update.IsApplicationProject, read.Update.IsApplicationProject);
        Assert.AreSequenceEqual(initial.Update.Contents, read.Update.Contents);
    }
}
