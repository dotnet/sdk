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
        // OSX is disabled until https://github.com/dotnet/runtime/issues/125332 is fixed.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Log.WriteLine("SKIPPED: OSX deduplication not yet supported (https://github.com/dotnet/runtime/issues/125332)");
            return;
        }

        // Find and extract archive
        string archivePath = SdkTestContext.FindSdkAcquisitionArtifact("dotnet-sdk-*.tar.gz");
        Log.WriteLine($"Found SDK archive: {Path.GetFileName(archivePath)}");
        string extractedPath = ExtractArchive(archivePath);

        // Verify deduplication worked - Windows uses hardlinks, Unix uses symbolic links
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FileLinkHelpers.VerifyDirectoryHasHardlinks(extractedPath, Log, "archive");
        }
        else
        {
            FileLinkHelpers.VerifyDirectoryHasRelativeSymlinks(extractedPath, Log, "archive");
        }
    }

    private string ExtractArchive(string archivePath)
    {
        var testDir = TestAssetsManager.CreateTestDirectory();
        string extractPath = Path.Combine(testDir.Path, "sdk-extracted");
        Directory.CreateDirectory(extractPath);

        Log.WriteLine($"Extracting archive to: {extractPath}");

        FileLinkHelpers.ExtractTar(archivePath, extractPath, Log);

        return extractPath;
    }
}
