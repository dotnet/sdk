// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.NET.Infrastructure.Tests;

[TestClass]
public class TestPathUtilityTests
{
    [TestMethod]
    public void ResolveFullPath_DriveRelativePath_ResolvesAgainstBasePath()
    {
        string basePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "scenario"));
        string path = Path.DirectorySeparatorChar == '\\'
            ? $"{Path.GetPathRoot(basePath)![0]}:scenario.binlog"
            : "C:scenario.binlog";
        string expectedPath = Path.GetFullPath(Path.DirectorySeparatorChar == '\\'
            ? Path.Combine(basePath, "scenario.binlog")
            : Path.Combine(basePath, path));

        Assert.AreEqual(expectedPath, TestPathUtility.ResolveFullPath(path, basePath));
    }
}
