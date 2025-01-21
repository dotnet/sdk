// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools
{
    internal static class StringBuilderCache
    {
        // The value 512 was chosen empirically as 95% percentile of returning string length.
        private const int MAX_BUILDER_SIZE = 512;

        [ThreadStatic]
        private static StringBuilder t_cachedInstance;

        public static StringBuilder Acquire(int capacity = 16 /*StringBuilder.DefaultCapacity*/)
        {
            if (capacity <= MAX_BUILDER_SIZE)
            {
                StringBuilder sb = t_cachedInstance;
                t_cachedInstance = null;
                if (sb != null)
                {
                    // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                    // when the requested size is larger than the current capacity
                    if (capacity <= sb.Capacity)
                    {
                        sb.Length = 0; // Equivalent of sb.Clear() that works on .Net 3.5

                        return sb;
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder(capacity);
            return stringBuilder;
        }

        public static string GetStringAndRelease(StringBuilder sb)
        {
            string result = sb.ToString();
            Release(sb);
            return result;
        }

        public static void Release(StringBuilder sb)
        {
            if (sb.Capacity <= MAX_BUILDER_SIZE)
            {
                // Assert we are not replacing another string builder. That could happen when Acquire is reentered.
                // User of StringBuilderCache has to make sure that calling method call stacks do not also use StringBuilderCache.
                Debug.Assert(t_cachedInstance == null, "Unexpected replacing of other StringBuilder.");
                t_cachedInstance = sb;
            }
        }
    }
}
