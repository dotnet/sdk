// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class HostFxrQueryProcessTests : SdkTest
{
    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"SdkInfo\":null,\"RuntimeInfo\":[]}")]
    [DataRow("{\"SdkInfo\":[],\"RuntimeInfo\":null}")]
    public void QueryResult_RejectsMissingOrNullCollections(string json)
    {
        Assert.ThrowsExactly<JsonException>(() =>
            JsonSerializer.Deserialize(json, HostFxrQueryJsonContext.Default.HostFxrQueryResult));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("relative-root")]
    public void PrivateQuery_RejectsInvalidArgumentsWithoutNormalStartup(string installRoot)
    {
        var (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            [HostFxrQueryProcess.Command, installRoot], captureOutput: true, timeoutMilliseconds: 10_000);

        exitCode.Should().Be(1);
        output.Should().Contain("The hostfxr query requires one absolute install root.");
        output.Should().NotContain("Telemetry");
    }

    [TestMethod]
    public void PrivateQuery_MissingArgumentDoesNotRunNormalCommands()
    {
        var (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            [HostFxrQueryProcess.Command], captureOutput: true, timeoutMilliseconds: 10_000);

        exitCode.Should().Be(1);
        output.Should().Contain("The hostfxr query requires one absolute install root.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GetInfo_PreservesNativeSdkAndRuntimePaths_AndReleasesHostFxr()
    {
        using var testEnv = new TestEnvironment();
        string root = Path.Combine(testEnv.InstallPath, "root with spaces [query] \u00e9");
        string hostRoot = Path.GetDirectoryName(SelfUpdateTestFiles.DotnetHostPath)!;
        string sourceFxr = Directory.EnumerateFiles(Path.Combine(hostRoot, "host", "fxr"), "hostfxr.dll", SearchOption.AllDirectories).First();
        string fxrDirectory = Path.Combine(root, "host", "fxr", Path.GetFileName(Path.GetDirectoryName(sourceFxr)!));
        Directory.CreateDirectory(fxrDirectory);
        string hostFxrPath = Path.Combine(fxrDirectory, "hostfxr.dll");
        File.Copy(sourceFxr, hostFxrPath);
        // A non-executable muxer proves enumeration uses hostfxr, not dotnet --list-*.
        File.WriteAllText(InstallationValidator.GetMuxerPath(root), string.Empty);

        string sdkPath = Path.Combine(root, "sdk", "10.0.100");
        Directory.CreateDirectory(sdkPath);
        File.WriteAllText(Path.Combine(sdkPath, "dotnet.dll"), string.Empty);
        string previewSdkPath = Path.Combine(root, "sdk", "11.0.100-rc.2.26504.105");
        Directory.CreateDirectory(previewSdkPath);
        File.WriteAllText(Path.Combine(previewSdkPath, "dotnet.dll"), string.Empty);
        string runtimePath = Path.Combine(root, "shared", "Microsoft.NETCore.App");
        Directory.CreateDirectory(Path.Combine(runtimePath, "10.0.0"));
        File.WriteAllText(Path.Combine(runtimePath, "10.0.0", "Microsoft.NETCore.App.deps.json"), "{}");
        string aspNetRuntimePath = Path.Combine(root, "shared", "Microsoft.AspNetCore.App");
        Directory.CreateDirectory(Path.Combine(aspNetRuntimePath, "10.0.0"));
        File.WriteAllText(Path.Combine(aspNetRuntimePath, "10.0.0", "Microsoft.AspNetCore.App.deps.json"), "{}");

        var info = HostFxrQueryProcess.GetInfo(root, DotnetupTestUtilities.GetDotnetupExecutablePath());

        info.SdkInfo.Select(sdk => (sdk.Version.ToString(), sdk.Path)).Should().Equal(
            ("10.0.100", sdkPath),
            ("11.0.100-rc.2.26504.105", previewSdkPath));
        info.RuntimeInfo.Select(runtime => (runtime.Name, runtime.Version.ToString(), runtime.Path))
            .Should().Equal(
                ("Microsoft.AspNetCore.App", "10.0.0", aspNetRuntimePath),
                ("Microsoft.NETCore.App", "10.0.0", runtimePath));
        using (File.Open(hostFxrPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            manifest.AddInstallation(new DotnetInstallRoot(root, InstallerUtilities.GetDefaultInstallArchitecture()), new Installation
            {
                Component = InstallComponent.SDK,
                Version = "10.0.100",
                Subcomponents = [],
            });
        }
        var (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            ["list", "--format", "json", "--manifest-path", testEnv.ManifestPath],
            captureOutput: true,
            workingDirectory: testEnv.TempRoot,
            environmentVariables: new Dictionary<string, string>
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = Path.Combine(testEnv.TempRoot, "data"),
                ["DOTNET_TESTHOOK_DEFAULT_DOTNET_PATH"] = root,
            },
            timeoutMilliseconds: 30_000);

        exitCode.Should().Be(0, output);
        output.Should().Contain("\"isValid\": true", "archive validation must relaunch dotnetup, not the test host");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GetInfo_ReportsChildFailure()
    {
        using var testEnv = new TestEnvironment();
        string fxrDirectory = Path.Combine(testEnv.InstallPath, "host", "fxr", "10.0.0");
        Directory.CreateDirectory(fxrDirectory);
        File.WriteAllText(Path.Combine(fxrDirectory, "hostfxr.dll"), string.Empty);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HostFxrQueryProcess.GetInfo(testEnv.InstallPath, DotnetupTestUtilities.GetDotnetupExecutablePath()));

        exception.Message.Should().Contain("Hostfxr query exited with code 1").And.Contain("Hostfxr query failed");
    }
}
