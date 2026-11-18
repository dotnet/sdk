// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests;

[TestClass]
public class GivenDotNetMacInstallers : SdkTest
{
    [TestMethod]
    public void PkgPackagePreservesSymbolicLinks() =>
        FileLinkHelpers.VerifyInstallerSymlinks(
            OSPlatform.OSX,
            "dotnet-sdk-*.pkg",
            excludeSubstrings: ["-internal"],
            ExtractPkgPackage,
            TestAssetsManager,
            Log);

    private void ExtractPkgPackage(string installerPath, string tempDir)
    {
        // Let pkgutil expand both the package and its payloads, including PBZX payloads.
        var expandedDir = Path.Combine(tempDir, "expanded");
        new RunExeCommand(Log, "pkgutil")
            .Execute("--expand-full", installerPath, expandedDir)
            .Should().Pass();
    }
}
