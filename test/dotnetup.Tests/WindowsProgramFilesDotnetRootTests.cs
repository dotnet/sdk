// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests that the Windows system roots are the Program Files folders the installer and host use by default for each
/// architecture. The registry is not read, so installs in a custom location are not found.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
[OSCondition(OperatingSystems.Windows)]
public class WindowsProgramFilesDotnetRootTests
{
    [TestMethod]
    [DataRow(Architecture.X64, InstallArchitecture.x64, @"Program Files\dotnet")]
    [DataRow(Architecture.X64, InstallArchitecture.x86, @"Program Files (x86)\dotnet")]
    [DataRow(Architecture.Arm64, InstallArchitecture.arm64, @"Program Files\dotnet")]
    [DataRow(Architecture.Arm64, InstallArchitecture.x64, @"Program Files\dotnet\x64")]
    [DataRow(Architecture.Arm64, InstallArchitecture.x86, @"Program Files (x86)\dotnet")]
    [DataRow(Architecture.X86, InstallArchitecture.x86, @"Program Files\dotnet")]
    public void UsesTheInstallerFolderForTheArchitecture(Architecture osArchitecture, InstallArchitecture architecture, string expectedRoot)
    {
        using var testEnv = new TestEnvironment();
        var resolver = CreateResolverWithEveryRoot(testEnv.TempRoot, osArchitecture);

        WindowsPathHelper.GetProgramFilesDotnetRoot(resolver, architecture)
            .Should().Be(new DotnetInstallRoot(Path.Combine(testEnv.TempRoot, expectedRoot), architecture));
    }

    [TestMethod]
    public void ReturnsNullWhenTheFolderIsMissing()
    {
        using var testEnv = new TestEnvironment();
        string programFiles = Directory.CreateDirectory(Path.Combine(testEnv.TempRoot, "Program Files")).FullName;

        // Only the native folder exists, so x64 on Arm64 Windows must not use it.
        Directory.CreateDirectory(Path.Combine(programFiles, "dotnet"));
        var resolver = new AdminInstallRootResolver(Architecture.Arm64, programFiles, Path.Combine(testEnv.TempRoot, "Program Files (x86)"));

        WindowsPathHelper.GetProgramFilesDotnetRoot(resolver, InstallArchitecture.x64).Should().BeNull();
    }

    [TestMethod]
    [DataRow(Architecture.X64, InstallArchitecture.arm64, "Program Files", "Program Files (x86)")]
    [DataRow(Architecture.X64, InstallArchitecture.x64, "", "")]
    public void ReturnsNullForUnsupportedRoots(
        Architecture osArchitecture, InstallArchitecture architecture, string programFiles, string programFilesX86)
    {
        var resolver = new AdminInstallRootResolver(osArchitecture, programFiles, programFilesX86);

        WindowsPathHelper.GetProgramFilesDotnetRoot(resolver, architecture).Should().BeNull();
    }

    [TestMethod]
    public void GetProgramFilesDotnetPaths_ReturnsExistingRootsNativeFirst()
    {
        using var testEnv = new TestEnvironment();
        var resolver = CreateResolverWithEveryRoot(testEnv.TempRoot, Architecture.Arm64);

        WindowsPathHelper.GetProgramFilesDotnetPaths(resolver).Should().Equal(
            Path.Combine(testEnv.TempRoot, "Program Files", "dotnet"),
            Path.Combine(testEnv.TempRoot, "Program Files (x86)", "dotnet"),
            Path.Combine(testEnv.TempRoot, "Program Files", "dotnet", "x64"));
    }

    [TestMethod]
    public void GetProgramFilesDotnetPaths_SkipsMissingRoots()
    {
        using var testEnv = new TestEnvironment();
        string programFiles = Path.Combine(testEnv.TempRoot, "Program Files");
        Directory.CreateDirectory(Path.Combine(programFiles, "dotnet"));
        var resolver = new AdminInstallRootResolver(Architecture.X64, programFiles, Path.Combine(testEnv.TempRoot, "Program Files (x86)"));

        WindowsPathHelper.GetProgramFilesDotnetPaths(resolver).Should().Equal(Path.Combine(programFiles, "dotnet"));
    }

    [TestMethod]
    public void GetProgramFilesDotnetRoot_ForTheProcessArchitecture_IsAnExistingRootOfThatArchitecture()
    {
        InstallArchitecture architecture = InstallerUtilities.GetDefaultInstallArchitecture();

        if (WindowsPathHelper.GetProgramFilesDotnetRoot(architecture) is { } root)
        {
            root.Architecture.Should().Be(architecture);
            Directory.Exists(root.Path).Should().BeTrue();
        }
    }

    private static AdminInstallRootResolver CreateResolverWithEveryRoot(string tempRoot, Architecture osArchitecture)
    {
        string programFiles = Path.Combine(tempRoot, "Program Files");
        string programFilesX86 = Path.Combine(tempRoot, "Program Files (x86)");
        Directory.CreateDirectory(Path.Combine(programFiles, "dotnet", "x64"));
        Directory.CreateDirectory(Path.Combine(programFilesX86, "dotnet"));
        return new AdminInstallRootResolver(osArchitecture, programFiles, programFilesX86);
    }
}
