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
        Directory.CreateDirectory(alternateSdk);
        foreach (string file in Directory.EnumerateFiles(toolset.SdkFolderUnderTest))
        {
            File.Copy(file, Path.Combine(alternateSdk, Path.GetFileName(file)));
        }
        foreach (string directory in Directory.EnumerateDirectories(toolset.SdkFolderUnderTest))
        {
            Directory.CreateSymbolicLink(Path.Combine(alternateSdk, Path.GetFileName(directory)), directory);
        }
        Directory.CreateSymbolicLink(Path.Combine(alternateRoot, "shared"), Path.Combine(toolset.DotNetRoot, "shared"));
        Directory.CreateSymbolicLink(Path.Combine(alternateRoot, "host"), Path.Combine(toolset.DotNetRoot, "host"));

        WriteGlobalJson(asset.Path, toolset.DotNetRoot, toolset.SdkVersion);
        WriteGlobalJson(nestedDirectory, alternateRoot, "99.0.100");

        int invocation = 0;
        foreach (var (outOfProc, useServer, nodeReuse) in new[]
        {
            (false, false, false),
            (true, false, false),
            (false, true, false),
            (false, true, true)
        })
        {
            string binlog = Path.Combine(asset.Path, $"{++invocation}.binlog");
            var command = CreateDiscoveryCommand(asset.Path, "msbuild");
            command.WithEnvironmentVariable("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC", outOfProc.ToString());
            command.WithEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", useServer.ToString());
            command.WithEnvironmentVariable("MSBUILDUSESERVER", useServer ? "1" : "0");
            command.WithEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
            var result = command.Execute("Probe.proj", "-t:Probe", $"-nr:{nodeReuse}",
                $"-p:DiscoveryUseServer={useServer}", $"-p:DiscoveryNodeReuse={nodeReuse}", $"-bl:{binlog}");
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
        string sdkDirectory = TestPathUtility.ResolveTempPrefixLink(toolset.SdkFolderUnderTest);
        string sdksDirectory = TestPathUtility.ResolveTempPrefixLink(toolset.SdksPath);
        Path.TrimEndingDirectorySeparator(properties.GetProperty("MSBuildToolsPath").GetString()!)
            .Should().Be(sdkDirectory);
        Path.TrimEndingDirectorySeparator(properties.GetProperty("MSBuildExtensionsPath").GetString()!)
            .Should().Be(sdkDirectory);
        properties.GetProperty("MSBuildSDKsPath").GetString().Should().Be(sdksDirectory);
        Path.GetFullPath(properties.GetProperty("MicrosoftNETBuildTasksAssembly").GetString()!)
            .Should().StartWith(sdksDirectory + Path.DirectorySeparatorChar);
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

    private static void AssertPaths(string projectDirectory, string sdkDirectory)
    {
        sdkDirectory = TestPathUtility.ResolveTempPrefixLink(sdkDirectory);
        string[] paths = File.ReadAllLines(Path.Combine(projectDirectory, "paths.txt"));
        paths.Should().HaveCount(4);
        Path.TrimEndingDirectorySeparator(paths[0]).Should().Be(sdkDirectory);
        Path.TrimEndingDirectorySeparator(paths[1]).Should().Be(sdkDirectory);
        Path.TrimEndingDirectorySeparator(paths[2]).Should().Be(Path.Combine(sdkDirectory, "Sdks"));
        paths[3].Should().StartWith(Path.Combine(sdkDirectory, "Sdks") + Path.DirectorySeparatorChar);
    }
}
