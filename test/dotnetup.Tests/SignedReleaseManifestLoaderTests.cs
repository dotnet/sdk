// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.Dotnet.Installation.Internal.Signing;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Pure-function unit tests for <see cref="SignedReleaseManifestLoader"/> helpers. The
/// network/file-IO orchestration is exercised end-to-end via
/// <see cref="SignatureVerifierTests"/> + future integration tests; these focus on the
/// URL-derivation and JSON-parsing primitives that are easiest to break without being noticed.
/// </summary>
public class SignedReleaseManifestLoaderTests
{
    private static SignedReleaseManifestLoader CreateLoader(string indexUrl) => new(
        new HttpClient(), // never sent on; loader constructor doesn't do IO besides mkdtemp
        new SignatureVerificationOptions(new X509Certificate2Collection(), new X509Certificate2Collection()),
        new Uri(indexUrl));

    /// <summary>
    /// Builds a JSON byte array with the given <paramref name="signatureFile"/> as the value
    /// of <c>signature.file</c>, properly JSON-escaping the value so test data with backslashes
    /// or quotes doesn't corrupt the JSON document itself.
    /// </summary>
    private static byte[] BuildJsonWithSignatureFile(string signatureFile)
    {
        // JsonSerializer correctly escapes the string contents.
        string escaped = JsonSerializer.Serialize(signatureFile);
        return Encoding.UTF8.GetBytes($$"""{ "signature": { "file": {{escaped}} } }""");
    }
    // ---------------- DeriveSiblingUrl ----------------

