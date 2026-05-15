// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal.Signing;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Downloads release manifest JSON, verifies its detached CMS signature against a sibling
/// <c>.p7s</c>, and hands the verified bytes to the deployment library's file-based parsing
/// APIs (<see cref="ProductCollection.GetFromFileAsync(string, bool)"/> and
/// <see cref="Product.GetReleasesAsync(string, bool)"/>) with <c>downloadLatest: false</c> so
/// the library only parses the local file and never re-fetches the JSON itself.
///
/// <para>
/// Net cost vs. the un-verified path: one extra small GET per JSON (the <c>.p7s</c>) and
/// ~100 ms of crypto. No double-download. The deployment library is used purely as a parser.
/// </para>
///
/// <para>
/// Threading: instance methods are sync but the loader can be invoked concurrently by the
/// orchestrator's parallel <c>PrepareInstall</c> path. <see cref="HttpClient"/> is
/// thread-safe; the temp directory is per-instance.
/// </para>
/// </summary>
internal sealed class SignedReleaseManifestLoader : IDisposable
{
    private static readonly TimeSpan ManifestFetchTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;
    private readonly SignatureVerificationOptions _options;
    private readonly Uri _indexUrl;
    private readonly string _tempDir;

    public SignedReleaseManifestLoader(HttpClient httpClient, SignatureVerificationOptions options, Uri indexUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _indexUrl = indexUrl ?? throw new ArgumentNullException(nameof(indexUrl));

        // Directory.CreateTempSubdirectory uses mkdtemp(3) on POSIX which atomically creates
        // the directory with mode 0700; on Windows %LOCALAPPDATA%\Temp is per-user-ACL'd.
        _tempDir = Directory.CreateTempSubdirectory("dotnetup-sigverify-").FullName;
    }

