// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks;

internal static class TaskObjectCache
{
    public static T GetOrCreate<T>(
        IBuildEngine4 buildEngine,
        string key,
        RegisteredTaskObjectLifetime lifetime,
        Func<T> valueFactory) where T : class
    {
        // Different SDK task assemblies can share the same MSBuild process.
        object cacheKey = (typeof(CacheEntry<T>), key);
        var entry = (CacheEntry<T>?)buildEngine.GetRegisteredTaskObject(cacheKey, lifetime);
        if (entry is null)
        {
            var candidate = new CacheEntry<T>();
            buildEngine.RegisterTaskObject(cacheKey, candidate, lifetime, allowEarlyCollection: true);

            // Equal-key registration retains the first object, which may belong to another caller.
            entry = (CacheEntry<T>?)buildEngine.GetRegisteredTaskObject(cacheKey, lifetime) ?? candidate;
        }

        return entry.GetValue(valueFactory);
    }

    private sealed class CacheEntry<T> where T : class
    {
        private T _value = null!;
        private bool _initialized;
        private object? _syncLock;

        // Unlike Lazy<T>, this retries after an exception and does not retain the task's factory.
        public T GetValue(Func<T> valueFactory) =>
            LazyInitializer.EnsureInitialized(ref _value, ref _initialized, ref _syncLock, valueFactory);
    }
}
