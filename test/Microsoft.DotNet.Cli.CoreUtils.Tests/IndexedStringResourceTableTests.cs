// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class IndexedStringResourceTableTests
{
    [TestMethod]
    public void Lookup_RepeatedName_DelegatesEachCall()
    {
        CountingReader reader = new();
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        Assert.AreEqual("Hello", table.Lookup("Greeting"));
        Assert.AreEqual("Hello", table.Lookup("Greeting"));
        Assert.AreEqual(2, reader.LookupCount);
    }

#if !DEBUG
    [TestMethod]
    public void Lookup_AlternatingNames_DoesNotAllocateCacheEntries()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new CountingReader(),
            StringResourceManagerOptions.None);

        _ = table.Lookup("Greeting");
        _ = table.Lookup("Farewell");
        long before = GC.GetAllocatedBytesForCurrentThread();
        string? greeting = table.Lookup("Greeting");
        string? farewell = table.Lookup("Farewell");
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual("Hello", greeting);
        Assert.AreEqual("Goodbye", farewell);
        Assert.AreEqual(0L, allocated);
    }
#endif

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Lookup_RepeatedName_DecodesEachTime(bool streamBacked)
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        IStringResourceReader reader = streamBacked
            ? new StreamStringResourceReader(new MemoryStream(resources, writable: false))
            : new RawResourceReader(resources);
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        string? first = table.Lookup("Greeting");
        string? second = table.Lookup("Greeting");

        Assert.AreEqual("Hello", first);
        Assert.AreEqual("Hello", second);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void Lookup_ConcurrentCustomReader_DelegatesEveryCall()
    {
        CountingReader reader = new();
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        Parallel.For(0, 8, worker =>
        {
            for (int i = 0; i < 1_000; i++)
            {
                bool greeting = ((i + worker) & 1) == 0;
                string name = greeting ? "Greeting" : "Farewell";
                string expected = greeting ? "Hello" : "Goodbye";
                Assert.AreEqual(expected, table.Lookup(name));
            }
        });

        Assert.AreEqual(8_000, reader.LookupCount);
    }

    [TestMethod]
    [DataRow("Greeting")]
    [DataRow("Farewell")]
    [DataRow("Missing")]
    public void Lookup_DisposedTable_ThrowsObjectDisposedException(string name)
    {
        CountingReader reader = new();
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);
        table.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => table.Lookup(name));
        Assert.AreEqual(0, reader.LookupCount);
        Assert.AreEqual(1, reader.DisposeCount);
    }

    [TestMethod]
    public void Dispose_RepeatedCalls_DisposesReaderOnce()
    {
        CountingReader reader = new();
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            reader,
            StringResourceManagerOptions.None);

        table.Dispose();
        table.Dispose();

        Assert.AreEqual(1, reader.DisposeCount);
    }

    [TestMethod]
    public void Create_UnknownOptions_DisposesReaderAndThrows()
    {
        CountingReader reader = new();
        try
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IndexedStringResourceTable.Create(
                reader,
                (StringResourceManagerOptions)int.MinValue));
            Assert.AreEqual(1, reader.DisposeCount);
        }
        finally
        {
            if (reader.DisposeCount == 0)
            {
                reader.Dispose();
            }
        }
    }

    [TestMethod]
    public void Create_NullResource_DisposesReaderAndThrows()
    {
        CountingReader reader = new(ResourceTypeCode.Null);
        try
        {
            Assert.ThrowsExactly<BadImageFormatException>(() => IndexedStringResourceTable.Create(
                reader,
                StringResourceManagerOptions.None));
            Assert.AreEqual(1, reader.DisposeCount);
        }
        finally
        {
            if (reader.DisposeCount == 0)
            {
                reader.Dispose();
            }
        }
    }

    private sealed class CountingReader(ResourceTypeCode typeCode = ResourceTypeCode.String) : IStringResourceReader
    {
        private int _lookupCount;
        private int _disposeCount;

        internal int LookupCount => Volatile.Read(ref _lookupCount);

        internal int DisposeCount => Volatile.Read(ref _disposeCount);

        public int ResourceCount => 2;

        public ResourceTypeCode GetResourceTypeCode(int index) => typeCode;

        public string GetResourceName(int index) => index == 0 ? "Greeting" : "Farewell";

        public string GetString(int index) => index == 0 ? "Hello" : "Goodbye";

        public string? Lookup(string name)
        {
            Interlocked.Increment(ref _lookupCount);
            return name switch
            {
                "Greeting" => "Hello",
                "Farewell" => "Goodbye",
                _ => null
            };
        }

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }
}
