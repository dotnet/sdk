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
        var cliHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), ".dotnet");
        Directory.CreateDirectory(cliHome);
        CreateUserLocalFileForCurrentSdk(cliHome);

        var projectPath =
            _testAssetsManager
                .CopyTestAsset(DcProjAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
        .WithWorkingDirectory(projectPath)
        .WithEnvironmentVariable("DOTNET_CLI_HOME", cliHome)
        .WithEnvironmentVariable("HOME", cliHome)
        .WithEnvironmentVariable("DOTNET_ROOT", cliHome)
        .WithEnvironmentVariable("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", Path.Combine(cliHome, "sdk-manifests"))
        .WithEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS", Path.Combine(cliHome, "packs"))
        .WithEnvironmentVariable("DOTNETSDK_WORKLOAD_METADATA_ROOT", Path.Combine(cliHome, "metadata"))
        .Execute()
        .Should()
        // if we did try to restore the dcproj in this TestAsset we would fail, so passing means we didn't!
        .Pass();
    }

    [Fact]
    public void ProjectsThatDoNotSupportWorkloadsAndAreTransitivelyReferencedDoNotBreakTheBuild()
    {
        var cliHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(cliHome);
        CreateUserLocalFileForCurrentSdk(cliHome);

        var projectPath =
            _testAssetsManager
                .CopyTestAsset(TransitiveReferenceNoWorkloadsAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
            .WithWorkingDirectory(projectPath)
            .WithEnvironmentVariable("DOTNET_CLI_HOME", cliHome)
            .WithEnvironmentVariable("HOME", cliHome)
            .WithEnvironmentVariable("DOTNET_ROOT", cliHome)
            .WithEnvironmentVariable("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", Path.Combine(cliHome, "sdk-manifests"))
            .WithEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS", Path.Combine(cliHome, "packs"))
            .WithEnvironmentVariable("DOTNETSDK_WORKLOAD_METADATA_ROOT", Path.Combine(cliHome, "metadata"))
            .Execute()
            .Should()
            // if we did try to restore the esproj in this TestAsset we would fail, so passing means we didn't!
            .Pass();
    }

    private void CreateUserLocalFileForCurrentSdk(string cliHome)
    {
        var result = new DotnetCommand(Log, "--version").Execute();
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            throw new Exception("Failed to get dotnet version");
        }
        var sdkVersion = result.StdOut.Trim();
        var version = Version.Parse(sdkVersion.Split('-')[0]);
        var featureBand = $"{version.Major}.{version.Minor}.{(version.Build / 100) * 100}";

        Directory.CreateDirectory(Path.Combine(cliHome, "sdk-manifests"));
        Directory.CreateDirectory(Path.Combine(cliHome, "packs"));

        File.Create(Path.Combine(cliHome, "userlocal")).Dispose();

        var userlocalPath = Path.Combine(cliHome, "metadata", "workloads", featureBand);
        Directory.CreateDirectory(userlocalPath);
        var userlocalFile = Path.Combine(userlocalPath, "userlocal");
        File.Create(userlocalFile).Dispose();
    }
}
