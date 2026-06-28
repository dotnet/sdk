// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class EnvironmentUtilitiesTests
{
    [TestMethod]
    public void MultipleValues()
    {
        var builder = new Dictionary<string, string>();
        builder.InsertListItem("X", "a", separator: ';');
        builder.InsertListItem("X", "b", separator: ';');
        builder.InsertListItem("X", "a", separator: ';');

        Assert.AreSequenceEqual([new KeyValuePair<string, string>("X", "b;a")], builder);
    }

    [TestMethod]
    public void EmptyValue()
    {
        var builder = new Dictionary<string, string>();
        builder["X"] = "";

        builder.InsertListItem("X", "a", separator: ';');
        builder.InsertListItem("X", "b", separator: ';');
        builder.InsertListItem("X", "a", separator: ';');

        Assert.AreSequenceEqual([new KeyValuePair<string, string>("X", "b;a")], builder);
    }
}
