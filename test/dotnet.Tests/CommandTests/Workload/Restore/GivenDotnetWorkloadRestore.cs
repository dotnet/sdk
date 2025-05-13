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
        var Home = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        if (string.IsNullOrEmpty(Home))
        {
            throw new InvalidOperationException("DOTNET_CLI_HOME is not set in the environment.");
        }
        var cliHome = Path.Combine(Home, ".dotnet");
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
        .Execute("--verbosity", "diag")
        .Should()
        // if we did try to restore the dcproj in this TestAsset we would fail, so passing means we didn't!
        .Pass();
    }

    [Fact]
    public void ProjectsThatDoNotSupportWorkloadsAndAreTransitivelyReferencedDoNotBreakTheBuild()
    {
        var Home = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        if (string.IsNullOrEmpty(Home))
        {
            throw new InvalidOperationException("DOTNET_CLI_HOME is not set in the environment.");
        }
        Directory.CreateDirectory(cliHome);
        CreateUserLocalFileForCurrentSdk(cliHome);

        IsRunningInContainer();
        Log.WriteLine($"[Debug] OSDescription = {RuntimeInformation.OSDescription}");

        var projectPath =
            _testAssetsManager
                .CopyTestAsset(TransitiveReferenceNoWorkloadsAssetName)
                .WithSource()
                .Path;

        new DotnetWorkloadCommand(Log, "restore")
            .WithWorkingDirectory(projectPath)
            .WithEnvironmentVariable("DOTNET_CLI_HOME", cliHome)
            .Execute("--verbosity", "diag")
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
        File.Create(Path.Combine(userlocalPath, "userlocal")).Dispose();

        var userlocalPath1 = Path.Combine(cliHome, "metadata", "workloads", featureBand, "installertype");
        Directory.CreateDirectory(userlocalPath1);
        File.Create(Path.Combine(userlocalPath1, "userlocal")).Dispose();
    }

    bool IsRunningInContainer()
    {
        // Check the DOTNET_RUNNING_IN_CONTAINER environment variable
        var envVar = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        Log.WriteLine($"[Debug] DOTNET_RUNNING_IN_CONTAINER = {(envVar ?? "null")}");
        if (!string.IsNullOrEmpty(envVar) && envVar.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            Log.WriteLine("[Debug] Container detected via DOTNET_RUNNING_IN_CONTAINER environment variable.");
            return true;
        }

        // Check for the presence of /.dockerenv file (common in Docker containers)
        if (File.Exists("/.dockerenv"))
           {
            Log.WriteLine("[Debug] Container detected via /.dockerenv file.");
            return true;
        }

        // Check for the presence of /run/.containerenv file (used in some container runtimes)
        if (File.Exists("/run/.containerenv"))
        {
            Log.WriteLine("[Debug] Container detected via /run/.containerenv file.");
            return true;
        }

        // Inspect /proc/1/cgroup for container-related keywords
        try
        {
            var lines = File.ReadAllLines("/proc/1/cgroup");
            foreach (var line in lines)
            {
                Log.WriteLine($"[Debug] /proc/1/cgroup line: {line}");
                if (line.Contains("docker") || line.Contains("kubepods") || line.Contains("containerd"))
                {
                    Log.WriteLine("[Debug] Container detected via /proc/1/cgroup content.");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine($"[Debug] Failed to read /proc/1/cgroup: {ex.Message}");
        }

        // No container indicators found
        Log.WriteLine("[Debug] No container indicators found. Assuming not running in a container.");
        return false;
    }
}
