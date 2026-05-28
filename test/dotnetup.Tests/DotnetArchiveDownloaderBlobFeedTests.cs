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
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for the blob feed fallback path used when the release manifest does
/// not list the requested version (e.g. daily/preview builds).
/// </summary>
public class DotnetArchiveDownloaderBlobFeedTests
{
    private readonly ITestOutputHelper _log;

    public DotnetArchiveDownloaderBlobFeedTests(ITestOutputHelper log)
    {
        _log = log;
    }

    [Theory]
    [InlineData(InstallComponent.SDK, "Sdk", "dotnet-sdk")]
    [InlineData(InstallComponent.Runtime, "Runtime", "dotnet-runtime")]
    [InlineData(InstallComponent.ASPNETCore, "aspnetcore/Runtime", "aspnetcore-runtime")]
    [InlineData(InstallComponent.WindowsDesktop, "WindowsDesktop", "windowsdesktop-runtime")]
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

    [Fact]
    public void ParseHashFile_ReturnsHashFromBareHexLine()
    {
        string hash = new string('a', 128);
        BlobFeedUrlBuilder.ParseHashFile(hash).Should().Be(hash);
    }

    [Fact]
    public void ParseHashFile_HandlesTrailingWhitespace()
    {
        string hash = new string('b', 128);
        BlobFeedUrlBuilder.ParseHashFile(hash + "\r\n").Should().Be(hash);
        BlobFeedUrlBuilder.ParseHashFile("  " + hash + "  ").Should().Be(hash);
    }

    [Fact]
    public void ParseHashFile_HandlesShasum512Format()
    {
        string hash = new string('c', 128);
        BlobFeedUrlBuilder.ParseHashFile($"{hash}  dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip\n")
            .Should().Be(hash);
    }

    [Fact]
    public void ParseHashFile_LowercasesHash()
    {
        string upper = new string('A', 128);
        string lower = new string('a', 128);
        BlobFeedUrlBuilder.ParseHashFile(upper).Should().Be(lower);
    }

    [Fact]
    public void ParseHashFile_RejectsWrongLength()
    {
        Assert.Throws<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile(new string('a', 64)));
    }

    [Fact]
    public void ParseHashFile_RejectsNonHex()
    {
        // 128 chars including 'g' (not hex)
        string bad = new string('a', 127) + "g";
        Assert.Throws<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile(bad));
    }

    [Fact]
    public void ParseHashFile_RejectsEmpty()
    {
        Assert.Throws<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile(""));
        Assert.Throws<FormatException>(() => BlobFeedUrlBuilder.ParseHashFile("   \r\n"));
    }

    /// <summary>
    /// When the user supplies a fully specified prerelease version that is not
    /// in the release manifest, the downloader should fall back to the blob feed.
    /// </summary>
    [Fact]
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
    [Fact]
    public void ResolveManifestEntry_DoesNotFallback_ForStableVersion()
    {
        const string version = "10.0.100";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.Throws<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.VersionNotFound);
        history.Should().BeEmpty("blob feeds must not be probed for stable versions");
    }

    /// <summary>
    /// When the channel string is not a fully specified version (e.g. "preview"),
    /// a manifest miss is a real error — don't probe blob feeds.
    /// </summary>
    [Fact]
    public void ResolveManifestEntry_DoesNotFallback_ForNamedChannel()
    {
        const string channel = "preview";
        const string resolved = "10.0.100-preview.4.25216.37";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.Throws<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(channel, InstallComponent.SDK), new ReleaseVersion(resolved)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.VersionNotFound);
        history.Should().BeEmpty("named channel misses must not fall back to blob feeds");
    }

    /// <summary>
    /// When the product is in the manifest but the specific release is not, the
    /// thrown error must surface as ReleaseNotFound (distinct from VersionNotFound).
    /// </summary>
    [Fact]
    public void ResolveManifestEntry_ReleaseNotFound_PreservedDistinctlyFromVersionNotFound()
    {
        const string channel = "preview";
        const string resolved = "10.0.100-preview.4.25216.37";
        var (handler, _) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.ReleaseNotFound);

        var ex = Assert.Throws<DotnetInstallException>(() =>
            InvokeResolveManifestEntry(downloader, BuildRequest(channel, InstallComponent.SDK), new ReleaseVersion(resolved)));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.ReleaseNotFound);
    }

    /// <summary>
    /// If both feeds 404 the .sha512 file, surface a clear VersionNotFound error.
    /// On Windows, both tar.gz and zip are probed before failing.
    /// </summary>
    [Fact]
    public void ResolveManifestEntry_BlobFeed404_ThrowsVersionNotFound()
    {
        const string version = "10.0.100-preview.4.25216.37";
        var (handler, history) = BuildHandler(new());

        using var http = new HttpClient(handler);
        var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var ex = Assert.Throws<DotnetInstallException>(() =>
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
    [PlatformSpecificFact(TestPlatforms.Windows)]
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
        using var downloader = CreateDownloader(http, manifestThrows: DotnetInstallErrorCode.VersionNotFound);

        var (url, hash) = InvokeResolveManifestEntry(downloader, BuildRequest(version, InstallComponent.SDK), new ReleaseVersion(version));

        url.Should().Be($"https://ci.dot.net/public/Sdk/{version}/dotnet-sdk-{version}-{rid}.zip");
        hash.Should().Be(expectedHash);
        history.Should().HaveCount(2, "should probe tar.gz first, then fall back to zip");
    }

    /// <summary>
    /// Runtime-shaped versions (patch &lt; 100) fall back correctly to the Runtime feed path.
    /// </summary>
    [Fact]
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

    // --- Test helpers ---

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
                _ => throw new DotnetInstallException(
                    _code,
                    $"Test stub: manifest failure for {resolvedVersion}",
                    version: resolvedVersion.ToString(),
                    component: installRequest.Component.ToString()),
            };
        }
    }
}
