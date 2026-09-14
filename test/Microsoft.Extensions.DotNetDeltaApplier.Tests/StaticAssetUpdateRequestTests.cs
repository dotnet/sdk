// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

public class StaticAssetUpdateRequestTests
{
    [Fact]
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
        Assert.Equal(initial.Update.AssemblyName, read.Update.AssemblyName);
        Assert.Equal(initial.Update.RelativePath, read.Update.RelativePath);
        Assert.Equal(initial.Update.IsApplicationProject, read.Update.IsApplicationProject);
        AssertEx.SequenceEqual(initial.Update.Contents, read.Update.Contents);
    }
}
