// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ObjectCache = NetAnalyzers::Analyzer.Utilities.BoundedCacheWithFactory<object, object>;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class BoundedCacheLifetimeTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DoNotParallelize]
        public void LiveKeysKeepEntriesAvailableAcrossCollection()
        {
            var cache = new ObjectCache();
            var key = new object();
            var calls = 0;
            object Create(object _) { calls++; return new object(); }
            var expected = cache.GetOrCreateValue(key, Create);
            Collect();
            Assert.AreSame(expected, cache.GetOrCreateValue(key, Create));
            Assert.AreEqual(1, calls);
            GC.KeepAlive(key);
        }

        [TestMethod]
        [DoNotParallelize]
        public void CapacityRemainsBoundedWithLiveKeys()
        {
            var cache = new ObjectCache();
            var keys = Enumerable.Range(0, 12).Select(_ => new object()).ToArray();
            var values = keys.Select(key => CreateValue(cache, key)).ToArray();
            Collect();
            Assert.HasCount(5, values.Where(value => value.TryGetTarget(out _)).ToArray());
            GC.KeepAlive(keys);
        }

        [TestMethod]
        [DoNotParallelize]
        public void UnreachableKeyValueCyclesAreCollected()
        {
            var cache = new ObjectCache();
            var (key, value) = CreateCycle(cache);
            Collect();
            Assert.IsFalse(key.TryGetTarget(out _));
            Assert.IsFalse(value.TryGetTarget(out _));
            GC.KeepAlive(cache);
        }

        [TestMethod]
        public void FactoryFailureDoesNotEvictExistingEntry()
        {
            var cache = new ObjectCache();
            var keys = Enumerable.Range(0, 5).Select(_ => new object()).ToArray();
            var values = keys.Select(key => cache.GetOrCreateValue(key, _ => new object())).ToArray();
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                cache.GetOrCreateValue(new object(), _ => throw new InvalidOperationException()));

            for (var index = 0; index < keys.Length; index++)
            {
                Assert.AreSame(values[index], cache.GetOrCreateValue(keys[index], _ => throw new InvalidOperationException()));
            }
        }

        [TestMethod]
        public void ConcurrentRequestsComputeOneValue()
        {
            var cache = new ObjectCache();
            var key = new object();
            var calls = 0;
            var results = new object[32];
            Parallel.For(0, results.Length, new ParallelOptions { CancellationToken = TestContext.CancellationToken }, index =>
                results[index] = cache.GetOrCreateValue(key, _ =>
                {
                    Interlocked.Increment(ref calls);
                    return new object();
                }));
            Assert.AreEqual(1, calls);

            foreach (var result in results)
            {
                Assert.AreSame(results[0], result);
            }
        }

        [TestMethod]
        public void EquivalentKeysAndRecencyKeepExistingBehavior()
        {
            var cache = new ObjectCache();
            var keys = Enumerable.Range(0, 6).Select(value => new EqualKey(value)).ToArray();
            var values = keys.Take(5).Select(key => cache.GetOrCreateValue(key, _ => new object())).ToArray();
            Assert.AreSame(values[0], cache.GetOrCreateValue(new EqualKey(0), _ => throw new InvalidOperationException()));
            _ = cache.GetOrCreateValue(keys[5], _ => new object());
            Assert.AreSame(values[0], cache.GetOrCreateValue(keys[0], _ => throw new InvalidOperationException()));
            var replacement = cache.GetOrCreateValue(keys[1], _ => new object());
            Assert.AreNotSame(values[1], replacement);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference<object> CreateValue(ObjectCache cache, object key)
            => new(cache.GetOrCreateValue(key, _ => new byte[1024 * 1024]));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference<object> Key, WeakReference<object> Value) CreateCycle(ObjectCache cache)
        {
            var key = new object();
            var value = cache.GetOrCreateValue(key, _ => new KeyOwner(key));
            return (new WeakReference<object>(key), new WeakReference<object>(value));
        }

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed record EqualKey(int Value);
        private sealed record KeyOwner(object Key);
    }
}
