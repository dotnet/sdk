// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tests;

public class MixedInstallationDetectorTests : SdkTest
{
    public MixedInstallationDetectorTests(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void IsMixedInstallation_ReturnsFalse_WhenMuxerPathIsNull()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation(null!, "/some/path");
        Assert.False(result);
    }

    [Fact]
    public void IsMixedInstallation_ReturnsFalse_WhenMuxerPathIsEmpty()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation("", "/some/path");
        Assert.False(result);
    }

    [Fact]
    public void IsMixedInstallation_ReturnsFalse_WhenDotnetRootIsNull()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation("/usr/share/dotnet/dotnet", null);
        Assert.False(result);
    }

    [Fact]
    public void IsMixedInstallation_ReturnsFalse_WhenDotnetRootIsEmpty()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation("/usr/share/dotnet/dotnet", "");
        Assert.False(result);
    }

    [LinuxOnlyFact]
    public void IsMixedInstallation_ReturnsFalse_WhenMuxerAndDotnetRootAreInSameGlobalLocation()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation(
            "/usr/share/dotnet/dotnet",
            "/usr/share/dotnet");
        Assert.False(result);
    }

    [LinuxOnlyFact]
    public void IsMixedInstallation_ReturnsTrue_WhenMuxerIsGlobalAndDotnetRootIsDifferent()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation(
            "/usr/share/dotnet/dotnet",
            "/home/user/.dotnet");
        Assert.True(result);
    }

    [LinuxOnlyFact]
    public void IsMixedInstallation_ReturnsFalse_WhenMuxerIsNotInGlobalLocation()
    {
        bool result = MixedInstallationDetector.IsMixedInstallation(
            "/home/user/.dotnet/dotnet",
            "/usr/share/dotnet");
        Assert.False(result);
    }

    [WindowsOnlyFact]
    public void IsMixedInstallation_ReturnsTrue_OnWindows_WhenMuxerIsGlobalAndDotnetRootIsDifferent()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        bool result = MixedInstallationDetector.IsMixedInstallation(
            Path.Combine(programFiles, "dotnet", "dotnet.exe"),
            @"C:\Users\user\.dotnet");
        Assert.True(result);
    }

    [Fact]
    public void GetDocumentationUrl_ReturnsLinuxUrl_OnLinux()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string? url = MixedInstallationDetector.GetDocumentationUrl();
            Assert.Equal("https://learn.microsoft.com/en-us/dotnet/core/install/linux-package-mixup", url);
        }
    }

    [Fact]
    public void GetDocumentationUrl_ReturnsNull_OnNonLinux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string? url = MixedInstallationDetector.GetDocumentationUrl();
            Assert.Null(url);
        }
    }
}
