// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Analyzer.Utilities
{
    /// <summary>
    /// Provides bounded cache for analyzers.
    /// Entries remain available while their cached key is alive, subject to the capacity bound.
    /// Conditional ownership allows otherwise unreachable key/value cycles to be collected.
    /// </summary>
    internal sealed class BoundedCache<TKey, TValue> : BoundedCacheWithFactory<TKey, TValue>
        where TKey : class
        where TValue : new()
    {
        public TValue GetOrCreateValue(TKey key)
        {
            return GetOrCreateValue(key, CreateDefaultValue);

            // Local functions.
            static TValue CreateDefaultValue(TKey _) => new();
        }
    }
}
