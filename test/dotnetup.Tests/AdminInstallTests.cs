// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminInstallRootResolverTests
{
    private const string ProgramFiles = @"C:\PF";
    private const string ProgramFilesX86 = @"C:\PFx86";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(Architecture.X64, InstallArchitecture.x64, @"C:\PF\dotnet")]
    [DataRow(Architecture.X64, InstallArchitecture.x86, @"C:\PFx86\dotnet")]
    [DataRow(Architecture.Arm64, InstallArchitecture.arm64, @"C:\PF\dotnet")]
    [DataRow(Architecture.Arm64, InstallArchitecture.x64, @"C:\PF\dotnet\x64")]
    [DataRow(Architecture.Arm64, InstallArchitecture.x86, @"C:\PFx86\dotnet")]
    [DataRow(Architecture.X86, InstallArchitecture.x86, @"C:\PF\dotnet")]
    public void Resolve_ReturnsSystemInstallRoot(Architecture osArchitecture, InstallArchitecture payload, string expected)
    {
        var resolver = new AdminInstallRootResolver(osArchitecture, ProgramFiles, ProgramFilesX86);

        var root = resolver.Resolve(payload);

        root.Path.Should().Be(expected);
        root.Architecture.Should().Be(payload);
    }

    [TestMethod]
    [DataRow(Architecture.X64, InstallArchitecture.arm64)]
    [DataRow(Architecture.X86, InstallArchitecture.x64)]
    [DataRow(Architecture.Arm, InstallArchitecture.x64)]
    public void Resolve_UnsupportedCombination_Throws(Architecture osArchitecture, InstallArchitecture payload)
    {
        var resolver = new AdminInstallRootResolver(osArchitecture, ProgramFiles, ProgramFilesX86);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => resolver.Resolve(payload));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.PlatformNotSupported);
    }

    [TestMethod]
    public void Resolve_MissingProgramFiles_Throws()
    {
        var resolver = new AdminInstallRootResolver(Architecture.X64, string.Empty, string.Empty);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => resolver.Resolve(InstallArchitecture.x64));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.PlatformNotSupported);
    }
}

[TestClass]
public class InstallationValidatorTests
{
    private const string Version = "10.0.100";

    [TestMethod]
    public void Validate_ReturnsFalse_WhenRootIsMissing()
    {
        var install = MakeInstall(Path.Combine(Path.GetTempPath(), "dotnetup-missing-" + Guid.NewGuid().ToString("N")));

        new InstallationValidator().Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain("Muxer not found");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenMuxerIsMissing()
    {
        using var testEnv = new TestEnvironment();
        WriteMarker(testEnv.InstallPath, InstallComponent.SDK, Version);

        new InstallationValidator().Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("Muxer not found");
    }

    [TestMethod]
    [DataRow(InstallComponent.SDK)]
    [DataRow(InstallComponent.Runtime)]
    [DataRow(InstallComponent.ASPNETCore)]
    [DataRow(InstallComponent.WindowsDesktop)]
    internal void Validate_ReturnsTrue_WhenMarkerFileExists(InstallComponent component)
    {
        using var testEnv = new TestEnvironment();
        WriteMuxer(testEnv.InstallPath);
        WriteMarker(testEnv.InstallPath, component, Version);

        new InstallationValidator().Validate(MakeInstall(testEnv.InstallPath) with { Component = component }, out string? failure).Should().BeTrue();
        failure.Should().BeNull();
    }

    [TestMethod]
    [DataRow(InstallComponent.SDK)]
    [DataRow(InstallComponent.Runtime)]
    [DataRow(InstallComponent.ASPNETCore)]
    [DataRow(InstallComponent.WindowsDesktop)]
    internal void Validate_ReturnsFalse_WhenVersionFolderLacksMarkerFile(InstallComponent component)
    {
        using var testEnv = new TestEnvironment();
        WriteMuxer(testEnv.InstallPath);
        string componentPath = InstallationValidator.GetComponentDirectory(testEnv.InstallPath, component, Version)!;
        Directory.CreateDirectory(componentPath);
        File.WriteAllText(Path.Combine(componentPath, "payload"), string.Empty);

        new InstallationValidator().Validate(MakeInstall(testEnv.InstallPath) with { Component = component }, out string? failure).Should().BeFalse();
        failure.Should().Contain("Component layout validation failed");
    }

    [TestMethod]
    [DataRow("wrong-version")]
    [DataRow("wrong-component")]
    [DataRow("wrong-root")]
    public void Validate_ReturnsFalse_WhenMarkerIsForAnotherInstall(string scenario)
    {
        using var testEnv = new TestEnvironment();
        WriteMuxer(testEnv.InstallPath);
        string root = scenario == "wrong-root" ? Path.Combine(testEnv.TempRoot, "other") : testEnv.InstallPath;
        string version = scenario == "wrong-version" ? "10.0.101" : Version;
        InstallComponent component = scenario == "wrong-component" ? InstallComponent.Runtime : InstallComponent.SDK;
        WriteMarker(root, component, version);

        new InstallationValidator().Validate(MakeInstall(testEnv.InstallPath)).Should().BeFalse();
    }

    [TestMethod]
    public void Validate_WindowsDesktopCanBeInstalledWithoutMuxer()
    {
        using var testEnv = new TestEnvironment();
        WriteMarker(testEnv.InstallPath, InstallComponent.WindowsDesktop, Version);

        new InstallationValidator().Validate(MakeInstall(testEnv.InstallPath) with { Component = InstallComponent.WindowsDesktop }).Should().BeTrue();
    }

    private static DotnetInstall MakeInstall(string root) =>
        new(new DotnetInstallRoot(root, InstallArchitecture.x64), new ReleaseVersion(Version), InstallComponent.SDK);

    private static void WriteMuxer(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(InstallationValidator.GetMuxerPath(root), string.Empty);
    }

    private static void WriteMarker(string root, InstallComponent component, string version)
    {
        string componentPath = InstallationValidator.GetComponentDirectory(root, component, version)!;
        Directory.CreateDirectory(componentPath);
        string marker = component switch
        {
            InstallComponent.SDK => "dotnet.dll",
            InstallComponent.Runtime => InstallComponentExtensions.RuntimeFrameworkName + ".deps.json",
            InstallComponent.ASPNETCore => InstallComponentExtensions.AspNetCoreFrameworkName + ".deps.json",
            _ => InstallComponentExtensions.WindowsDesktopFrameworkName + ".deps.json",
        };
        File.WriteAllText(Path.Combine(componentPath, marker), string.Empty);
    }
}