// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ContentStoreTests
{
    [Theory]
    [InlineData("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha256:FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // random valid sha256 digest
    [InlineData("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")] // well known empty content digest
    [InlineData("md5:5b0bcabd1ed22e9fb1310cf6")] // unregistered algorithm, but valid per OCI grammar
    [InlineData("sha256:abc")] // valid per OCI grammar (no min length)
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916a")] // 63 hex chars, valid per OCI grammar
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916adF")] // 65 hex chars, valid per OCI grammar
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad000000000000")] // long, valid per OCI grammar
    public void PathForDescriptor_AcceptsWellFormedDigest(string digest)
    {
        Descriptor descriptor = CreateDescriptorWithDigest(digest);
        string path = ContentStore.PathForDescriptor(descriptor);
        Assert.StartsWith(ContentStore.ContentRoot, path);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("sha256:")] // missing encoded value
    [InlineData("sha256:../..\\xyz_not_hex!!")] // path traversal / invalid characters
    [InlineData("c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // missing algorithm prefix
    [InlineData("0000000c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // correct string length, no algorithm prefix
    public void PathForDescriptor_RejectsInvalidDigest(string digest)
    {
        Descriptor descriptor = CreateDescriptorWithDigest(digest);
        Assert.Throws<ArgumentException>(() =>
            ContentStore.PathForDescriptor(descriptor));
    }

    private static Descriptor CreateDescriptorWithDigest(string digest) =>
        new("application/vnd.oci.image.layer.v1.tar+gzip", digest, 1024);
}
