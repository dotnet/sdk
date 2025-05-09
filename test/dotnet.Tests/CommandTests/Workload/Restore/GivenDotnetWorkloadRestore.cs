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
        var cliHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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

        Log.WriteLine($"[DEBUG] DOTNET_CLI_HOME = {Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")}");
        Log.WriteLine($"[DEBUG] HOME = {Environment.GetEnvironmentVariable("HOME")}");
        Log.WriteLine($"[DEBUG] DOTNET_ROOT = {Environment.GetEnvironmentVariable("DOTNET_ROOT")}");

        var projectPath =
            _testAssetsManager
                .CopyTestAsset(TransitiveReferenceNoWorkloadsAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
            .WithWorkingDirectory(projectPath)
            .WithEnvironmentVariable("DOTNET_ROOT", cliHome)
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

        var userlocalPath = Path.Combine(cliHome, "metadata", "workloads", featureBand);
        Directory.CreateDirectory(userlocalPath);

        var userlocalFile = Path.Combine(userlocalPath, "userlocal");
        File.Create(userlocalFile).Dispose();

        Log.WriteLine($"[DEBUG] Directory exists({userlocalPath}): {Directory.Exists(userlocalPath)}");
        Log.WriteLine($"[DEBUG] File exists({userlocalFile}): {File.Exists(userlocalFile)}");
        Log.WriteLine($"[DEBUG] userlocal path writable: {IsDirectoryWritable(userlocalPath)}");
    }

    private bool IsDirectoryWritable(string path)
    {
        try
        {
            var testFile = Path.Combine(path, Path.GetRandomFileName());
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
