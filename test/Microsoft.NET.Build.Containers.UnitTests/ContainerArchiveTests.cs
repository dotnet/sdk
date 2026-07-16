// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContainerArchiveTests
{
    [TestMethod]
    public async Task Rejects_unsupported_manifest_media_type()
    {
        BuiltImage image = new()
        {
            Config = string.Empty,
            Manifest = string.Empty,
            ManifestDigest = string.Empty,
            ManifestMediaType = "unsupported"
        };

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => ContainerArchive.WriteImageToStreamAsync(image, default, default, Stream.Null, default));
    }
}
