// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Microsoft.NET.Build.Containers.UnitTests;


public static class Data
{
    public static class Layer
    {
        /// <summary>
        /// layerBase64String is a base64 encoding of a simple tarball, obtained like this:
        /// $ echo 'you bothered to find out what was in here. Congratulations!' > test.txt
        /// $ tar czvf test.tar.gz test.txt
        /// $ cat test.tar.gz | base64
        /// </summary>
        public static string ConformanceLayerBase64String = "H4sIAAAAAAAAA+3OQQrCMBCF4a49xXgBSUnaHMCTRBptQRNpp6i3t0UEV7oqIv7fYgbmzeJpHHSjVy0" +
            "WZCa1c/MufWVe94N3RWlrZ72x3k/30nhbFWKWLPU0Dhp6keJ8im//PuU/6pZH2WVtYx8b0Sz7LjWSR5VLG6YRBumSzOlGtjkd+qD" +
            "jMWiX07Befbs7AAAAAAAAAAAAAAAAAPyzO34MnqoAKAAA";
        public static byte[] ConformanceLayerBytes = Convert.FromBase64String(ConformanceLayerBase64String);
        public static byte[] ConformanceLayerSha256Digest = SHA256.HashData(ConformanceLayerBytes);
        public static string ConformanceLayerSha256DigestString = Convert.ToHexStringLower(ConformanceLayerSha256Digest);
        public static int ConformanceLayerContentLength = ConformanceLayerBytes.Length;
        public static Descriptor ConformanceLayerDescriptor = new(
            "application/vnd.oci.image.layer.v1.tar+gzip",
            digest: new Digest(DigestAlgorithm.sha256, ConformanceLayerSha256DigestString),
            size: ConformanceLayerContentLength
            );

    }

    public static class Config
    {

    }

}
