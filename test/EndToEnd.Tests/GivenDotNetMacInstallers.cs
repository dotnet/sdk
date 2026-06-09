// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests;

public class GivenDotNetMacInstallers(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
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
        // Expand the pkg using pkgutil
        var expandedDir = Path.Combine(tempDir, "expanded");
        new RunExeCommand(Log, "pkgutil")
            .Execute("--expand", installerPath, expandedDir)
            .Should().Pass();

        // Find and extract the Payload from each component
        // pkg files contain one or more component directories, each with a Payload file
        var payloadFiles = Directory.GetFiles(expandedDir, "Payload", SearchOption.AllDirectories);

        foreach (var payloadFile in payloadFiles)
        {
            var componentDir = Path.GetDirectoryName(payloadFile)!;
            var componentName = Path.GetFileName(componentDir);
            var extractDir = Path.Combine(tempDir, "data", componentName);
            Directory.CreateDirectory(extractDir);

            // Payload is a cpio archive (possibly compressed)
            // Use ditto which handles Apple's archive formats well
            new RunExeCommand(Log, "ditto")
                .Execute("-x", payloadFile, extractDir)
                .Should().Pass();
        }
    }
}
