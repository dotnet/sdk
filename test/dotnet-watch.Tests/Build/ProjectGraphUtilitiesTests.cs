// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests.Build;

[TestClass]
public class ProjectGraphUtilitiesTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow(@"\", "/")]
    [DataRow(@"foo\", @"foo/")]
    [DataRow(@"foo\bar", @"foo/bar")]
    [DataRow(@"foo/bar", @"foo/bar")]
    [DataRow(@"foo\bar\", @"foo/bar/")]
    [DataRow(@"foo/bar\", @"foo/bar/")]
    [DataRow(@"foo/bar/", @"foo/bar/")]
    [DataRow(@"foo\bar/", @"foo/bar/")]
    public void GetDirectoryWithTrailingSlash(string input, string expected)
    {
        var normalized = ProjectGraphUtilities.NormalizeSeparators(input);
        Assert.AreEqual(expected, normalized);
    }
}

