// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests;

public class GivenSdkArchives(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void ItHasDeduplicatedAssemblies()
    {
        // TODO: Windows is not supported yet - blocked on signing support (https://github.com/dotnet/sdk/issues/52182).
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Find and extract archive
        string archivePath = TestContext.FindSdkAcquisitionArtifact("dotnet-sdk-*.tar.gz");
        Log.WriteLine($"Found SDK archive: {Path.GetFileName(archivePath)}");
        string extractedPath = ExtractArchive(archivePath);

        // Verify deduplication worked by checking for symbolic links
        SymbolicLinkHelpers.VerifyDirectoryHasRelativeSymlinks(extractedPath, Log, "archive");
    }

    private string ExtractArchive(string archivePath)
    {
        var testDir = TestAssetsManager.CreateTestDirectory();
        string extractPath = Path.Combine(testDir.Path, "sdk-extracted");
        Directory.CreateDirectory(extractPath);

        Log.WriteLine($"Extracting archive to: {extractPath}");

        SymbolicLinkHelpers.ExtractTarGz(archivePath, extractPath, Log);

        return extractPath;
    }
}
