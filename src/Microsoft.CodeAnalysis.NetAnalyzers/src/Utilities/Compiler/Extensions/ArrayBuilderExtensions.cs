// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Analyzer.Utilities.PooledObjects.Extensions
{
    internal static class ArrayBuilderExtensions
    {
        public static void AddIfNotNull<T>(this ArrayBuilder<T> builder, T? item)
            where T : class
        {
            if (item != null)
            {
                builder.Add(item);
            }
        }
    }
}
