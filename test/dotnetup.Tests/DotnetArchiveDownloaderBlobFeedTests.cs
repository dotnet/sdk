// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for the blob feed fallback path used when the release manifest does
/// not list the requested version (e.g. daily/preview builds).
/// </summary>
[TestClass]
public class DotnetArchiveDownloaderBlobFeedTests : IDisposable
{
    private readonly ITestOutputHelper _log;

    public DotnetArchiveDownloaderBlobFeedTests(TestContext testContext)
    {
        _log = new TestContextOutputHelper(testContext);
        // Defensive: clear any IT-policy override leaked from an earlier test.
        UnsignedSourcePolicy.OverrideForTesting = null;
    }

    public void Dispose() => UnsignedSourcePolicy.OverrideForTesting = null;

    [TestMethod]
    [DataRow(InstallComponent.SDK, "Sdk", "dotnet-sdk")]
    [DataRow(InstallComponent.Runtime, "Runtime", "dotnet-runtime")]
    [DataRow(InstallComponent.ASPNETCore, "aspnetcore/Runtime", "aspnetcore-runtime")]
    [DataRow(InstallComponent.WindowsDesktop, "WindowsDesktop", "windowsdesktop-runtime")]
    public void GetFeedLocation_BuildsExpectedUrls(InstallComponent component, string expectedDir, string expectedPrefix)
    {
        var version = new ReleaseVersion("10.0.100-preview.4.25216.37");
        const string rid = "win-x64";
        const string ext = ".zip";

        var location = BlobFeedUrlBuilder.GetFeedLocation(component, version, rid, ext);

        string expectedFile = $"{expectedPrefix}-10.0.100-preview.4.25216.37-win-x64.zip";

        location.ArchiveUrl.Should().Be(
            $"https://ci.dot.net/public/{expectedDir}/10.0.100-preview.4.25216.37/{expectedFile}");
        location.ChecksumUrl.Should().Be(
            $"https://ci.dot.net/public-checksums/{expectedDir}/10.0.100-preview.4.25216.37/{expectedFile}.sha512");
    }

    [TestMethod]
    public void ParseHashFile_ReturnsHashFromBareHexLine()
    {
        string hash = new string('a', 128);
        BlobFeedUrlBuilder.ParseHashFile(hash).Should().Be(hash);
    }

    [TestMethod]
    public void ParseHashFile_HandlesTrailingWhitespace()
    {
        string hash = new string('b', 128);
        BlobFeedUrlBuilder.ParseHashFile(hash + "\r\n").Should().Be(hash);
        BlobFeedUrlBuilder.ParseHashFile("  " + hash + "  ").Should().Be(hash);
    }

    [TestMethod]
    public void ParseHashFile_HandlesShasum512Format()
    {
        string hash = new string('c', 128);
        BlobFeedUrlBuilder.ParseHashFile($"{hash}  dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip\n")
            .Should().Be(hash);
    }

    [TestMethod]
    public void ParseHashFile_LowercasesHash()
    {
        string upper = new string('A', 128);
        string lower = new string('a', 128);
        BlobFeedUrlBuilder.ParseHashFile(upper).Should().Be(lower);
    }

