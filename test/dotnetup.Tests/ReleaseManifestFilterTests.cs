// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests that <see cref="ReleaseManifest.IsMatchingRuntimeComponent"/> correctly identifies
/// the right runtime component for each <see cref="InstallComponent"/> type, regardless of
/// enumeration ordering in <c>ProductRelease.Runtimes</c>.
/// </summary>
public class ReleaseManifestFilterTests
{
    // Known component display names from Microsoft.Deployment.DotNet.Releases:
    //   RuntimeReleaseComponent.Name  = ".NET Core Runtime"  (≤3.x) or ".NET Runtime" (≥5.0 if resource updated)
    //   AspNetCoreReleaseComponent.Name = "ASP.NET Core Runtime"
    //   WindowsDesktopReleaseComponent.Name = "Desktop Runtime"
    //   SdkReleaseComponent.Name = "SDK"

    private static readonly string[] AllRuntimeNames = new[]
    {
        ".NET Core Runtime",
        ".NET Runtime",
        "ASP.NET Core Runtime",
        "Desktop Runtime",
    };

    // ---- Runtime (default) component filter ----

    [Theory]
    [InlineData(".NET Core Runtime")]
    [InlineData(".NET Runtime")]
    public void Runtime_MatchesBaseRuntimeNames(string name)
    {
        Assert.True(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.Runtime));
    }

    [Theory]
    [InlineData("ASP.NET Core Runtime")]
    [InlineData("Desktop Runtime")]
    [InlineData("SDK")]
    public void Runtime_DoesNotMatchOtherComponents(string name)
    {
        Assert.False(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.Runtime));
    }

    // ---- ASPNETCore component filter ----

    [Fact]
    public void ASPNETCore_MatchesAspNetCoreRuntime()
    {
        Assert.True(ReleaseManifest.IsMatchingRuntimeComponent("ASP.NET Core Runtime", InstallComponent.ASPNETCore));
    }

    [Theory]
    [InlineData(".NET Core Runtime")]
    [InlineData(".NET Runtime")]
    [InlineData("Desktop Runtime")]
    [InlineData("SDK")]
    public void ASPNETCore_DoesNotMatchOtherComponents(string name)
    {
        Assert.False(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.ASPNETCore));
    }

    // ---- WindowsDesktop component filter ----

    [Fact]
    public void WindowsDesktop_MatchesDesktopRuntime()
    {
        Assert.True(ReleaseManifest.IsMatchingRuntimeComponent("Desktop Runtime", InstallComponent.WindowsDesktop));
    }

    [Theory]
    [InlineData(".NET Core Runtime")]
    [InlineData(".NET Runtime")]
    [InlineData("ASP.NET Core Runtime")]
    [InlineData("SDK")]
    public void WindowsDesktop_DoesNotMatchOtherComponents(string name)
    {
        Assert.False(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.WindowsDesktop));
    }

    // ---- Cross-filter exclusivity: each name matches exactly one component ----

    [Theory]
    [InlineData(".NET Core Runtime")]
    [InlineData(".NET Runtime")]
    [InlineData("ASP.NET Core Runtime")]
    [InlineData("Desktop Runtime")]
    public void EachRuntimeName_MatchesExactlyOneComponent(string name)
    {
        var runtimeComponents = new[]
        {
            InstallComponent.Runtime,
            InstallComponent.ASPNETCore,
            InstallComponent.WindowsDesktop,
        };

        int matchCount = runtimeComponents.Count(c => ReleaseManifest.IsMatchingRuntimeComponent(name, c));
        Assert.Equal(1, matchCount);
    }

    // ---- Ordering resilience: regardless of which runtime appears first,
    //      only the correct one is selected by the filter ----

    [Fact]
    public void RuntimeFilter_IsResilientToOrdering_WhenAspNetCoreAppearsFirst()
    {
        // Simulates the bug scenario: ASP.NET Core Runtime enumerated before .NET Core Runtime.
        // The old filter r.Name.Contains(".NET Core Runtime") would match both.
        var names = new[] { "ASP.NET Core Runtime", ".NET Core Runtime", "Desktop Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.Runtime)).ToList();

        Assert.Single(matched);
        Assert.Equal(".NET Core Runtime", matched[0]);
    }

    [Fact]
    public void RuntimeFilter_IsResilientToOrdering_WhenDesktopAppearsFirst()
    {
        var names = new[] { "Desktop Runtime", "ASP.NET Core Runtime", ".NET Core Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.Runtime)).ToList();

        Assert.Single(matched);
        Assert.Equal(".NET Core Runtime", matched[0]);
    }

    [Fact]
    public void AspNetFilter_IsResilientToOrdering_WhenBaseRuntimeAppearsFirst()
    {
        var names = new[] { ".NET Core Runtime", "ASP.NET Core Runtime", "Desktop Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.ASPNETCore)).ToList();

        Assert.Single(matched);
        Assert.Equal("ASP.NET Core Runtime", matched[0]);
    }

    [Fact]
    public void DesktopFilter_IsResilientToOrdering_WhenBaseRuntimeAppearsFirst()
    {
        var names = new[] { ".NET Core Runtime", "Desktop Runtime", "ASP.NET Core Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.WindowsDesktop)).ToList();

        Assert.Single(matched);
        Assert.Equal("Desktop Runtime", matched[0]);
    }

    // ---- Case insensitivity ----

    [Theory]
    [InlineData("asp.net core runtime", InstallComponent.ASPNETCore)]
    [InlineData("ASP.NET CORE RUNTIME", InstallComponent.ASPNETCore)]
    [InlineData(".net core runtime", InstallComponent.Runtime)]
    [InlineData(".NET RUNTIME", InstallComponent.Runtime)]
    [InlineData("desktop runtime", InstallComponent.WindowsDesktop)]
    [InlineData("DESKTOP RUNTIME", InstallComponent.WindowsDesktop)]
    public void Filters_AreCaseInsensitive(string name, InstallComponent component)
    {
        Assert.True(ReleaseManifest.IsMatchingRuntimeComponent(name, component));
    }

    // ---- IsCompositeArchive tests ----

    [Theory]
    [InlineData("aspnetcore-runtime-composite-linux-x64.tar.gz")]
    [InlineData("aspnetcore-runtime-composite-linux-arm64.tar.gz")]
    [InlineData("aspnetcore-runtime-composite-linux-musl-x64.tar.gz")]
    [InlineData("aspnetcore-runtime-composite-linux-musl-arm64.tar.gz")]
    [InlineData("aspnetcore-runtime-composite-linux-musl-arm.tar.gz")]
    [InlineData("ASPNETCORE-RUNTIME-COMPOSITE-LINUX-X64.TAR.GZ")]
    public void IsCompositeArchive_ReturnsTrueForCompositeFiles(string fileName)
    {
        Assert.True(ReleaseManifest.IsCompositeArchive(fileName));
    }

    [Theory]
    [InlineData("aspnetcore-runtime-linux-x64.tar.gz")]
    [InlineData("aspnetcore-runtime-linux-arm64.tar.gz")]
    [InlineData("dotnet-runtime-linux-x64.tar.gz")]
    [InlineData("dotnet-runtime-win-x64.zip")]
    [InlineData("windowsdesktop-runtime-win-x64.zip")]
    [InlineData("dotnet-sdk-linux-x64.tar.gz")]
    public void IsCompositeArchive_ReturnsFalseForRegularFiles(string fileName)
    {
        Assert.False(ReleaseManifest.IsCompositeArchive(fileName));
    }

    // ---- FindMatchingFile would select the right file when composites are present ----

    [Fact]
    public void CompositeFilter_SelectsRegularOverComposite_WhenBothShareRidAndExtension()
    {
        // Simulates the manifest scenario where ASP.NET Core has both regular and composite archives
        // with the same RID and extension. Only the non-composite should be selected.
        var fileNames = new[]
        {
            "aspnetcore-runtime-composite-linux-x64.tar.gz",
            "aspnetcore-runtime-linux-x64.tar.gz",
            "aspnetcore-runtime-composite-linux-arm64.tar.gz",
            "aspnetcore-runtime-linux-arm64.tar.gz",
        };

        var nonComposite = fileNames
            .Where(f => !ReleaseManifest.IsCompositeArchive(f))
            .Where(f => f.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, nonComposite.Count);
        Assert.All(nonComposite, f => Assert.DoesNotContain("composite", f));
    }

    // ---- IsApphostPackArchive tests ----

    [Theory]
    [InlineData("dotnet-apphost-pack-win-x64.zip")]
    [InlineData("dotnet-apphost-pack-linux-arm64.tar.gz")]
    [InlineData("dotnet-apphost-pack-linux-musl-x64.tar.gz")]
    [InlineData("DOTNET-APPHOST-PACK-WIN-X64.ZIP")]
    public void IsApphostPackArchive_ReturnsTrueForApphostPacks(string fileName)
    {
        Assert.True(ReleaseManifest.IsApphostPackArchive(fileName));
    }

    [Theory]
    [InlineData("dotnet-runtime-win-x64.zip")]
    [InlineData("dotnet-runtime-linux-x64.tar.gz")]
    [InlineData("aspnetcore-runtime-linux-x64.tar.gz")]
    [InlineData("windowsdesktop-runtime-win-x64.zip")]
    [InlineData("dotnet-sdk-linux-x64.tar.gz")]
    public void IsApphostPackArchive_ReturnsFalseForRegularFiles(string fileName)
    {
        Assert.False(ReleaseManifest.IsApphostPackArchive(fileName));
    }

    [Fact]
    public void ApphostPackFilter_SelectsRuntimeOverApphostPack_WhenBothShareRidAndExtension()
    {
        // The .NET Core Runtime component lists both dotnet-apphost-pack and dotnet-runtime files.
        // Only the runtime archive should be selected.
        var fileNames = new[]
        {
            "dotnet-apphost-pack-win-x64.zip",
            "dotnet-runtime-win-x64.zip",
            "dotnet-apphost-pack-linux-x64.tar.gz",
            "dotnet-runtime-linux-x64.tar.gz",
        };

        var filtered = fileNames
            .Where(f => !ReleaseManifest.IsCompositeArchive(f))
            .Where(f => !ReleaseManifest.IsApphostPackArchive(f))
            .ToList();

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, f => Assert.Contains("dotnet-runtime", f));
    }
}
