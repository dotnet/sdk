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
    [TestMethod]
    public void Validate_ReturnsFalse_WhenRootIsMissing()
    {
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => throw new InvalidOperationException("must not be called"));
        var install = MakeInstall(Path.Combine(Path.GetTempPath(), "dotnetup-missing-" + Guid.NewGuid().ToString("N")));

        validator.Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain("does not exist");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostFxrIsMissing()
    {
        using var testEnv = new TestEnvironment();
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => throw new InvalidOperationException("must not be called"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("hostfxr");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("10.0.0/nested")]
    public void Validate_ReturnsFalse_WhenHostFxrIsNotDirectlyInAVersionDirectory(string relativeDirectory)
    {
        using var testEnv = new TestEnvironment();
        string directory = Path.Combine(testEnv.InstallPath, "host", "fxr", relativeDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, HostFxrLibraryName), string.Empty);
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => throw new InvalidOperationException("must not be called"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("hostfxr");
    }

    [TestMethod]
    public void Validate_ReturnsTrue_WhenHostReportsInstall()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var install = MakeInstall(testEnv.InstallPath);
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => HostReport(
            sdks: [new NetSdkInfo("10.0.100", Path.Combine(testEnv.InstallPath, "sdk", "10.0.100"))],
            runtimes: [new NetRuntimeInfo(InstallComponentExtensions.RuntimeFrameworkName, "10.0.100", Path.Combine(testEnv.InstallPath, "shared", InstallComponentExtensions.RuntimeFrameworkName))]));

        validator.Validate(install).Should().BeTrue();
        validator.Validate(install, out string? failure).Should().BeTrue();
        failure.Should().BeNull();
        validator.Validate(install with { Component = InstallComponent.Runtime }).Should().BeTrue();
        validator.Validate(install with { Component = InstallComponent.ASPNETCore }).Should().BeFalse();
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostDoesNotReportVersion()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var install = MakeInstall(testEnv.InstallPath);
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => HostReport(
            sdks: [new NetSdkInfo("10.0.101", Path.Combine(testEnv.InstallPath, "sdk", "10.0.101"))]));

        validator.Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain("10.0.101");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostReportsVersionFromAnotherRoot()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var install = MakeInstall(testEnv.InstallPath);
        string otherSdk = Path.Combine(testEnv.TempRoot, "other", "sdk", "10.0.100");
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => HostReport(sdks: [new NetSdkInfo("10.0.100", otherSdk)]));

        validator.Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain(otherSdk);
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostQueryThrows()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => throw new DllNotFoundException("hostfxr"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("hostfxr");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenMuxerIsMissing()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        File.Delete(InstallationValidator.GetMuxerPath(testEnv.InstallPath));

        new InstallationValidator(InstallMechanism.WindowsInstallerBundle).Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("Muxer not found");
    }

    [TestMethod]
    public void Validate_ArchiveRequiresComponentLayout()
    {
        using var testEnv = new TestEnvironment();
        File.WriteAllText(InstallationValidator.GetMuxerPath(testEnv.InstallPath), string.Empty);
        var validator = new InstallationValidator(InstallMechanism.Archive, _ => throw new InvalidOperationException("must not be called"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("Component layout validation failed");
    }

    [TestMethod]
    public void Validate_ArchivePreservesPlatformSpecificHostCheck()
    {
        using var testEnv = new TestEnvironment();
        File.WriteAllText(InstallationValidator.GetMuxerPath(testEnv.InstallPath), string.Empty);
        string sdkPath = Path.Combine(testEnv.InstallPath, "sdk", "10.0.100");
        Directory.CreateDirectory(sdkPath);
        File.WriteAllText(Path.Combine(sdkPath, "dotnet.dll"), string.Empty);
        int queryCalls = 0;
        var validator = new InstallationValidator(InstallMechanism.Archive, _ =>
        {
            queryCalls++;
            return HostReport(sdks: [new NetSdkInfo("10.0.100", sdkPath)]);
        });

        validator.Validate(MakeInstall(testEnv.InstallPath)).Should().BeTrue();
        queryCalls.Should().Be(OperatingSystem.IsWindows() ? 1 : 0);
    }

    [TestMethod]
    public void Validate_ArchiveWindowsDesktopCanBeInstalledWithoutMuxer()
    {
        using var testEnv = new TestEnvironment();
        string runtimePath = InstallationValidator.GetComponentDirectory(testEnv.InstallPath, InstallComponent.WindowsDesktop, "10.0.100")!;
        Directory.CreateDirectory(runtimePath);
        File.WriteAllText(Path.Combine(runtimePath, "WindowsBase.dll"), string.Empty);
        var validator = new InstallationValidator(InstallMechanism.Archive, _ => throw new InvalidOperationException("must not be called"));

        validator.Validate(MakeInstall(testEnv.InstallPath) with { Component = InstallComponent.WindowsDesktop }).Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(InstallMechanism.Archive, InstallComponent.SDK)]
    [DataRow(InstallMechanism.Archive, InstallComponent.Runtime)]
    [DataRow(InstallMechanism.Archive, InstallComponent.ASPNETCore)]
    [DataRow(InstallMechanism.Archive, InstallComponent.WindowsDesktop)]
    [DataRow(InstallMechanism.WindowsInstallerBundle, InstallComponent.SDK)]
    [DataRow(InstallMechanism.WindowsInstallerBundle, InstallComponent.Runtime)]
    [DataRow(InstallMechanism.WindowsInstallerBundle, InstallComponent.ASPNETCore)]
    [DataRow(InstallMechanism.WindowsInstallerBundle, InstallComponent.WindowsDesktop)]
    public void Validate_UnavailableEnvironmentInfoRequiresInstalledComponent(InstallMechanism mechanism, InstallComponent component)
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        string componentPath = InstallationValidator.GetComponentDirectory(testEnv.InstallPath, component, "10.0.100")!;
        Directory.CreateDirectory(componentPath);
        File.WriteAllText(Path.Combine(componentPath, "payload"), string.Empty);
        var validator = new InstallationValidator(mechanism, _ => throw new HostFxrEnvironmentInfoUnavailableException());

        validator.Validate(MakeInstall(testEnv.InstallPath) with { Component = component }, out string? failure).Should().BeTrue();
        failure.Should().BeNull();
    }

    [TestMethod]
    [DataRow("missing-component")]
    [DataRow("empty-component")]
    [DataRow("wrong-version")]
    [DataRow("wrong-component")]
    [DataRow("wrong-root")]
    [DataRow("missing-muxer")]
    [DataRow("missing-hostfxr")]
    public void Validate_UnavailableEnvironmentInfoDoesNotBypassMissingEvidence(string scenario)
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        if (scenario != "missing-component")
        {
            string root = scenario == "wrong-root" ? Path.Combine(testEnv.TempRoot, "other") : testEnv.InstallPath;
            string version = scenario == "wrong-version" ? "10.0.101" : "10.0.100";
            InstallComponent component = scenario == "wrong-component" ? InstallComponent.Runtime : InstallComponent.SDK;
            string componentPath = InstallationValidator.GetComponentDirectory(root, component, version)!;
            Directory.CreateDirectory(componentPath);
            if (scenario != "empty-component")
            {
                File.WriteAllText(Path.Combine(componentPath, "payload"), string.Empty);
            }
        }
        if (scenario == "missing-muxer")
        {
            File.Delete(InstallationValidator.GetMuxerPath(testEnv.InstallPath));
        }
        if (scenario == "missing-hostfxr")
        {
            Directory.Delete(Path.Combine(testEnv.InstallPath, "host"), recursive: true);
        }
        var validator = new InstallationValidator(InstallMechanism.WindowsInstallerBundle, _ => throw new HostFxrEnvironmentInfoUnavailableException());

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(InstallMechanism.Archive, "dll")]
    [DataRow(InstallMechanism.Archive, "export")]
    [DataRow(InstallMechanism.Archive, "query")]
    [DataRow(InstallMechanism.WindowsInstallerBundle, "dll")]
    [DataRow(InstallMechanism.WindowsInstallerBundle, "export")]
    [DataRow(InstallMechanism.WindowsInstallerBundle, "query")]
    public void Validate_OtherHostQueryFailuresDoNotUseLayoutFallback(InstallMechanism mechanism, string failureType)
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        string sdkPath = InstallationValidator.GetComponentDirectory(testEnv.InstallPath, InstallComponent.SDK, "10.0.100")!;
        Directory.CreateDirectory(sdkPath);
        File.WriteAllText(Path.Combine(sdkPath, "dotnet.dll"), string.Empty);
        Exception exception = failureType switch
        {
            "dll" => new DllNotFoundException("hostfxr"),
            "export" => new EntryPointNotFoundException("another hostfxr export"),
            _ => new InvalidOperationException("hostfxr query failed"),
        };
        var validator = new InstallationValidator(mechanism, _ => throw exception);

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain(exception.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(InstallMechanism.Archive)]
    [DataRow(InstallMechanism.WindowsInstallerBundle)]
    public void Validate_AvailableEnvironmentInfoStillRequiresHostReport(InstallMechanism mechanism)
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        string sdkPath = InstallationValidator.GetComponentDirectory(testEnv.InstallPath, InstallComponent.SDK, "10.0.100")!;
        Directory.CreateDirectory(sdkPath);
        File.WriteAllText(Path.Combine(sdkPath, "dotnet.dll"), string.Empty);
        var validator = new InstallationValidator(mechanism, _ => HostReport());

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("did not report SDK");
    }

    private static string HostFxrLibraryName => OperatingSystem.IsWindows()
        ? "hostfxr.dll"
        : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";

    private static NetEnvironmentInfo HostReport(NetSdkInfo[]? sdks = null, NetRuntimeInfo[]? runtimes = null) =>
        new(runtimes ?? [], sdks ?? []);

    private static DotnetInstall MakeInstall(string root) =>
        new(new DotnetInstallRoot(root, InstallArchitecture.x64), new ReleaseVersion("10.0.100"), InstallComponent.SDK);

    private static void StubHostFxr(string root) => StubHostFxr(root, "10.0.0");

    /// <summary>Creates an empty hostfxr library under <c>host/fxr</c> joined with <paramref name="subdirectories"/>.</summary>
    private static void StubHostFxr(string root, params string[] subdirectories)
    {
        string fxrDirectory = Path.Combine(root, "host", "fxr", Path.Combine(subdirectories));
        Directory.CreateDirectory(fxrDirectory);
        File.WriteAllText(Path.Combine(fxrDirectory, HostFxrLibraryName), string.Empty);
        File.WriteAllText(InstallationValidator.GetMuxerPath(root), string.Empty);
    }
}