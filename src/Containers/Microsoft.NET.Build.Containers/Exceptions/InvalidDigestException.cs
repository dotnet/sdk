// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;

namespace Microsoft.NET.Build.Containers;

internal sealed class InvalidDigestException : Exception
{
    public InvalidDigestException(string message)
        : base(message)
    {
    }

    public static void ThrowIfMismatched(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            string expectedHashString = Convert.ToHexStringLower(expected);
            string actualHashString = Convert.ToHexStringLower(actual);
            throw new InvalidDigestException($"Expected {expectedHashString}, got {actualHashString}.");
        }
    }
}
