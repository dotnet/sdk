// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests that macOS install_location registrations are read the way the host reads them.
/// The layouts follow https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md.
/// </summary>
[TestClass]
public class MacOSRegisteredRootTests
{
    [TestMethod]
    [DataRow(InstallArchitecture.arm64, "arm64-root")]
    [DataRow(InstallArchitecture.x64, "x64-root")]
    public void DualArchitectureRegistration_UsesOnlyTheProcessArchitectureFile(InstallArchitecture processArch, string expectedRoot)
    {
        using var testEnv = new TestEnvironment();
        var layout = new RegistrationLayout(testEnv.TempRoot);

        // The documented arm64 Mac layout: the arch-less file points at x64 for downlevel apphosts.
        layout.Register("install_location", "x64-root");
        layout.Register("install_location_arm64", "arm64-root");
        layout.Register("install_location_x64", "x64-root");

        var roots = layout.Read(processArch);

        roots.Should().Equal(new DotnetInstallRoot(layout.Root(expectedRoot), processArch));
    }

    [TestMethod]
    public void ArchLessFile_IsIgnoredWhenTheProcessArchitectureFileExists()
    {
        using var testEnv = new TestEnvironment();
        var layout = new RegistrationLayout(testEnv.TempRoot);
        layout.Register("install_location", "x64-root");
        layout.Register("install_location_arm64", "arm64-root");

        var roots = layout.Read(InstallArchitecture.arm64);

        roots.Should().Equal(new DotnetInstallRoot(layout.Root("arm64-root"), InstallArchitecture.arm64));
    }

    [TestMethod]
    public void ArchLessFile_IsUsedForTheProcessArchitectureWhenItsFileIsMissing()
    {
        using var testEnv = new TestEnvironment();
        var layout = new RegistrationLayout(testEnv.TempRoot);
        layout.Register("install_location", "native-root");
        layout.Register("install_location_x64", "x64-root");

        var roots = layout.Read(InstallArchitecture.arm64);

        roots.Should().Equal(new DotnetInstallRoot(layout.Root("native-root"), InstallArchitecture.arm64));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("missing-root")]
    public void ProcessArchitectureFileWithoutUsableRoot_DoesNotFallBackToTheArchLessFile(string registeredRoot)
    {
        using var testEnv = new TestEnvironment();
        var layout = new RegistrationLayout(testEnv.TempRoot);
        layout.Register("install_location", "x64-root");
        layout.WriteFile("install_location_arm64", registeredRoot.Length == 0 ? string.Empty : layout.Root(registeredRoot));

        layout.Read(InstallArchitecture.arm64).Should().BeEmpty();
    }

    [TestMethod]
    public void NoRegistration_AddsNoRoot()
    {
        using var testEnv = new TestEnvironment();

        new RegistrationLayout(testEnv.TempRoot).Read(InstallArchitecture.arm64).Should().BeEmpty();
    }

    private sealed class RegistrationLayout(string tempRoot)
    {
        private readonly string _configDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "etc-dotnet")).FullName;

        public string Root(string name) => Path.Combine(tempRoot, name);

        public void Register(string fileName, string rootName)
        {
            Directory.CreateDirectory(Root(rootName));
            WriteFile(fileName, Root(rootName));
        }

        public void WriteFile(string fileName, string content) =>
            File.WriteAllText(Path.Combine(_configDirectory, fileName), content + "\n");

        public List<DotnetInstallRoot> Read(InstallArchitecture processArch)
        {
            var roots = new List<DotnetInstallRoot>();
            DotnetEnvironmentManager.AddRegisteredMacOSRoot(roots, _configDirectory, processArch);
            return roots;
        }
    }
}
