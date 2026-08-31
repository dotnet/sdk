// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using OrasProject.Oras.Oci;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class DescriptorTests
{
    [TestMethod]
    public void BasicInitializer()
    {
        Descriptor d = new()
        {
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Digest = "sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270",
            Size = 7682,
        };

        Console.WriteLine(JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));

        Assert.AreEqual("application/vnd.oci.image.manifest.v1+json", d.MediaType);
        Assert.AreEqual("sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270", d.Digest);
        Assert.AreEqual(7_682, d.Size);

        Assert.IsNull(d.Annotations);
        Assert.IsNull(d.Data);
        Assert.IsNull(d.Urls);
    }
}
