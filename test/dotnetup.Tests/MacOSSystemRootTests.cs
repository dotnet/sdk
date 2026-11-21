// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests that the macOS system root is the installer's default folder for the process architecture, matching
/// get_default_installation_dir_for_arch in the host's pal.unix.cpp.
/// </summary>
[TestClass]
public class MacOSSystemRootTests
{
    [TestMethod]
    [DataRow(InstallArchitecture.arm64, InstallArchitecture.arm64)]
    [DataRow(InstallArchitecture.x64, InstallArchitecture.x64)]
    public void NativeProcess_UsesTheDefaultFolder(InstallArchitecture processArch, InstallArchitecture osArch)
    {
        using var testEnv = new TestEnvironment();
        string defaultDirectory = CreateDefaultDirectory(testEnv.TempRoot, withX64Subfolder: true);

        Read(defaultDirectory, processArch, osArch).Should().Equal(new DotnetInstallRoot(defaultDirectory, processArch));
    }

    [TestMethod]
    public void X64ProcessOnArm64_UsesTheX64Subfolder()
    {
        using var testEnv = new TestEnvironment();
        string defaultDirectory = CreateDefaultDirectory(testEnv.TempRoot, withX64Subfolder: true);

        Read(defaultDirectory, InstallArchitecture.x64, InstallArchitecture.arm64)
            .Should().Equal(new DotnetInstallRoot(Path.Combine(defaultDirectory, "x64"), InstallArchitecture.x64));
    }

    [TestMethod]
    public void X64ProcessOnArm64_DoesNotUseTheArm64FolderWhenTheX64SubfolderIsMissing()
    {
        using var testEnv = new TestEnvironment();
        string defaultDirectory = CreateDefaultDirectory(testEnv.TempRoot, withX64Subfolder: false);

        Read(defaultDirectory, InstallArchitecture.x64, InstallArchitecture.arm64).Should().BeEmpty();
    }

    [TestMethod]
    public void MissingDefaultFolder_AddsNoRoot()
    {
        using var testEnv = new TestEnvironment();

        Read(Path.Combine(testEnv.TempRoot, "missing"), InstallArchitecture.arm64, InstallArchitecture.arm64).Should().BeEmpty();
    }

    private static string CreateDefaultDirectory(string tempRoot, bool withX64Subfolder)
    {
        string defaultDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "dotnet")).FullName;
        if (withX64Subfolder)
        {
            Directory.CreateDirectory(Path.Combine(defaultDirectory, "x64"));
        }

        return defaultDirectory;
    }

    private static List<DotnetInstallRoot> Read(string defaultDirectory, InstallArchitecture processArch, InstallArchitecture osArch)
    {
        var roots = new List<DotnetInstallRoot>();
        DotnetEnvironmentManager.AddMacOSRoot(roots, defaultDirectory, processArch, osArch);
        return roots;
    }
}
