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
            assemblyName: "assembly name",
            relativePath: "some path",
            [1, 2, 3],
            isApplicationProject: true);

        using var stream = new MemoryStream();
        await initial.WriteAsync(stream, CancellationToken.None);

        stream.Position = 0;
        var read = await StaticAssetUpdateRequest.ReadAsync(stream, CancellationToken.None);

        AssertEqual(initial, read);
    }

    private static void AssertEqual(StaticAssetUpdateRequest initial, StaticAssetUpdateRequest read)
    {
        Assert.Equal(initial.AssemblyName, read.AssemblyName);
        Assert.Equal(initial.RelativePath, read.RelativePath);
        Assert.Equal(initial.IsApplicationProject, read.IsApplicationProject);
        Assert.Equal(initial.Contents, read.Contents);
    }
}
