// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

internal sealed partial class IndexedStringResourceTable
{
    /// <summary>
    ///  An atomically published decoded string cache entry.
    /// </summary>
    private sealed class CacheEntry
    {
        internal CacheEntry(string name, string value)
        {
            Name = name;
            Value = value;
        }

        internal string Name { get; }

        internal string Value { get; }
    }
}