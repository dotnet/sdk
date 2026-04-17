// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

internal static class HashingUtils
{
#if NET9_0_OR_GREATER
    public static byte[] ComputeHash(MemoryStream memoryStream, Span<string> values)
#else
    public static byte[] ComputeHash(MemoryStream memoryStream, params string[] values)
#endif
    {
        using var writer = CreateWriter(memoryStream);
        using var sha256 = SHA256.Create();
        for (var i = 0; i < values.Length; i++)
        {
            writer.Write(values[i]);
        }
        writer.Flush();
        memoryStream.Position = 0;
        return sha256.ComputeHash(memoryStream);
    }

#if NET9_0_OR_GREATER
    public static byte[] ComputeHash(MemoryStream memoryStream, Span<ITaskItem> items, params Span<string> properties)
#else
    public static byte[] ComputeHash(MemoryStream memoryStream, Span<ITaskItem> items, params string[] properties)
#endif
    {
        using var writer = CreateWriter(memoryStream);
        using var sha256 = SHA256.Create();
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            writer.Write(item.ItemSpec);
            for (var j = 0; j < properties.Length; j++)
            {
                writer.Write(item.GetMetadata(properties[j]));
            }
        }
        writer.Flush();
        memoryStream.Position = 0;
        return sha256.ComputeHash(memoryStream);
    }

    internal static Dictionary<string, ITaskItem> ComputeHashLookup(
        MemoryStream memoryStream,
        ITaskItem[] candidateAssets,
#if NET9_0_OR_GREATER
        Span<string> metadata)
#else
        params string[] metadata)
#endif
    {
        var hashSet = new Dictionary<string, ITaskItem>(candidateAssets.Length);
        for (var i = 0; i < candidateAssets.Length; i++)
        {
            var candidate = candidateAssets[i];
            hashSet.Add(Convert.ToBase64String(ComputeHash(memoryStream, candidateAssets.AsSpan(i, 1), properties: metadata)), candidate);
        }

        return hashSet;
    }

    private static StreamWriter CreateWriter(MemoryStream memoryStream)
    {
        memoryStream.SetLength(0);
#if NET9_0_OR_GREATER
        return new(memoryStream, encoding: Encoding.UTF8, leaveOpen: true);
#else
        return new(memoryStream, Encoding.UTF8, 512, leaveOpen: true);
#endif
    }
}
