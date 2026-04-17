// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DigestUtilsTests
{
    [Theory]
    [InlineData("sha256:0000000000000000000000000000000000000000000000000000000000000000", "0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad", "c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")]
    [InlineData("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    public void GetEncoded_AcceptsValidDigest(string digest, string expectedEncoded)
    {
        string encoded = DigestUtils.GetEncoded(digest);
        Assert.Equal(expectedEncoded, encoded);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("sha256:")] // missing encoded value
    [InlineData("sha256:../..\\xyz_not_hex!!")] // path traversal / invalid characters
    [InlineData("c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // missing algorithm prefix
    [InlineData("0000000c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // correct string length, no algorithm prefix
    [InlineData("sha256:abc")] // too short for sha256
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916a")] // 63 hex chars (1 too short for sha256)
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916adF")] // uppercase hex not allowed per OCI spec
    [InlineData("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad000000000000")] // too long for sha256
    [InlineData("sha256:FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")] // uppercase hex not allowed per OCI spec
    [InlineData("md5:5b0bcabd1ed22e9fb1310cf6")] // unregistered algorithm
    [InlineData("sha512:abc")] // sha512 not currently supported
    [InlineData("blake3:abc")] // blake3 not currently supported
    public void GetEncoded_RejectsInvalidDigest(string digest)
    {
        Assert.Throws<InvalidDigestException>(() =>
            DigestUtils.GetEncoded(digest));
    }

    [Fact]
    public void ComputeSha256Digest_ReturnsCorrectDigest()
    {
        // Well-known: SHA-256 of empty string
        string digest = DigestUtils.ComputeSha256Digest("");
        Assert.Equal("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", digest);
    }

    [Fact]
    public void ComputeSha256_ReturnsLowercaseHex()
    {
        string hash = DigestUtils.ComputeSha256("");
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public void FormatSha256Digest_FormatsCorrectly()
    {
        string digest = DigestUtils.FormatSha256Digest("abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");
        Assert.Equal("sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", digest);
    }

    [Fact]
    public void ComputeSha256Digest_RoundTrips_Through_GetEncoded()
    {
        string digest = DigestUtils.ComputeSha256Digest("hello world");
        string encoded = DigestUtils.GetEncoded(digest);
        Assert.Equal(DigestUtils.ComputeSha256("hello world"), encoded);
    }
}
