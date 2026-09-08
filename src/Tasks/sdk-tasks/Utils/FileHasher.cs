// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK
using System;
using System.IO;
using System.IO.Hashing;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Shared utility for content-based file hashing.
/// </summary>
internal static class FileHasher
{
    /// <summary>
    /// Computes an XxHash64 content hash for a file, returned as a hex string.
    /// </summary>
    public static string ComputeFileHash(string filePath)
    {
        var xxHash = new XxHash64();
        using var stream = File.OpenRead(filePath);

        byte[] buffer = new byte[65536]; // 64KB buffer
        int bytesRead;
        while ((bytesRead = stream.Read(buffer)) > 0)
        {
            xxHash.Append(buffer[..bytesRead]);
        }

        return Convert.ToHexString(xxHash.GetCurrentHash());
    }
}
#endif
