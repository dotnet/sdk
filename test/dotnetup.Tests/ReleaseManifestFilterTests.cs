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

    // ---- Windows installer bundle selection ----

    [TestMethod]
    [DataRow("dotnet-sdk-win-x64.exe", InstallComponent.SDK, true)]
    [DataRow("dotnet-runtime-win-x64.exe", InstallComponent.Runtime, true)]
    [DataRow("aspnetcore-runtime-win-x64.exe", InstallComponent.ASPNETCore, true)]
    [DataRow("windowsdesktop-runtime-3.1.32-win-x64.exe", InstallComponent.WindowsDesktop, true)]
    [DataRow("DOTNET-SDK-WIN-ARM64.EXE", InstallComponent.SDK, true)]
    [DataRow("dotnet-hosting-win.exe", InstallComponent.ASPNETCore, false)]
    [DataRow("dotnet-runtime-win-x64.exe", InstallComponent.SDK, false)]
    [DataRow("dotnet-sdk-win-x64.zip", InstallComponent.SDK, false)]
    [DataRow("dotnet-sdk-win-x64.exe.sha512", InstallComponent.SDK, false)]
    public void IsInstallerBundleFor_MatchesOnlyTheComponentBundle(string fileName, InstallComponent component, bool expected)
    {
        ReleaseManifest.IsInstallerBundleFor(fileName, component).Should().Be(expected);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(InstallComponent.SDK, "dotnet-sdk-win-x64.exe")]
    [DataRow(InstallComponent.Runtime, "dotnet-runtime-win-x64.exe")]
    [DataRow(InstallComponent.ASPNETCore, "aspnetcore-runtime-win-x64.exe")]
    [DataRow(InstallComponent.WindowsDesktop, "windowsdesktop-runtime-win-x64.exe")]
    public void ResolveReleaseFile_BundleMechanism_SelectsComponentBundle(InstallComponent component, string expectedName)
    {
        var result = ReleaseManifest.ResolveReleaseFile(GetFixtureComponent(component), BuildRequest(component, InstallMechanism.WindowsInstallerBundle));

        result.Status.Should().Be(FindReleaseFileStatus.Found);
        result.File!.Name.Should().Be(expectedName);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(InstallComponent.SDK, "dotnet-sdk-win-x64.zip")]
    [DataRow(InstallComponent.Runtime, "dotnet-runtime-win-x64.zip")]
    [DataRow(InstallComponent.ASPNETCore, "aspnetcore-runtime-win-x64.zip")]
    [DataRow(InstallComponent.WindowsDesktop, "windowsdesktop-runtime-win-x64.zip")]
    public void ResolveReleaseFile_ArchiveMechanism_StillSelectsArchive(InstallComponent component, string expectedName)
    {
        var result = ReleaseManifest.ResolveReleaseFile(GetFixtureComponent(component), BuildRequest(component, InstallMechanism.Archive));

        result.Status.Should().Be(FindReleaseFileStatus.Found);
        result.File!.Name.Should().Be(expectedName);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ResolveReleaseFile_BundleMechanism_WithoutBundle_IsUserInstallableArtifactMiss()
    {
        // The fixture's x86 RID lists only an archive for the SDK.
        var request = BuildRequest(InstallComponent.SDK, InstallMechanism.WindowsInstallerBundle, InstallArchitecture.x86);

        var result = ReleaseManifest.ResolveReleaseFile(GetFixtureComponent(InstallComponent.SDK), request);

        result.Status.Should().Be(FindReleaseFileStatus.NoUserInstallableArtifact);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ResolveReleaseFile_BundleMechanism_WithoutFilesForRid_IsNoMatchingFile()
    {
        var request = BuildRequest(InstallComponent.Runtime, InstallMechanism.WindowsInstallerBundle, InstallArchitecture.arm64);

        var result = ReleaseManifest.ResolveReleaseFile(GetFixtureComponent(InstallComponent.Runtime), request);

        result.Status.Should().Be(FindReleaseFileStatus.NoMatchingFile);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ResolveReleaseFile_BundleMechanism_NeverSelectsBundleOffWindows()
    {
        var result = ReleaseManifest.ResolveReleaseFile(GetFixtureComponent(InstallComponent.SDK), BuildRequest(InstallComponent.SDK, InstallMechanism.WindowsInstallerBundle));

        result.Status.Should().NotBe(FindReleaseFileStatus.Found);
    }

    private static DotnetInstallRequest BuildRequest(InstallComponent component, InstallMechanism mechanism, InstallArchitecture architecture = InstallArchitecture.x64) =>
        new(
            new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-unused-root"), architecture),
            new UpdateChannel("9.0.0"),
            component,
            new InstallRequestOptions { Untracked = true, InstallMechanism = mechanism });

    private static Microsoft.Deployment.DotNet.Releases.ReleaseComponent GetFixtureComponent(InstallComponent component)
    {
        var release = s_fixtureRelease.Value;
        return component switch
        {
            InstallComponent.SDK => release.Sdks.Single(),
            InstallComponent.Runtime => release.Runtime,
            InstallComponent.ASPNETCore => release.AspNetCoreRuntime,
            InstallComponent.WindowsDesktop => release.WindowsDesktopRuntime,
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
    }

    private static readonly Lazy<Microsoft.Deployment.DotNet.Releases.ProductRelease> s_fixtureRelease = new(ParseFixtureRelease);

    private static Microsoft.Deployment.DotNet.Releases.ProductRelease ParseFixtureRelease()
    {
        string json = $$"""
        {
          "releases": [
            {
              "release-date": "2024-11-12",
              "release-version": "9.0.0",
              "security": false,
              "runtime": {
                "version": "9.0.0",
                "version-display": "9.0.0",
                "files": [
                  {{FixtureFile("dotnet-apphost-pack-win-x64.zip", "win-x64")}},
                  {{FixtureFile("dotnet-runtime-win-x64.exe", "win-x64")}},
                  {{FixtureFile("dotnet-runtime-win-x64.zip", "win-x64")}},
                  {{FixtureFile("dotnet-runtime-linux-x64.tar.gz", "linux-x64")}}
                ]
              },
              "sdk": {
                "version": "9.0.100",
                "version-display": "9.0.100",
                "runtime-version": "9.0.0",
                "files": [
                  {{FixtureFile("dotnet-sdk-win-x64.exe", "win-x64")}},
                  {{FixtureFile("dotnet-sdk-win-x64.zip", "win-x64")}},
                  {{FixtureFile("dotnet-sdk-win-x86.zip", "win-x86")}},
                  {{FixtureFile("dotnet-sdk-linux-x64.tar.gz", "linux-x64")}}
                ]
              },
              "sdks": [
                {
                  "version": "9.0.100",
                  "version-display": "9.0.100",
                  "runtime-version": "9.0.0",
                  "files": [
                    {{FixtureFile("dotnet-sdk-win-x64.exe", "win-x64")}},
                    {{FixtureFile("dotnet-sdk-win-x64.zip", "win-x64")}},
                    {{FixtureFile("dotnet-sdk-win-x86.zip", "win-x86")}},
                    {{FixtureFile("dotnet-sdk-linux-x64.tar.gz", "linux-x64")}}
                  ]
                }
              ],
              "aspnetcore-runtime": {
                "version": "9.0.0",
                "version-display": "9.0.0",
                "files": [
                  {{FixtureFile("aspnetcore-runtime-composite-win-x64.exe", "win-x64")}},
                  {{FixtureFile("dotnet-hosting-win.exe", "win-x64")}},
                  {{FixtureFile("aspnetcore-runtime-win-x64.exe", "win-x64")}},
                  {{FixtureFile("aspnetcore-runtime-win-x64.zip", "win-x64")}},
                  {{FixtureFile("aspnetcore-runtime-linux-x64.tar.gz", "linux-x64")}}
                ]
              },
              "windowsdesktop": {
                "version": "9.0.0",
                "version-display": "9.0.0",
                "files": [
                  {{FixtureFile("windowsdesktop-runtime-win-x64.exe", "win-x64")}},
                  {{FixtureFile("windowsdesktop-runtime-win-x64.zip", "win-x64")}}
                ]
              }
            }
          ]
        }
        """;

        string path = Path.Combine(Path.GetTempPath(), "dotnetup-test-releases-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            return Microsoft.Deployment.DotNet.Releases.Product.GetReleasesAsync(path).GetAwaiter().GetResult().Single();
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }

    private static string FixtureFile(string name, string rid) => $$"""
        { "name": "{{name}}", "rid": "{{rid}}", "url": "https://example.test/{{name}}", "hash": "{{new string('0', 128)}}" }
        """;
}
