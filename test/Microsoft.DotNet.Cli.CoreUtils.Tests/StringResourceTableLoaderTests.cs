// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class StringResourceTableLoaderTests
{
    [TestMethod]
    public void LoadStringTableFromResourcesFile_ValidStrings_ReturnsOrdinalDictionary()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(
            ("Greeting", "Hello"),
            ("Farewell", "Goodbye"));
        Dictionary<string, string> table = StringResourceTableLoader.LoadStringTableFromResourcesFile(resources);

        Assert.AreSame(StringComparer.Ordinal, table.Comparer);
        Assert.HasCount(2, table);
        Assert.AreEqual("Hello", table["Greeting"]);
        Assert.AreEqual("Goodbye", table["Farewell"]);
        Assert.IsFalse(table.ContainsKey("greeting"));
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_ValidAssembly_ReturnsEmbeddedStrings()
    {
        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            ResourceTestUtilities.TestAssembly,
            $"{ResourceTestUtilities.NeutralBaseName}.resources");

        Assert.IsNotNull(table);
        Assert.AreEqual("Hello", table["Greeting"]);
    }

    [TestMethod]
    public void LoadStringTableFromAssembly_ValidImage_ReturnsEmbeddedStrings()
    {
        byte[] assembly = File.ReadAllBytes(ResourceTestUtilities.TestAssembly.Location);
        Dictionary<string, string>? table = StringResourceTableLoader.LoadStringTableFromAssembly(
            assembly,
            $"{ResourceTestUtilities.NeutralBaseName}.resources");

        Assert.IsNotNull(table);
        Assert.AreEqual("Hello", table["Greeting"]);
    }

    [TestMethod]
    public void LoadStringTableFromResourcesFile_UnknownOptions_ThrowsArgumentOutOfRangeException()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StringResourceTableLoader.LoadStringTableFromResourcesFile(
            resources,
            (StringResourceManagerOptions)int.MinValue));
    }
}
