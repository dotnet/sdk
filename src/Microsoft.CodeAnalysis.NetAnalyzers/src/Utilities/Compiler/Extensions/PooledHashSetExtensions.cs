// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Analyzer.Utilities.PooledObjects.Extensions
{
    internal static class PooledHashSetExtensions
    {
        public static void AddRange<T>(this PooledHashSet<T> builder, IEnumerable<T> set2)
        {
            foreach (var item in set2)
            {
                builder.Add(item);
            }
        }
    }
}