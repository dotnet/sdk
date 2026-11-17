// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

[TestClass]
public sealed class GivenMSBuildSdkDiscovery : SdkTest
{
    [TestMethod]
    public void NestedInvocationsDiscoverTheirSelectedToolset()
    {
        var toolset = SdkTestContext.Current.ToolsetUnderTest;
        var asset = TestAssetsManager.CopyTestAsset("MSBuildSdkDiscovery", identifier: Guid.NewGuid().ToString("N")).WithSource();
        string nestedDirectory = Path.Combine(asset.Path, "nested");
        string alternateRoot = Path.Combine(asset.Path, "alternate");
        string alternateSdk = Path.Combine(alternateRoot, "sdk", "99.0.100");

        Directory.CreateDirectory(nestedDirectory);
        File.Copy(Path.Combine(asset.Path, "Probe.proj"), Path.Combine(nestedDirectory, "Probe.proj"), overwrite: true);
        CopyDirectory(toolset.SdkFolderUnderTest, alternateSdk);
        CopyDirectory(Path.Combine(toolset.DotNetRoot, "shared"), Path.Combine(alternateRoot, "shared"));
        CopyDirectory(Path.Combine(toolset.DotNetRoot, "host"), Path.Combine(alternateRoot, "host"));

        WriteGlobalJson(asset.Path, toolset.DotNetRoot, toolset.SdkVersion);
        WriteGlobalJson(nestedDirectory, alternateRoot, "99.0.100");

        int invocation = 0;
        // Repeat the server-enabled case with node reuse permitted.
        foreach (var (outOfProc, useServer) in new[] { (false, false), (true, false), (false, true), (false, true) })
        {
            string binlog = Path.Combine(asset.Path, $"{++invocation}.binlog");
            var command = CreateDiscoveryCommand(asset.Path, "msbuild");
            command.WithEnvironmentVariable("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC", outOfProc.ToString());
            command.WithEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", useServer.ToString());
            command.WithEnvironmentVariable("MSBUILDUSESERVER", useServer ? "1" : "0");
            command.WithEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
            var result = command.Execute("Probe.proj", "-t:Probe", $"-nr:{useServer}",
                $"-p:DiscoveryUseServer={useServer}", $"-bl:{binlog}");
            if (File.Exists(binlog))
            {
                TestContext.AddResultFile(binlog);
            }
            result.Should().Pass();

            AssertPaths(asset.Path, toolset.SdkFolderUnderTest);
            AssertPaths(nestedDirectory, alternateSdk);

            var events = BinaryLog.ReadRecords(binlog).Select(record => record.Args).ToArray();
            events.OfType<TargetStartedEventArgs>().Where(args => args.TargetName == "Report")
                .Should().Contain(args => args.BuildEventContext!.NodeId > 1);
        }
    }

    [TestMethod]
    public void EvaluationWithoutBuildDiscoversSdkImports()
    {
        var asset = TestAssetsManager.CopyTestAsset("MSBuildSdkDiscovery").WithSource();
        var toolset = SdkTestContext.Current.ToolsetUnderTest;
        WriteGlobalJson(asset.Path, toolset.DotNetRoot, toolset.SdkVersion);

        var command = CreateDiscoveryCommand(asset.Path, "msbuild");
        command.WithEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", "false");
        command.WithEnvironmentVariable("MSBUILDUSESERVER", "0");
        var result = command.Execute("Probe.proj", "-getProperty:MSBuildToolsPath,MSBuildExtensionsPath,MSBuildSDKsPath,MicrosoftNETBuildTasksAssembly", BinLogArgument([]));
        result.Should().Pass();
        using var output = JsonDocument.Parse(result.StdOut!);
        var properties = output.RootElement.GetProperty("Properties");
        Path.TrimEndingDirectorySeparator(properties.GetProperty("MSBuildToolsPath").GetString()!)
            .Should().Be(toolset.SdkFolderUnderTest);
        Path.TrimEndingDirectorySeparator(properties.GetProperty("MSBuildExtensionsPath").GetString()!)
            .Should().Be(toolset.SdkFolderUnderTest);
        properties.GetProperty("MSBuildSDKsPath").GetString().Should().Be(toolset.SdksPath);
        Path.GetFullPath(properties.GetProperty("MicrosoftNETBuildTasksAssembly").GetString()!)
            .Should().StartWith(toolset.SdksPath + Path.DirectorySeparatorChar);
    }

    [TestMethod]
    public void FileBasedRunDiscoversSdkWithoutEnvironmentDefaults()
    {
        var asset = TestAssetsManager.CopyTestAsset("MSBuildSdkDiscovery").WithSource();
        var toolset = SdkTestContext.Current.ToolsetUnderTest;
        WriteGlobalJson(asset.Path, toolset.DotNetRoot, toolset.SdkVersion);

        CreateDiscoveryCommand(asset.Path, "run", "--file", "Program.cs")
            .Execute(BinLogArgument([]))
            .Should().Pass()
            .And.HaveStdOutContaining("SDK discovery succeeded");
    }

    [TestMethod]
    public void ExplicitEnvironmentOverridesSelectSdkImports()
    {
        var asset = TestAssetsManager.CopyTestAsset("MSBuildSdkDiscovery").WithSource();
        string extensionsPath = Path.Combine(asset.Path, "Extensions");
        string sdksPath = Path.Combine(asset.Path, "Sdks");
        var command = new DotnetCommand(Log, "msbuild") { WorkingDirectory = asset.Path };
        command.EnvironmentToRemove.Add("MSBUILD_EXE_PATH");
        command.WithEnvironmentVariable("MSBuildExtensionsPath", extensionsPath);
        command.WithEnvironmentVariable("MSBuildSDKsPath", sdksPath);
        command.Execute("Override.proj", "-getProperty:DiscoveryExtensionsPath,DiscoverySdkPath", BinLogArgument([]))
            .Should().Pass()
            .And.HaveStdOutContaining(JsonSerializer.Serialize(extensionsPath + Path.DirectorySeparatorChar))
            .And.HaveStdOutContaining(JsonSerializer.Serialize(Path.Combine(sdksPath, "Discovery.Sdk", "Sdk") + Path.DirectorySeparatorChar));
    }

    private DotnetCommand CreateDiscoveryCommand(string workingDirectory, params string[] args)
    {
        var command = new DotnetCommand(Log, args) { WorkingDirectory = workingDirectory };
        command.EnvironmentToRemove.AddRange(["MSBUILD_EXE_PATH", "MSBuildSDKsPath", "MSBuildExtensionsPath"]);
        return command;
    }

    private static void WriteGlobalJson(string directory, string root, string version)
        => File.WriteAllText(Path.Combine(directory, "global.json"), JsonSerializer.Serialize(new
        {
            sdk = new { version, paths = new[] { root }, rollForward = "disable", allowPrerelease = true }
        }));

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (string source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static void AssertPaths(string projectDirectory, string sdkDirectory)
    {
        string[] paths = File.ReadAllLines(Path.Combine(projectDirectory, "paths.txt"));
        paths.Should().HaveCount(4);
        Path.TrimEndingDirectorySeparator(paths[0]).Should().Be(sdkDirectory);
        Path.TrimEndingDirectorySeparator(paths[1]).Should().Be(sdkDirectory);
        Path.TrimEndingDirectorySeparator(paths[2]).Should().Be(Path.Combine(sdkDirectory, "Sdks"));
        paths[3].Should().StartWith(Path.Combine(sdkDirectory, "Sdks") + Path.DirectorySeparatorChar);
    }
}
