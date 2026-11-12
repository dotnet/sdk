// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests that the Windows system root for one architecture is found the way the host finds it: that architecture's
/// registration first, then the Program Files folder the installer uses for that architecture.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
[OSCondition(OperatingSystems.Windows)]
public class WindowsProgramFilesDotnetRootTests
{
    private static readonly string s_existingDirectory = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
    private static readonly string s_otherExistingDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
    private static readonly string s_missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private static readonly Dictionary<string, string> s_none = [];
    private static readonly AdminInstallRootResolver s_noFallback = new(Architecture.X64, string.Empty, string.Empty);

    [TestMethod]
    [DataRow(InstallArchitecture.x64, true)]
    [DataRow(InstallArchitecture.x86, false)]
    public void UsesOnlyTheRequestedArchitecturesRegistration(InstallArchitecture architecture, bool expectFirstDirectory)
    {
        var sharedHostPaths = new Dictionary<string, string> { ["x64"] = s_existingDirectory };
        var installLocations = new Dictionary<string, string> { ["x86"] = s_otherExistingDirectory };

        WindowsPathHelper.GetProgramFilesDotnetRoot(architecture, sharedHostPaths, installLocations, s_noFallback)
            .Should().Be(new DotnetInstallRoot(expectFirstDirectory ? s_existingDirectory : s_otherExistingDirectory, architecture));
    }

    [TestMethod]
    public void PrefersSharedHostPathAndFallsBackToInstallLocationWhenItIsStale()
    {
        WindowsPathHelper.GetProgramFilesDotnetRoot(
                InstallArchitecture.x64,
                sharedHostPaths: new Dictionary<string, string> { ["x64"] = s_existingDirectory },
                installLocations: new Dictionary<string, string> { ["x64"] = s_otherExistingDirectory },
                s_noFallback)
            .Should().Be(new DotnetInstallRoot(s_existingDirectory, InstallArchitecture.x64));

        WindowsPathHelper.GetProgramFilesDotnetRoot(
                InstallArchitecture.x64,
                sharedHostPaths: new Dictionary<string, string> { ["x64"] = s_missingDirectory },
                installLocations: new Dictionary<string, string> { ["x64"] = s_otherExistingDirectory + Path.DirectorySeparatorChar },
                s_noFallback)
            .Should().Be(new DotnetInstallRoot(s_otherExistingDirectory, InstallArchitecture.x64));
    }

    [TestMethod]
    public void MatchesTheRegistryArchitectureNameCaseInsensitively()
    {
        // ReadArchitectureValues returns case-insensitive dictionaries.
        var installLocations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ARM64"] = s_existingDirectory };

        WindowsPathHelper.GetProgramFilesDotnetRoot(InstallArchitecture.arm64, s_none, installLocations, s_noFallback)
            .Should().Be(new DotnetInstallRoot(s_existingDirectory, InstallArchitecture.arm64));
    }

    [TestMethod]
    [DataRow("x86")]
    [DataRow("SDKFeatures")]
    public void UsesTheFallbackWhenOnlyOtherKeysAreRegistered(string otherKey)
    {
        using var testEnv = new TestEnvironment();
        var resolver = CreateResolverWithEveryFallbackRoot(testEnv.TempRoot, Architecture.X64);
        var installLocations = new Dictionary<string, string> { [otherKey] = s_existingDirectory };

        WindowsPathHelper.GetProgramFilesDotnetRoot(InstallArchitecture.x64, s_none, installLocations, resolver)
            .Should().Be(new DotnetInstallRoot(Path.Combine(testEnv.TempRoot, "Program Files", "dotnet"), InstallArchitecture.x64));
    }

    [TestMethod]
    public void UsesTheFallbackWhenTheRegisteredDirectoryIsMissing()
    {
        using var testEnv = new TestEnvironment();
        var resolver = CreateResolverWithEveryFallbackRoot(testEnv.TempRoot, Architecture.X64);
        var installLocations = new Dictionary<string, string> { ["x64"] = s_missingDirectory };

        WindowsPathHelper.GetProgramFilesDotnetRoot(InstallArchitecture.x64, s_none, installLocations, resolver)
            .Should().Be(new DotnetInstallRoot(Path.Combine(testEnv.TempRoot, "Program Files", "dotnet"), InstallArchitecture.x64));
    }

    [TestMethod]
    [DataRow(Architecture.X64, InstallArchitecture.x64, @"Program Files\dotnet")]
    [DataRow(Architecture.X64, InstallArchitecture.x86, @"Program Files (x86)\dotnet")]
    [DataRow(Architecture.Arm64, InstallArchitecture.arm64, @"Program Files\dotnet")]
    [DataRow(Architecture.Arm64, InstallArchitecture.x64, @"Program Files\dotnet\x64")]
    [DataRow(Architecture.Arm64, InstallArchitecture.x86, @"Program Files (x86)\dotnet")]
    [DataRow(Architecture.X86, InstallArchitecture.x86, @"Program Files\dotnet")]
    public void FallsBackToTheInstallerFolderForTheArchitecture(Architecture osArchitecture, InstallArchitecture architecture, string expectedRoot)
    {
        using var testEnv = new TestEnvironment();
        var resolver = CreateResolverWithEveryFallbackRoot(testEnv.TempRoot, osArchitecture);

        WindowsPathHelper.GetProgramFilesDotnetRoot(architecture, s_none, s_none, resolver)
            .Should().Be(new DotnetInstallRoot(Path.Combine(testEnv.TempRoot, expectedRoot), architecture));
    }

    [TestMethod]
    public void ReturnsNullWhenTheFallbackFolderIsMissing()
    {
        using var testEnv = new TestEnvironment();
        string programFiles = Directory.CreateDirectory(Path.Combine(testEnv.TempRoot, "Program Files")).FullName;

        // Only the native folder exists, so x64 on Arm64 Windows must not use it.
        Directory.CreateDirectory(Path.Combine(programFiles, "dotnet"));
        var resolver = new AdminInstallRootResolver(Architecture.Arm64, programFiles, Path.Combine(testEnv.TempRoot, "Program Files (x86)"));

        WindowsPathHelper.GetProgramFilesDotnetRoot(InstallArchitecture.x64, s_none, s_none, resolver).Should().BeNull();
    }

    [TestMethod]
    [DataRow(Architecture.X64, InstallArchitecture.arm64, "Program Files", "Program Files (x86)")]
    [DataRow(Architecture.X64, InstallArchitecture.x64, "", "")]
    public void ReturnsNullForUnsupportedFallbacks(
        Architecture osArchitecture, InstallArchitecture architecture, string programFiles, string programFilesX86)
    {
        var resolver = new AdminInstallRootResolver(osArchitecture, programFiles, programFilesX86);

        WindowsPathHelper.GetProgramFilesDotnetRoot(architecture, s_none, s_none, resolver).Should().BeNull();
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

    private static AdminInstallRootResolver CreateResolverWithEveryFallbackRoot(string tempRoot, Architecture osArchitecture)
    {
        string programFiles = Path.Combine(tempRoot, "Program Files");
        string programFilesX86 = Path.Combine(tempRoot, "Program Files (x86)");
        Directory.CreateDirectory(Path.Combine(programFiles, "dotnet", "x64"));
        Directory.CreateDirectory(Path.Combine(programFilesX86, "dotnet"));
        return new AdminInstallRootResolver(osArchitecture, programFiles, programFilesX86);
    }
}