    [TestMethod]
    public void ParseHashFile_RejectsWrongLength()
    {
        Assert.ThrowsExactly<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile(new string('a', 64)));
    }

    [TestMethod]
    public void ParseHashFile_RejectsNonHex()
    {
        // 128 chars including 'g' (not hex)
        string bad = new string('a', 127) + "g";
        Assert.ThrowsExactly<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile(bad));
    }

    [TestMethod]
    public void ParseHashFile_RejectsEmpty()
    {
        Assert.ThrowsExactly<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile(""));
        Assert.ThrowsExactly<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile("   \r\n"));
    }

    /// <summary>
    /// When the user supplies a fully specified prerelease version that is not
    /// in the release manifest, the downloader should fall back to the blob feed.
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_FallsBackToBlobFeed_ForUnknownPrerelease()
    {
        const string version = "10.0.100-preview.4.25216.37";
        string rid = DotnetupUtilities.GetRuntimeIdentifier(InstallArchitecture.x64);
        string ext = DotnetupTestUtilities.DefaultArchiveFileExtension;
        string expectedHash = new string('d', 128);
        var (handler, history) = BuildHandler(new()
        {
            [$"https://ci.dot.net/public-checksums/Sdk/{version}/dotnet-sdk-{version}-{rid}{ext}.sha512"] = (HttpStatusCode.OK, expectedHash + "\n"),
        });

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var (url, hash) = InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version));

        url.Should().Be($"https://ci.dot.net/public/Sdk/{version}/dotnet-sdk-{version}-{rid}{ext}");
        hash.Should().Be(expectedHash);
        history.Should().HaveCount(1, "should probe blob feed once");
    }

    /// <summary>
    /// Manifest miss for a stable (non-prerelease) version must NOT fall back to
    /// blob feeds — it's a real error.
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_DoesNotFallback_ForStableVersion()
    {
        const string version = "10.0.100";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.VersionNotFound);
        history.Should().BeEmpty("blob feeds must not be probed for stable versions");
    }

    /// <summary>
    /// When the channel string is not a fully specified version (e.g. "preview"),
    /// a manifest miss is a real error — don't probe blob feeds.
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_DoesNotFallback_ForNamedChannel()
    {
        const string channel = "preview";
        const string resolved = "10.0.100-preview.4.25216.37";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(channel, InstallComponent.SDK), new ReleaseVersion(resolved)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.VersionNotFound);
        history.Should().BeEmpty("named channel misses must not fall back to blob feeds");
    }

    /// <summary>
    /// When the product is in the manifest but the specific release is not, the
    /// thrown error must surface as ReleaseNotFound (distinct from VersionNotFound).
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_ReleaseNotFound_PreservedDistinctlyFromVersionNotFound()
    {
        const string channel = "preview";
        const string resolved = "10.0.100-preview.4.25216.37";
        var (handler, _) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.ReleaseNotFound);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(channel, InstallComponent.SDK), new ReleaseVersion(resolved)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.ReleaseNotFound);
    }

    /// <summary>
    /// Regression for dotnet/sdk#54906 follow-up: some Windows Desktop Runtime releases publish
    /// only non-installable artifacts (e.g. .exe installers) for the platform — no .tar.gz and no
    /// .zip. dotnetup cannot perform a user-level (xcopy) install of those, so the manifest lookup
    /// surfaces <see cref="FindReleaseFileStatus.NoUserInstallableArtifact"/>. The downloader must
    /// turn that into the user-actionable <see cref="DotnetInstallErrorCode.NoUserInstallableArtifact"/>
    /// error (NOT the product-category <see cref="DotnetInstallErrorCode.NoMatchingReleaseFileForPlatform"/>).
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_WindowsDesktopRuntimeWithNoArchive_ThrowsUserInstallableArtifactError()
    {
        // A specific Windows Desktop Runtime version whose release lists only .exe installers.
        const string version = "3.1.32";
        var (handler, _) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.NoUserInstallableArtifact);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.WindowsDesktop), new ReleaseVersion(version)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.NoUserInstallableArtifact);
        ex.ErrorCode.Should().NotBe(DotnetInstallErrorCode.NoMatchingReleaseFileForPlatform,
            "a release that ships only non-installable artifacts is a user error, not a product failure");
        ex.Message.Should().Be(ExpectedNoUserInstallableArtifactMessage(InstallComponent.WindowsDesktop, version));
    }

    [TestMethod]
    public void NoUserInstallableArtifact_ClassifiedAsUserError()
    {
        ErrorCategoryClassifier.ClassifyInstallError(DotnetInstallErrorCode.NoUserInstallableArtifact)
            .Should().Be(ErrorCategory.User,
                "a release with no user-installable archive is a user/environment condition, not a product bug");
    }

    /// <summary>
    /// End-to-end: parse a real Windows Desktop Runtime release (via the deployment library)
    /// whose manifest lists only .exe installers — no .tar.gz and no .zip — then drive the full
    /// download entry point <see cref="DotnetArchiveDownloader.DownloadArchiveWithVerification"/>.
    /// The resolve-then-download path must surface the user-actionable
    /// <see cref="DotnetInstallErrorCode.NoUserInstallableArtifact"/> error and must NOT issue any
    /// HTTP download (it fails during manifest resolution, before touching the network).
    /// Windows-only because the RID and .zip fallback are Windows-specific.
    /// </summary>
    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void DownloadArchiveWithVerification_WindowsDesktopRuntimeWithOnlyExeInstallers_FailsWithUserError_AndDoesNotDownload()
    {
        const string version = "3.1.32";

        // A real WindowsDesktopReleaseComponent parsed from a releases.json that publishes only
        // .exe installers for win-x64/win-x86 — exactly the shape that historically had no
        // user-installable (xcopy) archive.
        ReleaseComponent windowsDesktop = ParseWindowsDesktopComponentWithOnlyExeInstallers(version);

        var (handler, history) = BuildHandler(new());
        using var http = new HttpClient(handler);
        var cacheDir = Path.Combine(Path.GetTempPath(), "dotnetup-test-cache-" + Guid.NewGuid().ToString("N"));
        var downloader = new DotnetArchiveDownloader(new FixtureReleaseManifest(windowsDesktop), http, cacheDir);

        string destinationBase = Path.Combine(cacheDir, "windowsdesktop-runtime");

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            downloader.DownloadArchiveWithVerification(
                BuildRequest(version, InstallComponent.WindowsDesktop),
                new ReleaseVersion(version),
                destinationBase));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.NoUserInstallableArtifact);
        ex.Message.Should().Be(ExpectedNoUserInstallableArtifactMessage(InstallComponent.WindowsDesktop, version));
        history.Should().BeEmpty("resolution must fail before any archive is downloaded");
    }

    /// <summary>
    /// Writes a minimal but schema-valid releases.json containing a single Windows Desktop Runtime
    /// release whose only files are .exe installers, parses it with the deployment library, and
    /// returns the real <see cref="ReleaseComponent"/>.
    /// </summary>
    private static ReleaseComponent ParseWindowsDesktopComponentWithOnlyExeInstallers(string version)
    {
        string json = $$"""
        {
          "releases": [
            {
              "release-date": "2024-01-09",
              "release-version": "{{version}}",
              "security": false,
              "windowsdesktop": {
                "version": "{{version}}",
                "version-display": "{{version}}",
                "files": [
                  {
                    "name": "windowsdesktop-runtime-{{version}}-win-x64.exe",
                    "rid": "win-x64",
                    "url": "https://example.test/windowsdesktop-runtime-{{version}}-win-x64.exe",
                    "hash": "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"
                  },
                  {
                    "name": "windowsdesktop-runtime-{{version}}-win-x86.exe",
                    "rid": "win-x86",
                    "url": "https://example.test/windowsdesktop-runtime-{{version}}-win-x86.exe",
                    "hash": "1111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"
                  }
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
            var releases = Product.GetReleasesAsync(path).GetAwaiter().GetResult();
            var component = releases.Single().WindowsDesktopRuntime;
            component.Should().NotBeNull("the fixture defines a windowsdesktop component");
            return component;
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// If both feeds 404 the .sha512 file, surface a clear VersionNotFound error.
    /// On Windows, both tar.gz and zip are probed before failing.
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_BlobFeed404_ThrowsVersionNotFound()
    {
        const string version = "10.0.100-preview.4.25216.37";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.VersionNotFound);
        ex.Message.Should().Contain(version);

        // On Windows, both tar.gz and zip are probed; on other platforms only tar.gz
        int expectedProbes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 2 : 1;
        history.Should().HaveCount(expectedProbes);
    }

    /// <summary>
    /// On Windows, when the tar.gz archive is not available, the downloader falls back to zip.
    /// </summary>
    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void ResolveManifestEntry_FallsBackToZip_WhenTarGzNotAvailable_OnWindows()
    {
        const string version = "10.0.100-preview.4.25216.37";
        string rid = DotnetupUtilities.GetRuntimeIdentifier(InstallArchitecture.x64);
        string expectedHash = new string('f', 128);
        var (handler, history) = BuildHandler(new()
        {
            // tar.gz checksum not available (404), zip checksum is available
            [$"https://ci.dot.net/public-checksums/Sdk/{version}/dotnet-sdk-{version}-{rid}.zip.sha512"] = (HttpStatusCode.OK, expectedHash + "\n"),
        });

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var (url, hash) = InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version));

        url.Should().Be($"https://ci.dot.net/public/Sdk/{version}/dotnet-sdk-{version}-{rid}.zip");
        hash.Should().Be(expectedHash);
        history.Should().HaveCount(2, "should probe tar.gz first, then fall back to zip");
    }

    /// <summary>
    /// Runtime-shaped versions (patch &lt; 100) fall back correctly to the Runtime feed path.
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_FallsBackToRuntimeFeed_ForRuntimeVersion()
    {
        const string version = "10.0.0-preview.4.25216.10";
        string rid = DotnetupUtilities.GetRuntimeIdentifier(InstallArchitecture.x64);
        string ext = DotnetupTestUtilities.DefaultArchiveFileExtension;
        string expectedHash = new string('e', 128);
        var (handler, _) = BuildHandler(new()
        {
            [$"https://ci.dot.net/public-checksums/Runtime/{version}/dotnet-runtime-{version}-{rid}{ext}.sha512"] = (HttpStatusCode.OK, expectedHash),
        });

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var (url, hash) = InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.Runtime), new ReleaseVersion(version));

        url.Should().Be($"https://ci.dot.net/public/Runtime/{version}/dotnet-runtime-{version}-{rid}{ext}");
        hash.Should().Be(expectedHash);
    }

    // --- UnsignedSourcePolicy coverage ---

    [TestMethod]
    [DataRow("daily", true)]                          // bare daily channel
    [DataRow("10.0-daily", true)]                     // suffixed daily channel
    [DataRow("10.0.100-preview.4.25216.37", true)]    // fully-specified prerelease
    [DataRow("10.0.100", false)]                      // stable version
    [DataRow("preview", false)]                       // named channel (resolved via signed manifest)
    [DataRow("lts", false)]                           // named channel
    [DataRow("10.0", false)]                          // major.minor channel
    public void MayDownloadUnsigned_ClassifiesChannel(string channel, bool expected)
        => UnsignedSourcePolicy.MayDownloadUnsigned(BuildRequest(channel, InstallComponent.SDK))
            .Should().Be(expected);

    /// <summary>
    /// Defense-in-depth: when the IT "block unsigned downloads" policy is in effect,
    /// the blob-feed fallback inside the downloader must refuse with a clear
    /// <see cref="DotnetInstallErrorCode.UnsignedDownloadBlockedByPolicy"/> error
    /// and issue no HTTP probe.
    /// </summary>
    [TestMethod]
    public void ResolveManifestEntry_BlockedByPolicy_ThrowsClearError()
    {
        UnsignedSourcePolicy.OverrideForTesting = () => true;

        const string version = "10.0.100-preview.4.25216.37";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.UnsignedDownloadBlockedByPolicy);
        ex.Message.Should().Contain(version);
        history.Should().BeEmpty("policy check must short-circuit before any blob-feed probe");
    }

    [TestMethod]
    public void UnsignedDownloadBlockedByPolicy_ClassifiedAsUserError()
    {
        ErrorCategoryClassifier.ClassifyInstallError(DotnetInstallErrorCode.UnsignedDownloadBlockedByPolicy)
            .Should().Be(ErrorCategory.User, "IT-policy blocks are user/environment errors, not product bugs");
    }

    [TestMethod]
    public void MayDownloadUnsigned_BatchPredicate_TrueWhenAnyRequestIsDaily()
    {
        var requests = new List<DotnetInstallRequest>
        {
            BuildRequest("10.0.100", InstallComponent.SDK),       // stable — no warning
            BuildRequest("daily", InstallComponent.Runtime),       // daily — triggers warning
            BuildRequest("10.0.100", InstallComponent.ASPNETCore), // stable — no warning
        };

        requests.Any(r => UnsignedSourcePolicy.MayDownloadUnsigned(r)).Should().BeTrue(
            "a batch containing at least one daily request should trigger the unsigned-download warning");
    }

    [TestMethod]
    public void MayDownloadUnsigned_BatchPredicate_FalseWhenAllStable()
    {
        var requests = new List<DotnetInstallRequest>
        {
            BuildRequest("10.0.100", InstallComponent.SDK),
            BuildRequest("10.0", InstallComponent.Runtime),
            BuildRequest("lts", InstallComponent.ASPNETCore),
        };

        requests.Any(r => UnsignedSourcePolicy.MayDownloadUnsigned(r)).Should().BeFalse(
            "a batch of stable/named-channel requests should not trigger the unsigned-download warning");
    }

    // --- Test helpers ---

    /// <summary>
    /// Builds the expected user-facing message from the same resx resource the product code uses,
    /// so the assertion can't drift from the shipped string.
    /// </summary>
    private static string ExpectedNoUserInstallableArtifactMessage(InstallComponent component, string version) =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Strings.NoUserInstallableArtifact,
            component,
            new ReleaseVersion(version));

    private static DotnetInstallRequest BuildRequest(string channel, InstallComponent component)
    {
        var root = new DotnetInstallRoot(@"C:\dotnet-test", InstallArchitecture.x64);
        return new DotnetInstallRequest(root, new UpdateChannel(channel), component, new InstallRequestOptions());
    }

    private static DotnetArchiveDownloader CreateDownloader(HttpClient http, DotnetInstallErrorCode manifestThrows)
    {
        var manifest = new ThrowingReleaseManifest(manifestThrows);
        // Use a per-test cache directory so we don't pollute the user cache.
        var cacheDir = Path.Combine(Path.GetTempPath(), "dotnetup-test-cache-" + Guid.NewGuid().ToString("N"));
        return new DotnetArchiveDownloader(manifest, http, cacheDir);
    }

    private static (string DownloadUrl, string ExpectedHash) InvokeResolveManifestEntry(
        DotnetArchiveDownloader downloader,
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion)
    {
        var method = typeof(DotnetArchiveDownloader).GetMethod("ResolveManifestEntry",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("ResolveManifestEntry must exist on DotnetArchiveDownloader");

        try
        {
            var result = method!.Invoke(downloader, new object[] { request, resolvedVersion });
            // Tuple comes back as ValueTuple<string, string>; unpack via reflection-friendly cast.
            var tuple = ((string DownloadUrl, string ExpectedHash))result!;
            return tuple;
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            throw tie.InnerException;
        }
    }

    private static (RecordingHandler Handler, List<string> History) BuildHandler(Dictionary<string, (HttpStatusCode, string)> responses)
    {
        var history = new List<string>();
        var handler = new RecordingHandler(responses, history);
        return (handler, history);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode, string)> _responses;
        private readonly List<string> _history;

        public RecordingHandler(Dictionary<string, (HttpStatusCode, string)> responses, List<string> history)
        {
            _responses = responses;
            _history = history;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            _history.Add(url);

            if (_responses.TryGetValue(url, out var entry))
            {
                var resp = new HttpResponseMessage(entry.Item1)
                {
                    Content = new StringContent(entry.Item2, Encoding.UTF8, "text/plain"),
                };
                return Task.FromResult(resp);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class ThrowingReleaseManifest : ReleaseManifest
    {
        private readonly DotnetInstallErrorCode _code;

        public ThrowingReleaseManifest(DotnetInstallErrorCode code)
        {
            _code = code;
        }

        public override FindReleaseFileResult TryFindReleaseFile(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
        {
            return _code switch
            {
                DotnetInstallErrorCode.VersionNotFound => FindReleaseFileResult.ProductNotFound,
                DotnetInstallErrorCode.ReleaseNotFound => FindReleaseFileResult.ReleaseNotFound,
                DotnetInstallErrorCode.NoUserInstallableArtifact => FindReleaseFileResult.NoUserInstallableArtifact,
                _ => throw new DotnetInstallException(
                    _code,
                    $"Test stub: manifest failure for {resolvedVersion}",
                    version: resolvedVersion.ToString(),
                    component: installRequest.Component.ToString()),
            };
        }
    }

    /// <summary>
    /// A <see cref="ReleaseManifest"/> that runs the real
    /// <see cref="ReleaseManifest.ResolveReleaseFile"/> selection/classification logic against a
    /// real <see cref="ReleaseComponent"/> parsed from a fixture, so the download path is
    /// exercised end-to-end without network or signing.
    /// </summary>
    private sealed class FixtureReleaseManifest : ReleaseManifest
    {
        private readonly ReleaseComponent _component;

        public FixtureReleaseManifest(ReleaseComponent component)
        {
            _component = component;
        }

        public override FindReleaseFileResult TryFindReleaseFile(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
            => ResolveReleaseFile(_component, installRequest);
    }
}
