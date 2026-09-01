// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ContentStoreTests
{
    [Theory]
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")]
    [InlineData("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    public void PathForDescriptor_AcceptsWellFormedDigest(string digest)
    {
        Descriptor descriptor = CreateDescriptorWithDigest(digest);
        string path = ContentStore.PathForDescriptor(descriptor);
        Assert.StartsWith(ContentStore.ContentRoot, path);
    }

    [Theory]
    [InlineData("")] // empty string
    [InlineData("sha256:../..\\xyz_not_hex!!")] // non-hex characters
    [InlineData("c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // missing algorithm prefix
    public void PathForDescriptor_RejectsInvalidDigest(string digest)
    {
        Descriptor descriptor = CreateDescriptorWithDigest(digest);
        Assert.Throws<InvalidDigestException>(() =>
            ContentStore.PathForDescriptor(descriptor));
    }

    private static Descriptor CreateDescriptorWithDigest(string digest) =>
        new("application/vnd.oci.image.layer.v1.tar+gzip", digest, 1024);
}
