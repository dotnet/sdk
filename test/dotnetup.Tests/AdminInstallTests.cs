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
public class HostFxrInstallationValidatorTests
{
    [TestMethod]
    public void Validate_ReturnsFalse_WhenRootIsMissing()
    {
        var validator = new HostFxrInstallationValidator(_ => throw new InvalidOperationException("must not be called"));
        var install = MakeInstall(Path.Combine(Path.GetTempPath(), "dotnetup-missing-" + Guid.NewGuid().ToString("N")));

        validator.Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain("does not exist");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostFxrIsMissing()
    {
        using var testEnv = new TestEnvironment();
        var validator = new HostFxrInstallationValidator(_ => throw new InvalidOperationException("must not be called"));

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
        var validator = new HostFxrInstallationValidator(_ => throw new InvalidOperationException("must not be called"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("hostfxr");
    }

    [TestMethod]
    public void Validate_ReturnsTrue_WhenHostReportsInstall()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var install = MakeInstall(testEnv.InstallPath);
        var validator = new HostFxrInstallationValidator(_ => HostReport(
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
        var validator = new HostFxrInstallationValidator(_ => HostReport(
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
        var validator = new HostFxrInstallationValidator(_ => HostReport(sdks: [new NetSdkInfo("10.0.100", otherSdk)]));

        validator.Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain(otherSdk);
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostQueryThrows()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var validator = new HostFxrInstallationValidator(_ => throw new DllNotFoundException("hostfxr"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("hostfxr");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenMuxerIsMissing()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);

        new HostFxrInstallationValidator().Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("did not report");
    }

    [TestMethod]
    public void ParseMuxerListOutput_ReadsSdksAndRuntimes_AndSkipsMalformedLines()
    {
        const string sdkOutput =
            "10.0.100 [C:\\Program Files\\dotnet\\sdk]\r\n" +
            "11.0.100-rc.2.26504.105 [C:\\Program Files\\dotnet\\sdk]\r\n" +
            "not-a-version [x]\r\n" +
            "10.0.200\r\n" +
            "garbage\r\n";
        const string runtimeOutput =
            "Microsoft.AspNetCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App]\n" +
            "Microsoft.NETCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\n" +
            "Microsoft.NETCore.App not-a-version [C:\\x]\n" +
            "Microsoft.NETCore.App 10.0.1\n";

        var environment = HostFxrInstallationValidator.ParseMuxerListOutput(sdkOutput, runtimeOutput);

        environment.SdkInfo.Select(s => (s.Version.ToString(), s.Path)).Should().Equal(
            ("10.0.100", Path.Combine("C:\\Program Files\\dotnet\\sdk", "10.0.100")),
            ("11.0.100-rc.2.26504.105", Path.Combine("C:\\Program Files\\dotnet\\sdk", "11.0.100-rc.2.26504.105")));
        environment.RuntimeInfo.Select(r => (r.Name, r.Version.ToString(), r.Path)).Should().Equal(
            ("Microsoft.AspNetCore.App", "10.0.0", "C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App"),
            ("Microsoft.NETCore.App", "10.0.0", "C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App"));
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
    }
}