    /// <summary>
    /// Downloads <c>releases-index.json</c> from the configured index URL, verifies its
    /// detached signature, parses it via the deployment library, and returns the resulting
    /// <see cref="ProductCollection"/>.
    /// </summary>
    public ProductCollection GetVerifiedReleasesIndex()
    {
        string tempPath = DownloadAndVerify(_indexUrl);
        return ProductCollection.GetFromFileAsync(tempPath, downloadLatest: false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads a product's <c>releases.json</c>, verifies its detached signature, and
    /// returns the parsed releases. The channel URL is host-rebased onto the configured index
    /// URL via <see cref="GetReleaseUriForConfiguredHost"/> so private mirrors that serve the
    /// same signed index byte-for-byte resolve channel URLs against the mirror, not back to
    /// <c>builds.dotnet.microsoft.com</c>.
    /// </summary>
    public ReadOnlyCollection<ProductRelease> GetVerifiedReleases(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.ReleasesJson is null)
        {
            // Defensive: the deployment library reads this from the index JSON via GetUriOrDefault,
            // so a malformed / legacy entry could leave it null. Surface a clean error rather than NRE.
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ManifestParseFailed,
                $"Product '{product.ProductVersion}' has no releases.json URL in the verified release index.");
        }

        Uri channelUrl = GetReleaseUriForConfiguredHost(product.ReleasesJson);
        string tempPath = DownloadAndVerify(channelUrl);
        return product.GetReleasesAsync(tempPath, downloadLatest: false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Combines the scheme/host/port of <see cref="_indexUrl"/> with the absolute path of
    /// <paramref name="originalUrl"/>. If the index URL already shares the same authority as
    /// the original (the production case), this is a no-op.
    ///
    /// <para>
    /// Trust note: signature verification is signer-pinned (cert subject DN, EKU, chain),
    /// not host-pinned (dotnetup signature spec §5–6). Rebasing onto a private mirror does
    /// not weaken the cryptographic check.
    /// </para>
    /// </summary>
    internal Uri GetReleaseUriForConfiguredHost(Uri originalUrl)
    {
        if (string.Equals(originalUrl.Authority, _indexUrl.Authority, StringComparison.OrdinalIgnoreCase))
            return originalUrl;

        var builder = new UriBuilder(originalUrl)
        {
            Scheme = _indexUrl.Scheme,
            Host = _indexUrl.Host,
            Port = _indexUrl.Port,
            // UserInfo intentionally NOT copied. URL-embedded credentials (user:pass@host)
            // are deprecated and ignored by HttpClient. Configure auth on DefaultHttpClient
            // (UseDefaultCredentials, custom handler, env vars) — not in the URL.
        };
        return builder.Uri;
    }

    /// <summary>
    /// Downloads JSON bytes, fetches the sibling <c>.p7s</c>, runs signature verification, and
    /// writes the verified bytes to a temp file under <see cref="_tempDir"/>. Returns the temp
    /// path. Caller is responsible for not retaining the path beyond
    /// <see cref="Dispose"/> (the path is deleted with the temp directory).
    /// </summary>
    private string DownloadAndVerify(Uri jsonUrl)
    {
        // Manifest fetches use a tight per-request timeout independent of DefaultHttpClient.Instance's
        // 10-minute timeout (sized for archive downloads). A slow/stalling mirror should fail fast on
        // small JSON+sig fetches rather than hanging the install for 10 minutes.
        using var cts = new CancellationTokenSource(ManifestFetchTimeout);

        // 1. Download JSON bytes.
        byte[] jsonBytes = _httpClient.GetByteArrayAsync(jsonUrl, cts.Token).GetAwaiter().GetResult();

        // 2. Parse signature.file from the JSON body.
        string? sigFileName = ParseSignatureFileField(jsonBytes);
        if (sigFileName is null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.SignatureVerificationFailed,
                $"Release manifest at {jsonUrl} is unsigned — no 'signature' block found. " +
                "Signature verification is required. This may indicate an EOL channel or a tampered manifest.");
        }

        // 3. Derive sibling sig URL and download. A 404 here means the .p7s isn't co-hosted
        //    with the JSON — surface that as SignatureVerificationFailed (specific) rather
        //    than letting it bubble out as ManifestFetchFailed (which the outer catch in
        //    ReleaseManifest would attribute to the JSON URL, misleading the user).
        Uri sigUrl = DeriveSiblingUrl(jsonUrl, sigFileName);
        byte[] sigBytes;
        try
        {
            sigBytes = _httpClient.GetByteArrayAsync(sigUrl, cts.Token).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.SignatureVerificationFailed,
                $"Failed to download detached signature for {jsonUrl} from {sigUrl}: {ex.Message}",
                ex);
        }

        // 4. Verify in memory.
        VerificationResult result = SignatureVerifier.Verify(jsonBytes, sigBytes, _options);
        if (!result.IsValid)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.SignatureVerificationFailed,
                FormatVerificationFailure(result, jsonUrl));
        }

        // 5. Write verified bytes to per-instance temp dir for the deployment library to read.
        string tempPath = Path.Combine(_tempDir, Path.GetRandomFileName() + ".json");
        File.WriteAllBytes(tempPath, jsonBytes);
        return tempPath;
    }

    /// <summary>
    /// Reads the <c>signature.file</c> field from a JSON byte array. Returns <see langword="null"/>
    /// if the <c>signature</c> property or the <c>file</c> sub-property is absent, empty,
    /// whitespace, or contains characters that would make it more than a bare filename
    /// (path separators, scheme prefix). Defense in depth: a malicious mirror could otherwise
    /// point us at <c>"../../etc/passwd"</c> or <c>"https://attacker/x.p7s"</c>; we reject
    /// those upstream rather than relying on signature verification to catch the misdirected
    /// fetch later.
    /// </summary>
    internal static string? ParseSignatureFileField(byte[] jsonBytes)
    {
        using var doc = JsonDocument.Parse(jsonBytes);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        if (!doc.RootElement.TryGetProperty("signature", out JsonElement sig) ||
            sig.ValueKind != JsonValueKind.Object ||
            !sig.TryGetProperty("file", out JsonElement fileEl) ||
            fileEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? file = fileEl.GetString();
        if (string.IsNullOrWhiteSpace(file)) return null;

        // Bare filename only — no path traversal, no absolute URL, no directory navigation.
        // The signing protocol publishes filenames like "releases-index.json.<timestamp>.p7s".
        if (file.Contains('/', StringComparison.Ordinal) ||
            file.Contains('\\', StringComparison.Ordinal) ||
            file.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        return file;
    }

    /// <summary>
    /// Derives a sibling URL by replacing the last path segment of <paramref name="jsonUrl"/>
    /// with <paramref name="sigFileName"/>. Preserves scheme, host, port, query, fragment.
    /// </summary>
    internal static Uri DeriveSiblingUrl(Uri jsonUrl, string sigFileName)
    {
        var builder = new UriBuilder(jsonUrl);
        int lastSlash = builder.Path.LastIndexOf('/', StringComparison.Ordinal);
        if (lastSlash < 0)
        {
            // Defensive: shouldn't happen for HTTP URLs which always have at least one '/'.
            builder.Path = "/" + sigFileName;
        }
        else
        {
            builder.Path = string.Concat(builder.Path.AsSpan(0, lastSlash + 1), sigFileName);
        }
        return builder.Uri;
    }

    private static string FormatVerificationFailure(VerificationResult result, Uri url)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Signature verification failed for {url}: {result.Failures.Count} issue(s)");
        foreach (VerificationFailure f in result.Failures)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  - {f.Code}: {f.Reason}");
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Cleanup is best-effort; orphaned files are picked up by OS temp cleanup.
        }
    }
}
