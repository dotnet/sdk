// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watch.UnitTests;

namespace Microsoft.DotNet.HotReload.UnitTests;

public class EnvironmentUtilitiesTests
{
    [Fact]
    public void MultipleValues()
    {
        var builder = new Dictionary<string, string>();
        builder.InsertListItem("X", "a", separator: ';');
        builder.InsertListItem("X", "b", separator: ';');
        builder.InsertListItem("X", "a", separator: ';');

        AssertEx.SequenceEqual([KeyValuePair.Create("X", "b;a")], builder);
    }
}
