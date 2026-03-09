// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Workload.Restore.Tests;

public class GivenDotnetWorkloadRestore : SdkTest
{
    public GivenDotnetWorkloadRestore(ITestOutputHelper log) : base(log)
    {
    }

    public static string DcProjAssetName = "SolutionWithAppAndDcProj";
    public static string TransitiveReferenceNoWorkloadsAssetName = "ProjectWithEsProjReference";

    [Fact]
    public void ProjectsThatDoNotSupportWorkloadsAreNotInspected()
    {
        if (IsRunningInContainer())
        {
            // Skipping test in a Helix container environment due to read-only DOTNET_ROOT, which causes workload restore to fail when writing workload metadata.
            return;
        }

        var projectPath =
            TestAssetsManager
                .CopyTestAsset(DcProjAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
        .WithWorkingDirectory(projectPath)
        .Execute()
        .Should()
        // if we did try to restore the dcproj in this TestAsset we would fail, so passing means we didn't!
        .Pass();
    }

    [Fact]
    public void ProjectsThatDoNotSupportWorkloadsAndAreTransitivelyReferencedDoNotBreakTheBuild()
    {
        if (IsRunningInContainer())
        {
            // Skipping test in a Helix container environment due to read-only DOTNET_ROOT, which causes workload restore to fail when writing workload metadata.
            return;
        }

        var projectPath =
            TestAssetsManager
                .CopyTestAsset(TransitiveReferenceNoWorkloadsAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
        .WithWorkingDirectory(projectPath)
        .Execute()
        .Should()
        // if we did try to restore the esproj in this TestAsset we would fail, so passing means we didn't!
        .Pass();
    }

    [Fact]
    public void VersionOptionShouldNotConflictWithSkipManifestUpdate()
    {
        if (IsRunningInContainer())
        {
            // Skipping test in a Helix container environment due to read-only DOTNET_ROOT, which causes workload restore to fail when writing workload metadata.
            return;
        }

        var projectPath =
            TestAssetsManager
                .CopyTestAsset(TransitiveReferenceNoWorkloadsAssetName)
                .WithSource()
                .Path;

        var result = new DotnetWorkloadCommand(Log, "restore", "--version", "9.0.100")
        .WithWorkingDirectory(projectPath)
        .Execute();

        // Should not fail with "Cannot use the --skip-manifest-update and --sdk-version options together"
        // The command may fail for other reasons (e.g., version not found), but it should not fail with the skip-manifest-update error
        result.StdErr.Should().NotContain("Cannot use the");
        result.StdErr.Should().NotContain("--skip-manifest-update");
    }

    private bool IsRunningInContainer()
    {
        if (!File.Exists("/.dockerenv"))
        {
            return false;
        }

        string osDescription = RuntimeInformation.OSDescription.ToLowerInvariant();
        return osDescription.Contains("centos") ||
               osDescription.Contains("debian") ||
               osDescription.Contains("ubuntu");
    }
}