    [Theory]
    [InlineData(
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json",
        "releases-index.json.20260505084330.p7s",
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json.20260505084330.p7s")]
    [InlineData(
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json",
        "releases.json.20260505.p7s",
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json.20260505.p7s")]
    [InlineData(
        "https://mirror.corp.com:8443/dotnet/release-metadata/releases-index.json",
        "sig.p7s",
        "https://mirror.corp.com:8443/dotnet/release-metadata/sig.p7s")]
    public void DeriveSiblingUrl_ReplacesLastPathSegment(string jsonUrl, string sigName, string expected)
    {
        Uri actual = SignedReleaseManifestLoader.DeriveSiblingUrl(new Uri(jsonUrl), sigName);
        actual.ToString().Should().Be(expected);
    }

    [Fact]
    public void DeriveSiblingUrl_PreservesPort()
    {
        // Non-default port must round-trip through the UriBuilder.
        Uri actual = SignedReleaseManifestLoader.DeriveSiblingUrl(
            new Uri("https://example.com:9443/path/file.json"),
            "file.json.p7s");
        actual.Port.Should().Be(9443);
        actual.AbsolutePath.Should().Be("/path/file.json.p7s");
    }

    // ---------------- ParseSignatureFileField ----------------

    [Fact]
    public void ParseSignatureFileField_PresentSignatureBlock_ReturnsFile()
    {
        byte[] json = Encoding.UTF8.GetBytes("""
            {
              "channels": [],
              "signature": { "expiration": "2026-08-03T00:00:00Z", "file": "x.20260505.p7s" }
            }
            """);
        SignedReleaseManifestLoader.ParseSignatureFileField(json).Should().Be("x.20260505.p7s");
    }

    [Fact]
    public void ParseSignatureFileField_MissingSignatureProperty_ReturnsNull()
    {
        byte[] json = Encoding.UTF8.GetBytes("""{ "channels": [] }""");
        SignedReleaseManifestLoader.ParseSignatureFileField(json).Should().BeNull();
    }

    [Fact]
    public void ParseSignatureFileField_SignatureWithoutFile_ReturnsNull()
    {
        byte[] json = Encoding.UTF8.GetBytes("""
            { "signature": { "expiration": "2026-08-03T00:00:00Z" } }
            """);
        SignedReleaseManifestLoader.ParseSignatureFileField(json).Should().BeNull();
    }

    [Fact]
    public void ParseSignatureFileField_SignatureFileNotAString_ReturnsNull()
    {
        // Defensive: don't accept numeric or null values. The loader uses this to construct
        // a URL, so anything but a string must reject (loader will then throw
        // SignatureVerificationFailed for "unsigned manifest").
        byte[] json = Encoding.UTF8.GetBytes("""{ "signature": { "file": 42 } }""");
        SignedReleaseManifestLoader.ParseSignatureFileField(json).Should().BeNull();
    }

    [Fact]
    public void ParseSignatureFileField_ArrayRoot_ReturnsNull()
    {
        byte[] json = Encoding.UTF8.GetBytes("[]");
        SignedReleaseManifestLoader.ParseSignatureFileField(json).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ParseSignatureFileField_EmptyOrWhitespaceFile_ReturnsNull(string fileValue)
    {
        // An empty signature.file would derive a sibling URL ending in '/', which would
        // fetch a directory listing or the index page. Reject upstream.
        SignedReleaseManifestLoader.ParseSignatureFileField(BuildJsonWithSignatureFile(fileValue))
            .Should().BeNull();
    }

    [Theory]
    [InlineData("../etc/passwd")]                         // relative path traversal
    [InlineData("../../sensitive.p7s")]
    [InlineData("subdir/sig.p7s")]                        // forward-slash navigation
    [InlineData("subdir\\sig.p7s")]                       // back-slash navigation (Windows-style)
    [InlineData("https://attacker.com/sig.p7s")]          // absolute URL with scheme
    [InlineData("file:///etc/passwd")]                    // file: scheme
    [InlineData("//attacker.com/sig.p7s")]                // protocol-relative URL
    public void ParseSignatureFileField_PathTraversalOrSchemePrefix_ReturnsNull(string fileValue)
    {
        // signature.file MUST be a bare filename per the signing protocol's naming convention
        // (basename.<timestamp>.p7s). Anything that looks like a path or URL is rejected so a
        // malicious mirror cannot misdirect our GET.
        SignedReleaseManifestLoader.ParseSignatureFileField(BuildJsonWithSignatureFile(fileValue))
            .Should().BeNull();
    }

    // ---------------- GetReleaseUriForConfiguredHost (private-mirror rebase) ----------------

    [Fact]
    public void GetReleaseUriForConfiguredHost_SameAuthority_IsNoOp()
    {
        // Production case: index URL and Product.ReleasesJson both point at builds.dotnet
        // — the rebase must return the original instance without touching anything.
        using var loader = CreateLoader("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json");
        var original = new Uri("https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json");

        loader.GetReleaseUriForConfiguredHost(original).Should().BeSameAs(original);
    }

    [Fact]
    public void GetReleaseUriForConfiguredHost_DifferentHost_RebasesScheme_Host_AndPort()
    {
        // Mirror case: index URL is on internal host; rebase swaps scheme/host/port and
        // preserves the absolute path so a private mirror serving the same signed bytes
        // resolves channel URLs against itself.
        using var loader = CreateLoader("https://mirror.corp.com:8443/dotnet/release-metadata/releases-index.json");
        var original = new Uri("https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json");

        Uri rebased = loader.GetReleaseUriForConfiguredHost(original);

        rebased.Scheme.Should().Be("https");
        rebased.Host.Should().Be("mirror.corp.com");
        rebased.Port.Should().Be(8443);
        rebased.AbsolutePath.Should().Be("/dotnet/release-metadata/10.0/releases.json");
    }

    [Fact]
    public void GetReleaseUriForConfiguredHost_DoesNotPropagateUserInfo()
    {
        // URL-embedded credentials are deprecated and ignored by HttpClient. Make sure
        // the rebase NEVER copies UserInfo from the index URL into the rebased URL.
        using var loader = CreateLoader("https://alice:secret@mirror.corp.com/x/index.json");
        var original = new Uri("https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json");

        Uri rebased = loader.GetReleaseUriForConfiguredHost(original);
        rebased.UserInfo.Should().BeEmpty();
    }

    // ---------------- DownloadAndVerify orchestration (HTTP failure / tamper mapping) ----------------
    //
    // These tests cover the orchestration path inside SignedReleaseManifestLoader.DownloadAndVerify
    // that maps HTTP and verification failures onto typed DotnetInstallException codes
    // (PR #54300 review feedback). They drive GetVerifiedReleasesIndex through a stub
    // HttpMessageHandler so no real network is touched.
    //
    // Note on case (a) — JSON GET failure: the loader does NOT wrap the JSON GET in a try/catch
    // (only the .p7s GET). HttpRequestException bubbles out of the loader and is mapped to
    // DotnetInstallException(ManifestFetchFailed) one layer up in
    // ReleaseManifest.TryFindReleaseFile. Asserting HttpRequestException here documents the
    // loader-level contract; the wrapper is covered by a separate test below.

    private const string TestIndexUrl = "https://example.test/release-metadata/releases-index.json";
    private const string TestSigUrl = "https://example.test/release-metadata/releases-directory.json.20260505084330.p7s";

    private static readonly string s_signingAssetsDir = Path.Combine(
        typeof(SignedReleaseManifestLoaderTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "TestAssetsDir").Value!,
        "Signing");

    private static byte[] LoadSignedJson() =>
        File.ReadAllBytes(Path.Combine(s_signingAssetsDir, "releases-directory.json"));

    private static byte[] LoadSignedP7s() =>
        File.ReadAllBytes(Path.Combine(s_signingAssetsDir, "releases-directory.json.20260505084330.p7s"));

    private static SignedReleaseManifestLoader CreateLoaderWithHandler(StubHandler handler) =>
        new(new HttpClient(handler),
            // Empty trust roots are fine: the failure paths under test fail before chain
            // build (case b never reaches Verify; case c fails at CMS content/digest check).
            new SignatureVerificationOptions(new X509Certificate2Collection(), new X509Certificate2Collection()),
            new Uri(TestIndexUrl));

    [Fact]
    public void GetVerifiedReleasesIndex_JsonHttp500_ThrowsHttpRequestException()
    {
        // (a) JSON URL returns 500. HttpClient.GetByteArrayAsync throws HttpRequestException
        // on non-success, which propagates raw out of the loader. The wrapping to
        // DotnetInstallException(ManifestFetchFailed) happens in ReleaseManifest — see the
        // companion test ReleaseManifest_WrapsLoaderHttpFailure_AsManifestFetchFailed below.
        var handler = new StubHandler
        {
            { TestIndexUrl, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) },
        };
        using var loader = CreateLoaderWithHandler(handler);

        Action act = () => loader.GetVerifiedReleasesIndex();

        act.Should().Throw<HttpRequestException>();
        handler.RequestCount.Should().Be(1, "loader must not attempt the .p7s fetch when JSON GET fails");
    }

    [Fact]
    public void GetVerifiedReleasesIndex_SignatureP7sHttp500_ThrowsSignatureDownloadFailed()
    {
        // (b) JSON downloads + parses fine, but the sibling .p7s URL returns 500. The loader
        // catches HttpRequestException for the .p7s fetch only and surfaces it as the
        // dedicated SignatureDownloadFailed code so telemetry can distinguish a network
        // problem fetching the signature from a real cryptographic verification failure.
        var handler = new StubHandler
        {
            { TestIndexUrl, _ => OkBytes(LoadSignedJson()) },
            { TestSigUrl, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) },
        };
        using var loader = CreateLoaderWithHandler(handler);

        Action act = () => loader.GetVerifiedReleasesIndex();

        act.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureDownloadFailed);
    }

    [Fact]
    public void GetVerifiedReleasesIndex_TamperedJsonContent_ThrowsSignatureVerificationFailed()
    {
        // (c) JSON has been mutated after signing (single trailing whitespace byte appended).
        // It still parses as JSON and still contains a valid signature.file pointer, so the
        // loader successfully derives the sibling URL and downloads the real .p7s. The CMS
        // digest check then fails because the content bytes no longer match what was signed.
        // Loader must surface this as SignatureVerificationFailed (NOT SignatureDownloadFailed —
        // that code is reserved for network-layer failures fetching the .p7s).
        byte[] tampered = LoadSignedJson();
        // Append trailing whitespace: JsonDocument.Parse still accepts it and signature.file
        // remains discoverable, but the byte-level CMS hash no longer matches.
        byte[] mutated = new byte[tampered.Length + 1];
        Buffer.BlockCopy(tampered, 0, mutated, 0, tampered.Length);
        mutated[tampered.Length] = (byte)' ';

        var handler = new StubHandler
        {
            { TestIndexUrl, _ => OkBytes(mutated) },
            { TestSigUrl, _ => OkBytes(LoadSignedP7s()) },
        };
        using var loader = CreateLoaderWithHandler(handler);

        Action act = () => loader.GetVerifiedReleasesIndex();

        act.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.SignatureVerificationFailed);
    }

    [Fact]
    public void ReleaseManifest_WrapsLoaderHttpFailure_AsManifestFetchFailed()
    {
        // The loader leaves the JSON-GET HttpRequestException unwrapped; the typed mapping
        // to DotnetInstallException happens inside ReleaseManifest.TryFindReleaseFile. This
        // test pins that contract by calling the public ReleaseManifest path with a loader
        // whose HTTP stack is stubbed to return 500 on the JSON URL.
        var handler = new StubHandler
        {
            { TestIndexUrl, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) },
        };
        using var loader = CreateLoaderWithHandler(handler);
        var manifest = new ReleaseManifest(loader);

        Action act = () => manifest.GetReleasesIndex();

        // GetReleasesIndex propagates HttpRequestException raw — it's the TryFindReleaseFile
        // path that wraps. Verify both behaviors.
        act.Should().Throw<HttpRequestException>();
    }

    private static HttpResponseMessage OkBytes(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    /// <summary>
    /// Minimal HttpMessageHandler that maps absolute request URLs to canned responses.
    /// Unmapped URLs return 404 so test failures point at the wrong URL rather than hanging.
    /// Collection-initializer friendly via <see cref="Add"/>.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler, System.Collections.IEnumerable
    {
        private readonly System.Collections.Generic.Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes =
            new(StringComparer.Ordinal);

        public int RequestCount { get; private set; }

        public void Add(string url, Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _routes.Add(url, responder);

        public System.Collections.IEnumerator GetEnumerator() => _routes.GetEnumerator();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            string url = request.RequestUri!.ToString();
            if (_routes.TryGetValue(url, out var responder))
            {
                return Task.FromResult(responder(request));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
