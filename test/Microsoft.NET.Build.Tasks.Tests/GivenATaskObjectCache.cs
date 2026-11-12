// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests;

[TestClass]
public sealed class GivenATaskObjectCache : SdkTest
{
    [TestMethod]
    public async Task ConcurrentColdMissesInitializeOnlyOneValue()
    {
        using var engine = new SimultaneousCacheMissBuildEngine();
        int initializations = 0;

        object GetValue() => TaskObjectCache.GetOrCreate(
            engine,
            "shared",
            RegisteredTaskObjectLifetime.Build,
            () =>
            {
                Interlocked.Increment(ref initializations);
                return new object();
            });

        object[] results = await Task.WhenAll(
            Task.Run(GetValue, TestContext.CancellationToken),
            Task.Run(GetValue, TestContext.CancellationToken));

        Assert.AreEqual(1, initializations);
        Assert.AreSame(results[0], results[1]);
    }

    [TestMethod]
    public async Task DifferentKeysCanInitializeConcurrently()
    {
        var engine = new MockBuildEngine();
        using var factories = new Barrier(2);

        object GetValue(string key) => TaskObjectCache.GetOrCreate(
            engine,
            key,
            RegisteredTaskObjectLifetime.Build,
            () =>
            {
                Assert.IsTrue(factories.SignalAndWait(TimeSpan.FromSeconds(30), TestContext.CancellationToken),
                    "Unrelated cache keys must not serialize their factories.");
                return new object();
            });

        object[] results = await Task.WhenAll(
            Task.Run(() => GetValue("first"), TestContext.CancellationToken),
            Task.Run(() => GetValue("second"), TestContext.CancellationToken));

        Assert.AreNotSame(results[0], results[1]);
    }

    [TestMethod]
    public void FailedInitializationCanBeRetried()
    {
        var engine = new MockBuildEngine();
        int initializations = 0;

        object CreateValue()
        {
            if (++initializations == 1)
            {
                throw new IOException("The input is not ready.");
            }

            return new object();
        }

        Assert.ThrowsExactly<IOException>(() => TaskObjectCache.GetOrCreate(
            engine, "retry", RegisteredTaskObjectLifetime.Build, CreateValue));

        object result = TaskObjectCache.GetOrCreate(
            engine, "retry", RegisteredTaskObjectLifetime.Build, CreateValue);
        object cached = TaskObjectCache.GetOrCreate(
            engine, "retry", RegisteredTaskObjectLifetime.Build, CreateValue);

        Assert.AreEqual(2, initializations);
        Assert.AreSame(result, cached);
    }

    [TestMethod]
    public void CacheLifetimesAndBuildEnginesRemainIndependent()
    {
        var engine = new MockBuildEngine();
        object GetValue(MockBuildEngine buildEngine, RegisteredTaskObjectLifetime lifetime) =>
            TaskObjectCache.GetOrCreate(buildEngine, "lifetime", lifetime, () => new object());

        object buildValue = GetValue(engine, RegisteredTaskObjectLifetime.Build);
        object processValue = GetValue(engine, RegisteredTaskObjectLifetime.AppDomain);
        Assert.AreNotSame(buildValue, processValue);
        Assert.AreNotSame(buildValue, GetValue(new MockBuildEngine(), RegisteredTaskObjectLifetime.Build));

        foreach (var key in engine.RegisteredTaskObjects.Keys.Where(key => key.Item2 == RegisteredTaskObjectLifetime.Build))
        {
            engine.UnregisterTaskObject(key.Item1, key.Item2);
        }

        Assert.AreNotSame(buildValue, GetValue(engine, RegisteredTaskObjectLifetime.Build));
        Assert.AreSame(processValue, GetValue(engine, RegisteredTaskObjectLifetime.AppDomain));
    }

    [TestMethod]
    public void CacheEntriesDoNotCollideWithLegacyValuesOrOtherValueTypes()
    {
        var engine = new MockBuildEngine();
        var legacy = new object();
        engine.RegisterTaskObject("shared", legacy, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: true);

        object first = TaskObjectCache.GetOrCreate(
            engine, "shared", RegisteredTaskObjectLifetime.Build, () => new object());
        string second = TaskObjectCache.GetOrCreate(
            engine, "shared", RegisteredTaskObjectLifetime.Build, () => "another value type");

        Assert.AreNotSame(legacy, first);
        Assert.AreSame(legacy, engine.GetRegisteredTaskObject("shared", RegisteredTaskObjectLifetime.Build));
        Assert.AreSame(first, TaskObjectCache.GetOrCreate<object>(
            engine, "shared", RegisteredTaskObjectLifetime.Build, () => throw new InvalidOperationException()));
        Assert.AreEqual(second, TaskObjectCache.GetOrCreate<string>(
            engine, "shared", RegisteredTaskObjectLifetime.Build, () => throw new InvalidOperationException()));
    }

    [TestMethod]
    public async Task ConcurrentAssetsFileLoadsShareTheParsedLockFile()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string assetsFile = Path.Combine(directory, "project.assets.json");
        File.WriteAllText(assetsFile, """{"version":3,"targets":{},"libraries":{},"projectFileDependencyGroups":{}}""");
        using var engine = new SimultaneousCacheMissBuildEngine();
        var first = new LockFileCache(new ResolvePackageAssets { BuildEngine = engine });
        var second = new LockFileCache(new ResolvePackageAssets { BuildEngine = engine });

        var results = await Task.WhenAll(
            Task.Run(() => first.GetLockFile(assetsFile), TestContext.CancellationToken),
            Task.Run(() => second.GetLockFile(assetsFile), TestContext.CancellationToken));

        Assert.AreSame(results[0], results[1]);
    }

    [TestMethod]
    public void MissingAssetsFileCanBeLoadedAfterItAppears()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string assetsFile = Path.Combine(directory, "project.assets.json");
        var cache = new LockFileCache(new ResolvePackageAssets { BuildEngine = new MockBuildEngine() });

        Assert.ThrowsExactly<BuildErrorException>(() => cache.GetLockFile(assetsFile));
        File.WriteAllText(assetsFile, """{"version":3,"targets":{},"libraries":{},"projectFileDependencyGroups":{}}""");

        var result = cache.GetLockFile(assetsFile);

        Assert.AreSame(result, cache.GetLockFile(assetsFile));
    }

    private sealed class SimultaneousCacheMissBuildEngine : MockBuildEngine, IDisposable
    {
        private readonly Barrier _misses = new(2);
        private int _queries;

        public override object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            object? result = base.GetRegisteredTaskObject(key, lifetime);
            if (Interlocked.Increment(ref _queries) <= 2 &&
                !_misses.SignalAndWait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Both callers must observe the initial cache miss.");
            }

            return result;
        }

        public void Dispose() => _misses.Dispose();
    }
}
