// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class HostFxrQueryProcessTests
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
                ["DOTNET_DOTNETUP_DATA_DIR"] = Path.Combine(testEnv.TempRoot, "data"),
                ["DOTNET_TESTHOOK_DEFAULT_DOTNET_PATH"] = root,
            },
            timeoutMilliseconds: 30_000);

        exitCode.Should().Be(0, output);
        output.Should().Contain("\"isValid\": true", "archive validation must relaunch dotnetup, not the test host");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("corrupt")]
    [DataRow("missing")]
    [DataRow("nested")]
    [DataRow("unversioned")]
    public void GetInfo_ReportsChildFailure(string scenario)
    {
        using var testEnv = new TestEnvironment();
        CreateSdkLayout(testEnv.InstallPath);
        string hostFxr = CopyHostFxrWithoutEnvironmentInfo(testEnv.InstallPath);
        if (scenario == "corrupt")
        {
            File.WriteAllText(hostFxr, string.Empty);
        }
        else if (scenario == "missing")
        {
            File.Delete(hostFxr);
        }
        else
        {
            string directory = scenario == "nested"
                ? Path.Combine(Path.GetDirectoryName(hostFxr)!, "nested")
                : Path.Combine(testEnv.InstallPath, "host", "fxr", "not-a-version");
            Directory.CreateDirectory(directory);
            File.Move(hostFxr, Path.Combine(directory, "hostfxr.dll"));
        }

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HostFxrQueryProcess.GetInfo(testEnv.InstallPath, DotnetupTestUtilities.GetDotnetupExecutablePath()));

        exception.Message.Should().Contain("Hostfxr query exited with code 1").And.Contain("Hostfxr query failed");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void PrivateQuery_UnavailableEnvironmentInfoHasDistinctExitCode()
    {
        using var testEnv = new TestEnvironment();
        CopyHostFxrWithoutEnvironmentInfo(testEnv.InstallPath);

        var (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            [HostFxrQueryProcess.Command, testEnv.InstallPath], captureOutput: true, timeoutMilliseconds: 10_000);

        exitCode.Should().Be(HostFxrQueryProcess.EnvironmentInfoUnavailableExitCode);
        output.Should().Contain("does not export hostfxr_get_dotnet_environment_info");
        output.Should().NotContain("\"SdkInfo\"").And.NotContain("Telemetry");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GetInfo_UnavailableEnvironmentInfoThrowsDedicatedException_AndReleasesHostFxr()
    {
        using var testEnv = new TestEnvironment();
        string hostFxr = CopyHostFxrWithoutEnvironmentInfo(testEnv.InstallPath);

        Assert.ThrowsExactly<HostFxrEnvironmentInfoUnavailableException>(() =>
            HostFxrQueryProcess.GetInfo(testEnv.InstallPath, DotnetupTestUtilities.GetDotnetupExecutablePath()));

        using (File.Open(hostFxr, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(InstallMechanism.Archive)]
    [DataRow(InstallMechanism.WindowsInstallerBundle)]
    public void Validate_NativeHostWithoutEnvironmentInfoUsesInstalledLayout(InstallMechanism mechanism)
    {
        using var testEnv = new TestEnvironment();
        CopyHostFxrWithoutEnvironmentInfo(testEnv.InstallPath);
        CreateSdkLayout(testEnv.InstallPath);
        var install = new DotnetInstall(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            new ReleaseVersion("10.0.100"), InstallComponent.SDK);
        var validator = new InstallationValidator(mechanism,
            root => HostFxrQueryProcess.GetInfo(root, DotnetupTestUtilities.GetDotnetupExecutablePath()));

        validator.Validate(install, out string? failure).Should().BeTrue();
        failure.Should().BeNull();
        validator.Validate(install with { Version = new ReleaseVersion("10.0.101") }).Should().BeFalse();
        validator.Validate(install with { Component = InstallComponent.Runtime }).Should().BeFalse();
        File.Delete(InstallationValidator.GetMuxerPath(testEnv.InstallPath));
        validator.Validate(install).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("installed")]
    [DataRow("missing-component")]
    [DataRow("empty-component")]
    [DataRow("invalid-version")]
    [DataRow("missing-muxer")]
    public void GetInstalls_SkipsUnavailableEnumerationOnlyWithInstalledLayout(string scenario)
    {
        using var testEnv = new TestEnvironment();
        string hostFxr = CopyHostFxrWithoutEnvironmentInfo(testEnv.InstallPath);
        if (scenario != "missing-component")
        {
            CreateSdkLayout(testEnv.InstallPath);
            string sdkPath = Path.Combine(testEnv.InstallPath, "sdk", "10.0.100");
            if (scenario == "empty-component")
            {
                File.Delete(Path.Combine(sdkPath, "dotnet.dll"));
            }
            else if (scenario == "invalid-version")
            {
                Directory.Move(sdkPath, Path.Combine(testEnv.InstallPath, "sdk", "not-a-version"));
            }
            else if (scenario == "missing-muxer")
            {
                File.Delete(InstallationValidator.GetMuxerPath(testEnv.InstallPath));
            }
        }
        if (scenario == "installed")
        {
            HostFxrWrapper.getInstalls(testEnv.InstallPath).Should().BeEmpty();
        }
        else
        {
            Assert.ThrowsExactly<HostFxrEnvironmentInfoUnavailableException>(() =>
                HostFxrWrapper.getInstalls(testEnv.InstallPath));
        }

        using (File.Open(hostFxr, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }

    private static void CreateSdkLayout(string root)
    {
        File.WriteAllText(InstallationValidator.GetMuxerPath(root), string.Empty);
        string sdkPath = Path.Combine(root, "sdk", "10.0.100");
        Directory.CreateDirectory(sdkPath);
        File.WriteAllText(Path.Combine(sdkPath, "dotnet.dll"), string.Empty);
    }

    private static string CopyHostFxrWithoutEnvironmentInfo(string root)
    {
        string hostRoot = Path.GetDirectoryName(SelfUpdateTestFiles.DotnetHostPath)!;
        string sourceFxr = Directory.EnumerateFiles(Path.Combine(hostRoot, "host", "fxr"), "hostfxr.dll", SearchOption.AllDirectories).First();
        string fxrDirectory = Path.Combine(root, "host", "fxr", Path.GetFileName(Path.GetDirectoryName(sourceFxr)!));
        Directory.CreateDirectory(fxrDirectory);
        byte[] library = File.ReadAllBytes(sourceFxr);
        int exportOffset = library.AsSpan().IndexOf("hostfxr_get_dotnet_environment_info"u8);
        exportOffset.Should().BeGreaterThanOrEqualTo(0);
        // Rename only the copied export to model a loadable pre-.NET 6 hostfxr deterministically.
        library[exportOffset] = (byte)'H';
        string destination = Path.Combine(fxrDirectory, "hostfxr.dll");
        File.WriteAllBytes(destination, library);
        return destination;
    }
}
