// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
internal static class HashingUtils
{
    public static byte[] ComputeHash(MemoryStream memoryStream, params string[] values)
    {
        memoryStream.SetLength(0);
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8, -1, leaveOpen: true);
        using var sha256 = SHA256.Create();
        for (var i = 0; i < values.Length; i++)
        {
            writer.Write(values[i]);
        }
        writer.Flush();
        memoryStream.Position = 0;
        return sha256.ComputeHash(memoryStream);
    }

    public static byte[] ComputeHash(MemoryStream memoryStream, Span<ITaskItem> items, params string[] properties)
    {
        memoryStream.SetLength(0);
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8, -1, leaveOpen: true);
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
        params string[] metadata)
    {
        var hashSet = new Dictionary<string, ITaskItem>(candidateAssets.Length);
        for (var i = 0; i < candidateAssets.Length; i++)
        {
            var candidate = candidateAssets[i];
            hashSet.Add(Convert.ToBase64String(ComputeHash(memoryStream, candidateAssets.AsSpan(i, 1), properties: metadata)), candidate);
        }

        return hashSet;
    }
}
