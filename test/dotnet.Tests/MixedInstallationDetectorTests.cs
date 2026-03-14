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

    [Fact]
    public void IsMixedInstallation_DoesNotThrow_WithValidInputs()
    {
        // This test verifies that the method doesn't throw exceptions
        // The actual result depends on whether a global install is registered on the system
        bool result = MixedInstallationDetector.IsMixedInstallation(
            "/some/path/dotnet",
            "/different/path");
        
        // Result can be true or false depending on system configuration
        // We just verify it doesn't throw
        Assert.True(result == true || result == false);
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
