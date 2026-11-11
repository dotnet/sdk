// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class StreamStringResourceReaderTests
{
    [TestMethod]
    public void Lookup_ConcurrentLookupAndIndexReads_ReturnRequestedValues()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(
            ("Greeting", "Hello"),
            ("Farewell", "Goodbye"));
        using StreamStringResourceReader reader = new(new MemoryStream(resources, writable: false));
        string[] names = [reader.GetResourceName(0), reader.GetResourceName(1)];
        string[] values = [reader.GetString(0), reader.GetString(1)];

        Parallel.For(0, 8, worker =>
        {
            for (int i = 0; i < 1_000; i++)
            {
                int index = (i + worker) & 1;
                Assert.AreEqual(values[index], reader.Lookup(names[index]));
                Assert.AreEqual(names[index], reader.GetResourceName(index));
                Assert.AreEqual(ResourceTypeCode.String, reader.GetResourceTypeCode(index));
                Assert.AreEqual(values[index], reader.GetString(index));
            }
        });
    }

    [TestMethod]
    public void Lookup_MissingName_ReturnsNull()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        using StreamStringResourceReader reader = new(new MemoryStream(resources, writable: false));

        Assert.IsNull(reader.Lookup("Missing"));
        Assert.AreEqual("Hello", reader.Lookup("Greeting"));
    }

    [TestMethod]
    [DataRow("Greeting")]
    [DataRow("Farewell")]
    [DataRow("Missing")]
    public void Lookup_DisposedReader_ThrowsObjectDisposedException(string name)
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        using StreamStringResourceReader reader = new(new MemoryStream(resources, writable: false));
        reader.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => reader.Lookup(name));
    }

    [TestMethod]
    public void ReadMembers_DisposedReader_ThrowsObjectDisposedException()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        using StreamStringResourceReader reader = new(new MemoryStream(resources, writable: false));
        reader.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => reader.GetResourceTypeCode(0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => reader.GetResourceName(0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => reader.GetString(0));
    }
}
