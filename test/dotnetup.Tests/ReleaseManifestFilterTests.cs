// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using FluentAssertions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests that <see cref="ReleaseManifest.IsMatchingRuntimeComponent"/> correctly identifies
/// the right runtime component for each <see cref="InstallComponent"/> type, regardless of
/// enumeration ordering in <c>ProductRelease.Runtimes</c>.
/// </summary>
[TestClass]
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

    [TestMethod]
    [DataRow(".NET Core Runtime")]
    [DataRow(".NET Runtime")]
    public void Runtime_MatchesBaseRuntimeNames(string name)
    {
        Assert.IsTrue(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.Runtime));
    }

    [TestMethod]
    [DataRow("ASP.NET Core Runtime")]
    [DataRow("Desktop Runtime")]
    [DataRow("SDK")]
    public void Runtime_DoesNotMatchOtherComponents(string name)
    {
        Assert.IsFalse(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.Runtime));
    }

    // ---- ASPNETCore component filter ----

    [TestMethod]
    public void ASPNETCore_MatchesAspNetCoreRuntime()
    {
        Assert.IsTrue(ReleaseManifest.IsMatchingRuntimeComponent("ASP.NET Core Runtime", InstallComponent.ASPNETCore));
    }

    [TestMethod]
    [DataRow(".NET Core Runtime")]
    [DataRow(".NET Runtime")]
    [DataRow("Desktop Runtime")]
    [DataRow("SDK")]
    public void ASPNETCore_DoesNotMatchOtherComponents(string name)
    {
        Assert.IsFalse(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.ASPNETCore));
    }

    // ---- WindowsDesktop component filter ----

    [TestMethod]
    public void WindowsDesktop_MatchesDesktopRuntime()
    {
        Assert.IsTrue(ReleaseManifest.IsMatchingRuntimeComponent("Desktop Runtime", InstallComponent.WindowsDesktop));
    }

    [TestMethod]
    [DataRow(".NET Core Runtime")]
    [DataRow(".NET Runtime")]
    [DataRow("ASP.NET Core Runtime")]
    [DataRow("SDK")]
    public void WindowsDesktop_DoesNotMatchOtherComponents(string name)
    {
        Assert.IsFalse(ReleaseManifest.IsMatchingRuntimeComponent(name, InstallComponent.WindowsDesktop));
    }

    // ---- Cross-filter exclusivity: each name matches exactly one component ----

    [TestMethod]
    [DataRow(".NET Core Runtime")]
    [DataRow(".NET Runtime")]
    [DataRow("ASP.NET Core Runtime")]
    [DataRow("Desktop Runtime")]
    public void EachRuntimeName_MatchesExactlyOneComponent(string name)
    {
        var runtimeComponents = new[]
        {
            InstallComponent.Runtime,
            InstallComponent.ASPNETCore,
            InstallComponent.WindowsDesktop,
        };

        int matchCount = runtimeComponents.Count(c => ReleaseManifest.IsMatchingRuntimeComponent(name, c));
        Assert.AreEqual(1, matchCount);
    }

    // ---- Ordering resilience: regardless of which runtime appears first,
    //      only the correct one is selected by the filter ----

    [TestMethod]
    public void RuntimeFilter_IsResilientToOrdering_WhenAspNetCoreAppearsFirst()
    {
        // Simulates the bug scenario: ASP.NET Core Runtime enumerated before .NET Core Runtime.
        // The old filter r.Name.Contains(".NET Core Runtime") would match both.
        var names = new[] { "ASP.NET Core Runtime", ".NET Core Runtime", "Desktop Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.Runtime)).ToList();

        Assert.ContainsSingle(matched);
        Assert.AreEqual(".NET Core Runtime", matched[0]);
    }

    [TestMethod]
    public void RuntimeFilter_IsResilientToOrdering_WhenDesktopAppearsFirst()
    {
        var names = new[] { "Desktop Runtime", "ASP.NET Core Runtime", ".NET Core Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.Runtime)).ToList();

        Assert.ContainsSingle(matched);
        Assert.AreEqual(".NET Core Runtime", matched[0]);
    }

    [TestMethod]
    public void AspNetFilter_IsResilientToOrdering_WhenBaseRuntimeAppearsFirst()
    {
        var names = new[] { ".NET Core Runtime", "ASP.NET Core Runtime", "Desktop Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.ASPNETCore)).ToList();

        Assert.ContainsSingle(matched);
        Assert.AreEqual("ASP.NET Core Runtime", matched[0]);
    }

    [TestMethod]
    public void DesktopFilter_IsResilientToOrdering_WhenBaseRuntimeAppearsFirst()
    {
        var names = new[] { ".NET Core Runtime", "Desktop Runtime", "ASP.NET Core Runtime" };
        var matched = names.Where(n => ReleaseManifest.IsMatchingRuntimeComponent(n, InstallComponent.WindowsDesktop)).ToList();

        Assert.ContainsSingle(matched);
        Assert.AreEqual("Desktop Runtime", matched[0]);
    }

    // ---- Case insensitivity ----

    [TestMethod]
    [DataRow("asp.net core runtime", InstallComponent.ASPNETCore)]
    [DataRow("ASP.NET CORE RUNTIME", InstallComponent.ASPNETCore)]
    [DataRow(".net core runtime", InstallComponent.Runtime)]
    [DataRow(".NET RUNTIME", InstallComponent.Runtime)]
    [DataRow("desktop runtime", InstallComponent.WindowsDesktop)]
    [DataRow("DESKTOP RUNTIME", InstallComponent.WindowsDesktop)]
    public void Filters_AreCaseInsensitive(string name, InstallComponent component)
    {
        Assert.IsTrue(ReleaseManifest.IsMatchingRuntimeComponent(name, component));
    }

    // ---- IsCompositeArchive tests ----

    [TestMethod]
    [DataRow("aspnetcore-runtime-composite-linux-x64.tar.gz")]
    [DataRow("aspnetcore-runtime-composite-linux-arm64.tar.gz")]
    [DataRow("aspnetcore-runtime-composite-linux-musl-x64.tar.gz")]
    [DataRow("aspnetcore-runtime-composite-linux-musl-arm64.tar.gz")]
    [DataRow("aspnetcore-runtime-composite-linux-musl-arm.tar.gz")]
    [DataRow("ASPNETCORE-RUNTIME-COMPOSITE-LINUX-X64.TAR.GZ")]
    public void IsCompositeArchive_ReturnsTrueForCompositeFiles(string fileName)
    {
        Assert.IsTrue(ReleaseManifest.IsCompositeArchive(fileName));
    }

    [TestMethod]
    [DataRow("aspnetcore-runtime-linux-x64.tar.gz")]
    [DataRow("aspnetcore-runtime-linux-arm64.tar.gz")]
    [DataRow("dotnet-runtime-linux-x64.tar.gz")]
    [DataRow("dotnet-runtime-win-x64.zip")]
    [DataRow("windowsdesktop-runtime-win-x64.zip")]
    [DataRow("dotnet-sdk-linux-x64.tar.gz")]
    public void IsCompositeArchive_ReturnsFalseForRegularFiles(string fileName)
    {
        Assert.IsFalse(ReleaseManifest.IsCompositeArchive(fileName));
    }

    // ---- Archive selection prefers the regular archive when composites are present ----

    [TestMethod]
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

        Assert.HasCount(2, nonComposite);
        foreach (var f in nonComposite)
        {
            Assert.DoesNotContain("composite", f);
        }
    }

    // ---- IsApphostPackArchive tests ----

    [TestMethod]
    [DataRow("dotnet-apphost-pack-win-x64.zip")]
    [DataRow("dotnet-apphost-pack-linux-arm64.tar.gz")]
    [DataRow("dotnet-apphost-pack-linux-musl-x64.tar.gz")]
    [DataRow("DOTNET-APPHOST-PACK-WIN-X64.ZIP")]
    public void IsApphostPackArchive_ReturnsTrueForApphostPacks(string fileName)
    {
        Assert.IsTrue(ReleaseManifest.IsApphostPackArchive(fileName));
    }

    [TestMethod]
    [DataRow("dotnet-runtime-win-x64.zip")]
    [DataRow("dotnet-runtime-linux-x64.tar.gz")]
    [DataRow("aspnetcore-runtime-linux-x64.tar.gz")]
    [DataRow("windowsdesktop-runtime-win-x64.zip")]
    [DataRow("dotnet-sdk-linux-x64.tar.gz")]
    public void IsApphostPackArchive_ReturnsFalseForRegularFiles(string fileName)
    {
        Assert.IsFalse(ReleaseManifest.IsApphostPackArchive(fileName));
    }

    [TestMethod]
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

        Assert.HasCount(2, filtered);
        foreach (var f in filtered)
        {
            Assert.Contains("dotnet-runtime", f);
        }
    }

    // ---- ClassifyArchiveMiss tests (no user-installable archive) ----

    [TestMethod]
    public void ClassifyArchiveMiss_WindowsDesktopRuntimeWithOnlyExeInstallers_IsUserInstallableArtifactMiss()
    {
        // A specific Windows Desktop Runtime release that ships only .exe installers for the
        // platform (no .tar.gz, no .zip). dotnetup cannot xcopy-install these, so this must be
        // classified as a user-actionable miss, not the platform-has-no-files miss.
        var ridFileNames = new[]
        {
            "windowsdesktop-runtime-3.1.32-win-x64.exe",
            "windowsdesktop-runtime-3.1.32-win-x86.exe",
        };

        var result = ReleaseManifest.ClassifyArchiveMiss(ridFileNames);

        result.Status.Should().Be(FindReleaseFileStatus.NoUserInstallableArtifact);
    }

    [TestMethod]
    public void ClassifyArchiveMiss_NoFilesForPlatform_IsNoMatchingFile()
    {
        // When the release lists no files at all for the RID, the platform is genuinely
        // unsupported — this stays the product-category NoMatchingFile miss.
        var result = ReleaseManifest.ClassifyArchiveMiss(Array.Empty<string>());

        result.Status.Should().Be(FindReleaseFileStatus.NoMatchingFile);
    }
}
