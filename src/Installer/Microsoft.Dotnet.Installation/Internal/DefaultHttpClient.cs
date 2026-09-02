// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Process-wide proxy-aware <see cref="HttpClient"/> shared between
/// <see cref="DotnetArchiveDownloader"/> (archive downloads) and
/// <see cref="SignedReleaseManifestLoader"/> (manifest + signature downloads).
///
/// Lifetime: never disposed. <see cref="HttpClient"/> is thread-safe for sends; reusing a
/// single instance is the recommended pattern. Consumers MUST NOT call <c>Dispose</c>
/// on <see cref="Instance"/>.
///
/// Timeout (10 minutes) is sized for archive downloads. Manifest fetches override per-request
/// with a tight <see cref="CancellationTokenSource"/> so a slow mirror cannot stall an install.
/// </summary>
internal static class DefaultHttpClient
{
    public static HttpClient Instance { get; } = Create();

    private static HttpClient Create()
    {
        var handler = new HttpClientHandler()
        {
            UseProxy = true,
            UseDefaultCredentials = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            // Do NOT set AutomaticDecompression here. The archives are .tar.gz files
            // whose gzip layer is handled explicitly by the archive extractor.
            // Enabling automatic decompression causes HttpClient to add Accept-Encoding: gzip
            // and transparently strip the gzip layer when the CDN returns Content-Encoding: gzip,
            // resulting in a raw .tar on disk whose hash does not match the manifest's .tar.gz hash.
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Set user-agent to identify dotnetup in telemetry, including version
        var informationalVersion = typeof(DefaultHttpClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string userAgent = informationalVersion == null ? "dotnetup-dotnet-installer" : $"dotnetup-dotnet-installer/{informationalVersion}";

        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        return client;
    }
}
