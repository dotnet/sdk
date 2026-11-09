// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InstallRootEnumeratorTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetInstalls_DoesNotRequireLoadableHostfxr(bool hasInvalidHostfxr)
    {
        using var testEnv = new TestEnvironment();
        WriteMarker(testEnv.InstallPath, "sdk", "11.0.100-preview.1", "dotnet.dll");
        if (hasInvalidHostfxr)
        {
            WriteMarker(testEnv.InstallPath, Path.Combine("host", "fxr"), "11.0.0", "hostfxr.dll");
            WriteMarker(testEnv.InstallPath, Path.Combine("host", "fxr"), "11.0.0", "libhostfxr.so");
            WriteMarker(testEnv.InstallPath, Path.Combine("host", "fxr"), "11.0.0", "libhostfxr.dylib");
        }

        var installs = DotnetEnvironmentManager.GetExistingSystemInstalls([testEnv.InstallPath]);

        var install = installs.Should().ContainSingle().Subject;
        install.Component.Should().Be(InstallComponent.SDK);
        install.Version.ToString().Should().Be("11.0.100-preview.1");
        install.InstallRoot.Should().Be(new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()));
    }

    [TestMethod]
    [DataRow(InstallComponent.SDK)]
    [DataRow(InstallComponent.Runtime)]
    [DataRow(InstallComponent.ASPNETCore)]
    [DataRow(InstallComponent.WindowsDesktop)]
    internal void GetInstalls_UsesTheSameMarkersAsValidation(InstallComponent component)
    {
        using var testEnv = new TestEnvironment();
        File.WriteAllText(InstallationValidator.GetMuxerPath(testEnv.InstallPath), string.Empty);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        string versionDirectory = new DotnetInstall(root, new ReleaseVersion("11.0.100"), component).ComponentDirectory;
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(versionDirectory, "wrong.deps.json"), string.Empty);

        InstallRootEnumerator.GetInstalls(root).Should().BeEmpty();

        string marker = component == InstallComponent.SDK ? "dotnet.dll" : component.GetFrameworkName() + ".deps.json";
        File.WriteAllText(Path.Combine(versionDirectory, marker), string.Empty);
        var install = InstallRootEnumerator.GetInstalls(root).Should().ContainSingle().Subject;
        install.Component.Should().Be(component);
        new InstallationValidator().Validate(install).Should().BeTrue();

        File.Delete(Path.Combine(versionDirectory, marker));
        InstallRootEnumerator.GetInstalls(root).Should().BeEmpty();
        new InstallationValidator().Validate(install).Should().BeFalse();
    }

    [TestMethod]
    public void GetInstalls_RequiresValidVersionAndKnownFrameworkMarker()
    {
        using var testEnv = new TestEnvironment();
        WriteMarker(testEnv.InstallPath, "sdk", "not-a-version", "dotnet.dll");
        WriteMarker(testEnv.InstallPath, "sdk", "11.0.100", "payload");
        WriteMarker(testEnv.InstallPath, Path.Combine("shared", "Microsoft.NETCore.App"), "11.0.0", "Custom.Framework.deps.json");
        WriteMarker(testEnv.InstallPath, Path.Combine("shared", "Custom.Framework"), "11.0.0", "Custom.Framework.deps.json");
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        InstallRootEnumerator.GetInstalls(root).Should().BeEmpty();

        WriteMarker(testEnv.InstallPath, Path.Combine("shared", "Microsoft.NETCore.App"), "11.0.0", "Microsoft.NETCore.App.deps.json");
        var install = InstallRootEnumerator.GetInstalls(root).Should().ContainSingle().Subject;
        install.Component.Should().Be(InstallComponent.Runtime);
        install.Version.ToString().Should().Be("11.0.0");
    }

    [TestMethod]
    public void GetExistingSystemInstalls_PreservesFolderVersionsRootPriorityAndSorting()
    {
        using var testEnv = new TestEnvironment();
        string otherRoot = Path.Combine(testEnv.TempRoot, "other");
        WriteMarker(testEnv.InstallPath, "sdk", "11.0.100", "dotnet.dll");
        WriteMarker(testEnv.InstallPath, "sdk", "11.0.100", ".version");
        File.WriteAllText(Path.Combine(testEnv.InstallPath, "sdk", "11.0.100", ".version"),
            "commit\n11.0.100\nrid\n11.0.100-servicing.26523.112\n");
        WriteMarker(otherRoot, "sdk", "11.0.100", "dotnet.dll");
        WriteMarker(otherRoot, "sdk", "12.0.100-preview.1", "dotnet.dll");
        WriteMarker(otherRoot, Path.Combine("shared", "Microsoft.NETCore.App"), "11.0.0", "Microsoft.NETCore.App.deps.json");

        var installs = DotnetEnvironmentManager.GetExistingSystemInstalls([testEnv.InstallPath, otherRoot]);

        installs.Select(i => i.Version.ToString()).Should().Equal("12.0.100-preview.1", "11.0.100", "11.0.0");
        installs[1].InstallRoot.Path.Should().Be(testEnv.InstallPath);
        installs.Should().OnlyContain(i => i.InstallRoot.Architecture == InstallerUtilities.GetDefaultInstallArchitecture());
    }

    [TestMethod]
    public void GetInstalls_ReturnsEmptyForMissingOrEmptyRoot()
    {
        using var testEnv = new TestEnvironment();

        DotnetEnvironmentManager.GetExistingSystemInstalls(
            [Path.Combine(testEnv.TempRoot, "missing"), testEnv.InstallPath]).Should().BeEmpty();
    }

    private static void WriteMarker(string root, string componentDirectory, string version, string marker)
    {
        string versionDirectory = Path.Combine(root, componentDirectory, version);
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(versionDirectory, marker), string.Empty);
    }
}
