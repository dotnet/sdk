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
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.SDK);

        using var mutex = testEnv.AcquireMutex();
        manifest.InstallAlreadyExists(install).Should().BeTrue();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForDifferentVersion()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestRoot, "10.0.101", InstallComponent.SDK);

        using var mutex = testEnv.AcquireMutex();
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForDifferentComponent()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.Runtime);

        using var mutex = testEnv.AcquireMutex();
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForDifferentRoot()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, TestRoot, InstallComponent.SDK, "10.0.100");
        var install = MakeInstall(TestAltRoot, "10.0.100", InstallComponent.SDK);

        using var mutex = testEnv.AcquireMutex();
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_ReturnsFalseForEmptyManifest()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.SDK);

        using var mutex = testEnv.AcquireMutex();
        manifest.InstallAlreadyExists(install).Should().BeFalse();
    }

    [Fact]
    public void InstallAlreadyExists_DetectsDuplicateAmongMultipleInstallations()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(TestRoot, InstallArchitecture.x64);

        using var mutex = testEnv.AcquireMutex();
        manifest.AddInstallation(root, new Installation { Component = InstallComponent.SDK, Version = "9.0.100" });
        manifest.AddInstallation(root, new Installation { Component = InstallComponent.Runtime, Version = "9.0.0" });
        manifest.AddInstallation(root, new Installation { Component = InstallComponent.SDK, Version = "10.0.100" });

        var install = MakeInstall(TestRoot, "10.0.100", InstallComponent.SDK);
        manifest.InstallAlreadyExists(install).Should().BeTrue();
    }

    #endregion

    #region IsRootTracked

    [Fact]
    public void IsRootTracked_ReturnsTrueWhenRootExists()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, TestRoot, InstallComponent.SDK, "10.0.100");
        var root = new DotnetInstallRoot(TestRoot, InstallArchitecture.x64);

        using var mutex = testEnv.AcquireMutex();
        manifest.IsRootTracked(root).Should().BeTrue();
    }

    [Fact]
    public void IsRootTracked_ReturnsFalseWhenRootDoesNotExist()
    {
        using var testEnv = new TestEnvironment();
        var manifest = CreateManifestWithInstallation(testEnv, TestRoot, InstallComponent.SDK, "10.0.100");
        var root = new DotnetInstallRoot(TestAltRoot, InstallArchitecture.x64);

        using var mutex = testEnv.AcquireMutex();
        manifest.IsRootTracked(root).Should().BeFalse();
    }

    [Fact]
    public void IsRootTracked_ReturnsFalseForEmptyManifest()
    {
        using var testEnv = new TestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(TestRoot, InstallArchitecture.x64);

        using var mutex = testEnv.AcquireMutex();
        manifest.IsRootTracked(root).Should().BeFalse();
    }

    #endregion

    #region HasDotnetArtifacts

    [Fact]
    public void HasDotnetArtifacts_ReturnsFalseForNullPath()
    {
        DotnetupSharedManifest.HasDotnetArtifacts(null).Should().BeFalse();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsFalseForNonexistentPath()
    {
        DotnetupSharedManifest.HasDotnetArtifacts(Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N")))
            .Should().BeFalse();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsTrueWhenSdkDirectoryExists()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk"));

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsTrueWhenSharedDirectoryExists()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "shared"));

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsTrueWhenDotnetExeExists()
    {
        using var testEnv = new TestEnvironment();
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        File.WriteAllText(Path.Combine(testEnv.InstallPath, exeName), "fake");

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeTrue();
    }

    [Fact]
    public void HasDotnetArtifacts_ReturnsFalseForEmptyDirectory()
    {
        using var testEnv = new TestEnvironment();

        DotnetupSharedManifest.HasDotnetArtifacts(testEnv.InstallPath).Should().BeFalse();
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

    private static DotnetupSharedManifest CreateManifestWithInstallation(TestEnvironment testEnv, string rootPath, InstallComponent component, string version)
    {
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(rootPath, InstallArchitecture.x64);
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        manifest.AddInstallation(root, new Installation { Component = component, Version = version });
        return manifest;
    }

    #endregion
}
