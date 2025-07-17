// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Hashing;

namespace Microsoft.NET.Build.Tasks;

public static class HashingUtils
{
    /// <summary>
    /// Computes the XxHash64 hash of a file.
    /// </summary>
    /// <param name="content">A stream to read for the hash. If the stream is seekable it will be reset to its incoming position.</param>
    public static byte[] ComputeXXHash64(Stream content)
    {
        var initialPosition = content.CanSeek ? content.Position : 0;
        var hasher = new XxHash64();
        hasher.Append(content);
        if (content.CanSeek)
        {
            content.Position = initialPosition;
        }
        return hasher.GetCurrentHash();
    }
}
