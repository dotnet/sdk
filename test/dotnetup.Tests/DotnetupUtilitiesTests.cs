// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DotnetupUtilitiesTests
{
    [TestMethod]
    [DataRow(null, null, true)]
    [DataRow("", "", true)]
    [DataRow(null, "", false)]
    [DataRow("", null, false)]
    [DataRow(null, "global.json", false)]
    [DataRow("global.json", null, false)]
    [DataRow("", "global.json", false)]
    [DataRow("global.json", "", false)]
    public void PathsEqualHandlesMissingPaths(string? left, string? right, bool expected)
    {
        Assert.AreEqual(expected, DotnetupUtilities.PathsEqual(left, right));
    }

    [TestMethod]
    public void PathsEqualNormalizesNonemptyPaths()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotnetup-path-tests");
        Assert.IsTrue(DotnetupUtilities.PathsEqual(path, Path.Combine(path, ".")));
        Assert.IsTrue(DotnetupUtilities.PathsEqual(path, path + Path.DirectorySeparatorChar));
        Assert.IsFalse(DotnetupUtilities.PathsEqual(path, Path.Combine(path, "other")));
    }
}
