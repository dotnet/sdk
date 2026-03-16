// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class InstallerOrchestratorTests
{
    private static readonly string TestRoot = OperatingSystem.IsWindows() ? @"C:\dotnet" : "/dotnet";
    private static readonly string TestAltRoot = OperatingSystem.IsWindows() ? @"C:\other-dotnet" : "/other-dotnet";

    #region InstallAlreadyExists

    [Fact]
    public void InstallAlreadyExists_ReturnsTrueForDuplicateInstall()
    {
        var manifestData = ManifestWithInstallation(TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.SDK);

        InstallerOrchestratorSingleton.InstallAlreadyExists(manifestData, install).Should().BeTrue();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForDifferentVersion()
    {
        var manifestData = ManifestWithInstallation(TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestRoot, "10.0.101", InstallComponent.SDK);

        InstallerOrchestratorSingleton.InstallAlreadyExists(manifestData, install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForDifferentComponent()
    {
        var manifestData = ManifestWithInstallation(TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.Runtime);

        InstallerOrchestratorSingleton.InstallAlreadyExists(manifestData, install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForDifferentRoot()
    {
        var manifestData = ManifestWithInstallation(TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestAltRoot, "10.0.100", InstallComponent.SDK);

        InstallerOrchestratorSingleton.InstallAlreadyExists(manifestData, install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForEmptyManifest()
    {
        var manifestData = new DotnetupManifestData();
        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.SDK);

        InstallerOrchestratorSingleton.InstallAlreadyExists(manifestData, install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_DetectsDuplicateAmongMultipleInstallations()
    {
        var manifestData = new DotnetupManifestData
        {
            DotnetRoots =
            [
                new DotnetRootEntry
                {
                    Path = TestRoot,
                    Architecture = InstallArchitecture.x64,
                    Installations =
                    [
                        new Installation { Component = InstallComponent.SDK, Version = "9.0.100" },
                        new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" },
                        new Installation { Component = InstallComponent.SDK, Version = "10.0.100" }
                    ]
                }
            ]
        };

        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.SDK);
        InstallerOrchestratorSingleton.InstallAlreadyExists(manifestData, install).Should().BeTrue();
    }

    #endregion

    #region IsRootInManifest

    [Fact]
    public void IsRootInManifest_ReturnsTrueWhenRootExists()
    {
        var manifestData = ManifestWithInstallation(TestRoot, InstallComponent.SDK, "10.0.100");
        var root = new DotnetInstallRoot(TestRoot, InstallArchitecture.x64);

        InstallerOrchestratorSingleton.IsRootInManifest(manifestData, root).Should().BeTrue();
    }

    [Fact]
    public void IsRootInManifest_ReturnsFalseWhenRootDoesNotExist()
    {
        var manifestData = ManifestWithInstallation(TestRoot, InstallComponent.SDK, "10.0.100");
        var root = new DotnetInstallRoot(TestAltRoot, InstallArchitecture.x64);

        InstallerOrchestratorSingleton.IsRootInManifest(manifestData, root).Should().BeFalse();
    }

    [Fact]
    public void IsRootInManifest_ReturnsFalseForEmptyManifest()
    {
        var manifestData = new DotnetupManifestData();
        var root = new DotnetInstallRoot(TestRoot, InstallArchitecture.x64);

        InstallerOrchestratorSingleton.IsRootInManifest(manifestData, root).Should().BeFalse();
    }

    #endregion

    #region HasDotnetArtifacts

    [Fact]
    public void HasDotnetArtifacts_ReturnsFalseForNullPath()
    {
        InstallerOrchestratorSingleton.HasDotnetArtifacts(null).Should().BeFalse();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsFalseForNonexistentPath()
    {
        InstallerOrchestratorSingleton.HasDotnetArtifacts(Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N")))
            .Should().BeFalse();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsTrueWhenSdkDirectoryExists()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk"));

        InstallerOrchestratorSingleton.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsTrueWhenSharedDirectoryExists()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "shared"));

        InstallerOrchestratorSingleton.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsTrueWhenDotnetExeExists()
    {
        using var testEnv = new TestEnvironment();
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        File.WriteAllText(Path.Combine(testEnv.InstallPath, exeName), "fake");

        InstallerOrchestratorSingleton.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsFalseForEmptyDirectory()
    {
        using var testEnv = new TestEnvironment();

        InstallerOrchestratorSingleton.HasDotnetArtifacts(testEnv.InstallPath).Should().BeFalse();
    }

    #endregion

    #region InstallMultiple

    [Fact]
    public void InstallMultiple_EmptyList_ReturnsEmpty()
    {
        var results = InstallerOrchestratorSingleton.Instance.InstallMultiple([], noProgress: true);
        results.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static DotnetInstall MakeInstall(string path, string version, InstallComponent component)
    {
        return new DotnetInstall(
            new DotnetInstallRoot(path, InstallArchitecture.x64),
            new ReleaseVersion(version),
            component);
    }

    private static DotnetupManifestData ManifestWithInstallation(string rootPath, InstallComponent component, string version)
    {
        return new DotnetupManifestData
        {
            DotnetRoots =
            [
                new DotnetRootEntry
                {
                    Path = rootPath,
                    Architecture = InstallArchitecture.x64,
                    Installations =
                    [
                        new Installation { Component = component, Version = version }
                    ]
                }
            ]
        };
    }

    #endregion
}
