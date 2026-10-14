// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;
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
    public void Validate_ReturnsTrue_WhenHostFxrReportsInstall()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var install = MakeInstall(testEnv.InstallPath);
        var validator = new HostFxrInstallationValidator(_ => [install with { Component = InstallComponent.Runtime }, install]);

        validator.Validate(install).Should().BeTrue();
        validator.Validate(install, out string? failure).Should().BeTrue();
        failure.Should().BeNull();
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostFxrDoesNotReportInstall()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var install = MakeInstall(testEnv.InstallPath);
        var validator = new HostFxrInstallationValidator(_ => [install with { Version = new ReleaseVersion("10.0.101") }]);

        validator.Validate(install, out string? failure).Should().BeFalse();
        failure.Should().Contain("10.0.101");
    }

    [TestMethod]
    public void Validate_ReturnsFalse_WhenHostFxrThrows()
    {
        using var testEnv = new TestEnvironment();
        StubHostFxr(testEnv.InstallPath);
        var validator = new HostFxrInstallationValidator(_ => throw new DllNotFoundException("hostfxr"));

        validator.Validate(MakeInstall(testEnv.InstallPath), out string? failure).Should().BeFalse();
        failure.Should().Contain("hostfxr");
    }

    private static DotnetInstall MakeInstall(string root) =>
        new(new DotnetInstallRoot(root, InstallArchitecture.x64), new ReleaseVersion("10.0.100"), InstallComponent.SDK);

    private static void StubHostFxr(string root)
    {
        string fxrDirectory = Path.Combine(root, "host", "fxr", "10.0.0");
        Directory.CreateDirectory(fxrDirectory);
        string libraryName = OperatingSystem.IsWindows()
            ? "hostfxr.dll"
            : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";
        File.WriteAllText(Path.Combine(fxrDirectory, libraryName), string.Empty);
    }
}

[TestClass]
public class MuxerInstallQueryTests
{
    private static readonly DotnetInstallRoot s_root = new(@"C:\PF\dotnet", InstallArchitecture.x64);

    [TestMethod]
    public void ParseSdks_ReadsVersions_AndSkipsMalformedLines()
    {
        const string output = "10.0.100 [C:\\Program Files\\dotnet\\sdk]\r\n11.0.100-rc.2.26504.105 [C:\\Program Files\\dotnet\\sdk]\r\nnot-a-version [x]\r\ngarbage\r\n";

        var installs = MuxerInstallQuery.ParseSdks(output, s_root).ToList();

        installs.Select(i => i.Version.ToString()).Should().Equal("10.0.100", "11.0.100-rc.2.26504.105");
        installs.Should().OnlyContain(i => i.Component == InstallComponent.SDK && i.InstallRoot == s_root);
    }

    [TestMethod]
    public void ParseRuntimes_MapsKnownFrameworks_AndSkipsUnknown()
    {
        const string output =
            "Microsoft.AspNetCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App]\n" +
            "Microsoft.NETCore.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\n" +
            "Microsoft.WindowsDesktop.App 10.0.0 [C:\\Program Files\\dotnet\\shared\\Microsoft.WindowsDesktop.App]\n" +
            "Some.Other.App 1.0.0 [C:\\x]\n";

        var installs = MuxerInstallQuery.ParseRuntimes(output, s_root).ToList();

        installs.Select(i => i.Component).Should().Equal(
            InstallComponent.ASPNETCore, InstallComponent.Runtime, InstallComponent.WindowsDesktop);
        installs.Should().OnlyContain(i => i.Version.ToString() == "10.0.0");
    }

    [TestMethod]
    public void GetInstalls_ReturnsEmpty_WhenMuxerIsMissing()
    {
        using var testEnv = new TestEnvironment();

        MuxerInstallQuery.GetInstalls(testEnv.InstallPath).Should().BeEmpty();
    }
}
