// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;

namespace Microsoft.NET.Build.Containers;

internal static class StreamExtensions
{
    public static async Task CopyToAndVerifyAsync(
        this Stream source,
        Stream destination,
        string digest,
        CancellationToken cancellationToken)
    {
        byte[] expectedHash = DigestUtils.GetEncodedValue(digest).ToArray();

        // Assumption: SHA256 is the only supported algorithm.
        // See DigestUtils > s_registeredAlgorithms for more details.
        using HashAlgorithm hashAlgorithm = SHA256.Create();

        await using var cryptoStream = new CryptoStream(
            stream: source,
            transform: hashAlgorithm,
            mode: CryptoStreamMode.Read,
            // cryptoStream will be disposed at the end of the method, but setting leaveOpen=true
            // tells CryptoStream not to dispose the source Stream. The caller is responsible for
            // the lifetime of the source Stream.
            leaveOpen: true
        );

        await cryptoStream.CopyToAsync(destination, cancellationToken);

        InvalidDigestException.ThrowIfMismatched(expectedHash, hashAlgorithm.Hash ?? []);
    }
}
