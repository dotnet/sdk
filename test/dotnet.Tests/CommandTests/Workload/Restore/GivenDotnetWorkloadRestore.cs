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
            _testAssetsManager
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
            _testAssetsManager
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
