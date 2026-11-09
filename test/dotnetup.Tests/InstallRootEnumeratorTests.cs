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

        var installs = DotnetEnvironmentManager.GetExistingSystemInstalls([NativeRoot(testEnv.InstallPath)]);

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
    public void GetExistingSystemInstalls_DeduplicatesByFolderAndKeepsRootPriority()
    {
        using var testEnv = new TestEnvironment();
        string otherRoot = Path.Combine(testEnv.TempRoot, "other");
        WriteMarker(testEnv.InstallPath, "sdk", "11.0.100", "dotnet.dll");
        WriteVersionFile(testEnv.InstallPath, "sdk", "11.0.100", "commit\n11.0.100\nrid\n11.0.100-servicing.26523.112\n11.0.100");
        WriteMarker(otherRoot, "sdk", "11.0.100", "dotnet.dll");
        WriteMarker(otherRoot, "sdk", "12.0.100-preview.1", "dotnet.dll");
        WriteMarker(otherRoot, Path.Combine("shared", "Microsoft.NETCore.App"), "11.0.0", "Microsoft.NETCore.App.deps.json");

        var installs = DotnetEnvironmentManager.GetExistingSystemInstalls([NativeRoot(testEnv.InstallPath), NativeRoot(otherRoot)]);

        installs.Select(i => i.LayoutVersion.ToString()).Should().Equal("12.0.100-preview.1", "11.0.100", "11.0.0");
        installs[1].InstallRoot.Path.Should().Be(testEnv.InstallPath);
        installs[1].Version.ToString().Should().Be("11.0.100-servicing.26523.112");
        installs.Should().OnlyContain(i => i.InstallRoot.Architecture == InstallerUtilities.GetDefaultInstallArchitecture());
    }

    [TestMethod]
    [DataRow("10.0.403", "commit\n10.0.403\nwin-x64\n10.0.403-servicing.26523.112\n10.0.400", "10.0.403-servicing.26523.112", "10.0.403")]
    [DataRow("11.0.100", "commit\n11.0.100\nosx-arm64\n11.0.100-rtm.26552.115\n11.0.100", "11.0.100-rtm.26552.115", "11.0.100")]
    [DataRow("12.0.100-alpha.1.26480.102", "commit\n12.0.100-alpha.1.26480.102\nwin-x64\n12.0.100-alpha.1.26480.102\n12.0.100", "12.0.100-alpha.1.26480.102", null)]
    [DataRow("10.0.403", "commit\n10.0.402\nwin-x64\n10.0.402-servicing.26423.113\n10.0.400", "10.0.403", null)]
    [DataRow("10.0.403", "commit\n10.0.403\nwin-x64\n10.0.404-servicing.26423.113\n10.0.400", "10.0.403", null)]
    [DataRow("10.0.403", "commit\n10.0.403", "10.0.403", null)]
    [DataRow("10.0.403", null, "10.0.403", null)]
    public void GetInstalls_UsesTheSdkBuildIdentityFromItsVersionFile(string folder, string? versionFile, string version, string? productVersion)
    {
        using var testEnv = new TestEnvironment();
        WriteMarker(testEnv.InstallPath, "sdk", folder, "dotnet.dll");
        if (versionFile is not null)
        {
            WriteVersionFile(testEnv.InstallPath, "sdk", folder, versionFile);
        }

        var install = InstallRootEnumerator.GetInstalls(NativeRoot(testEnv.InstallPath)).Should().ContainSingle().Subject;

        install.Version.ToString().Should().Be(version);
        (install.ProductVersion?.ToString()).Should().Be(productVersion);
        install.LayoutVersion.ToString().Should().Be(folder);
        InstallationValidator.PassesValidationSmokeCheck(install).Should().BeTrue();
    }

    [TestMethod]
    public void GetInstalls_UsesTheFolderVersionForRuntimes()
    {
        using var testEnv = new TestEnvironment();
        string framework = Path.Combine("shared", "Microsoft.NETCore.App");
        WriteMarker(testEnv.InstallPath, framework, "10.0.12", "Microsoft.NETCore.App.deps.json");
        WriteVersionFile(testEnv.InstallPath, framework, "10.0.12", "95017c71\n10.0.12");

        var install = InstallRootEnumerator.GetInstalls(NativeRoot(testEnv.InstallPath)).Should().ContainSingle().Subject;

        install.Version.ToString().Should().Be("10.0.12");
        install.ProductVersion.Should().BeNull();
    }

    [TestMethod]
    public void GetExistingSystemInstalls_SkipsRootsOfOtherArchitectures()
    {
        using var testEnv = new TestEnvironment();
        string foreignRoot = Path.Combine(testEnv.TempRoot, "foreign");
        WriteMarker(foreignRoot, "sdk", "11.0.100", "dotnet.dll");
        WriteMarker(foreignRoot, "sdk", "12.0.100", "dotnet.dll");
        WriteMarker(testEnv.InstallPath, "sdk", "11.0.100", "dotnet.dll");
        InstallArchitecture foreignArch = Enum.GetValues<InstallArchitecture>()
            .First(arch => arch != InstallerUtilities.GetDefaultInstallArchitecture());

        // The foreign root comes first, so it would win deduplication if it were enumerated.
        var installs = DotnetEnvironmentManager.GetExistingSystemInstalls(
            [new DotnetInstallRoot(foreignRoot, foreignArch), NativeRoot(testEnv.InstallPath)]);

        var install = installs.Should().ContainSingle().Subject;
        install.Version.ToString().Should().Be("11.0.100");
        install.InstallRoot.Should().Be(NativeRoot(testEnv.InstallPath));
    }

    [TestMethod]
    public void GetInstalls_ReturnsEmptyForMissingOrEmptyRoot()
    {
        using var testEnv = new TestEnvironment();

        DotnetEnvironmentManager.GetExistingSystemInstalls(
            [NativeRoot(Path.Combine(testEnv.TempRoot, "missing")), NativeRoot(testEnv.InstallPath)]).Should().BeEmpty();
    }

    private static DotnetInstallRoot NativeRoot(string path) => new(path, InstallerUtilities.GetDefaultInstallArchitecture());

    private static void WriteMarker(string root, string componentDirectory, string version, string marker)
    {
        string versionDirectory = Path.Combine(root, componentDirectory, version);
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(versionDirectory, marker), string.Empty);
    }

    private static void WriteVersionFile(string root, string componentDirectory, string version, string content) =>
        File.WriteAllText(Path.Combine(root, componentDirectory, version, ".version"), content);
}
