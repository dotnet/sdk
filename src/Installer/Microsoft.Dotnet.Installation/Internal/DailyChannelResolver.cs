// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Resolves daily-build channels (e.g. <c>10.0-daily</c>, <c>daily</c>) to a
/// concrete <see cref="ReleaseVersion"/> by querying the aka.ms redirect for
/// the latest daily SDK archive and extracting the version from the redirect
/// target URL. The resolved version is then handed to the existing blob-feed
/// download path for the actual install.
/// </summary>
internal sealed class DailyChannelResolver : IDisposable
{
    /// <summary>
    /// aka.ms link template. The <c>{0}</c> placeholder is the channel's partial version
    /// (e.g. <c>10.0</c> or <c>10.0.1xx</c>); <c>{1}</c> is the component-specific
    /// archive prefix (e.g. <c>dotnet-sdk</c>, <c>dotnet-runtime</c>); <c>{2}</c> is
    /// the RID; <c>{3}</c> is the platform archive extension. Component matters
    /// because daily SDK and runtime builds are published at different base versions
    /// (e.g. SDK 10.0.110 vs Runtime 10.0.10, sharing the same prerelease tag).
    /// </summary>
    private const string AkaMsTemplate = "https://aka.ms/dotnet/{0}/daily/{1}-{2}{3}";

    private readonly HttpClient _httpClient;
    private readonly ReleaseManifest _releaseManifest;
    private readonly bool _shouldDisposeHttpClient;

    public DailyChannelResolver(ReleaseManifest? releaseManifest = null, HttpClient? httpClient = null)
    {
        _releaseManifest = releaseManifest ?? new ReleaseManifest();
        if (httpClient == null)
        {
            _httpClient = CreateDefaultHttpClient();
            _shouldDisposeHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _shouldDisposeHttpClient = false;
        }
    }

    /// <summary>
    /// Resolves the latest daily-build version for <paramref name="channel"/>.
    /// Returns <c>null</c> if no daily build is available. The version is
    /// discovered from the aka.ms shortlink for the requested
    /// <paramref name="component"/>, since daily SDK and runtime builds share
    /// the same prerelease tag but differ in base version.
    /// </summary>
    public ReleaseVersion? Resolve(UpdateChannel channel, InstallArchitecture architecture, InstallComponent component = InstallComponent.SDK)
    {
        if (!channel.IsDaily)
        {
            throw new ArgumentException($"Channel '{channel.Name}' is not a daily channel.", nameof(channel));
        }

        string rid = DotnetupUtilities.GetRuntimeIdentifier(architecture);
        // aka.ms has historically published the daily SDK as .zip for Windows RIDs
        // and .tar.gz elsewhere. (.tar.gz is now also published for Windows — see PR
        // #54467 — but .zip is the long-standing format we know is always present
        // back through older daily channels.) We only need one URL that resolves to
        // the daily build to extract a version from the redirect target, so pick
        // the historically-published extension for the platform.
        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".zip" : ".tar.gz";
        string archivePrefix = BlobFeedUrlBuilder.GetArchivePrefix(component);

        // Bare "daily": probe major+1 first so we pick up a new major as soon
        // as it starts producing daily builds, before its first GA release
        // updates the manifest. TryResolvePartialVersion returns null when
        // aka.ms doesn't have a link for the requested major, so the fallback
        // to the manifest's highest major runs as designed.
        if (channel.Name.Equals(ChannelVersionResolver.DailyChannel, StringComparison.OrdinalIgnoreCase))
        {
            int latestMajor = GetLatestManifestMajor();

            ReleaseVersion? candidate = TryResolvePartialVersion($"{latestMajor + 1}.0", archivePrefix, rid, extension);
            if (candidate != null)
            {
                return candidate;
            }

            return TryResolvePartialVersion($"{latestMajor}.0", archivePrefix, rid, extension);
        }

        // "<M>-daily" → use "<M>.0" as the aka.ms partial version (aka.ms paths use major.minor).
        // For prerelease-qualified daily channels ("<band>-preview.5-daily"), translate the
        // label to aka.ms's dotless form ("preview5") so the URL has the shape aka.ms
        // expects: ".../<band>-preview5/daily/...".
        string scope = UpdateChannel.StripDailySuffix(channel.Name);
        string partialVersion;
        if (UpdateChannel.TrySplitPartialVersionAndPrereleaseLabel(scope, out var bandPart, out var prereleaseLabel))
        {
            string akaMsLabel = prereleaseLabel.Replace(".", string.Empty, StringComparison.Ordinal);
            partialVersion = $"{NormalizePartialVersion(bandPart)}-{akaMsLabel}";
        }
        else
        {
            partialVersion = NormalizePartialVersion(scope);
        }

        return TryResolvePartialVersion(partialVersion, archivePrefix, rid, extension);
    }

    /// <summary>
    /// Converts a channel's partial version into the form expected by aka.ms paths:
    /// bare-major <c>10</c> becomes <c>10.0</c>; major.minor and feature-band
    /// partial versions pass through unchanged.
    /// </summary>
    private static string NormalizePartialVersion(string partialVersion)
    {
        if (int.TryParse(partialVersion, out _))
        {
            return $"{partialVersion}.0";
        }

        return partialVersion;
    }

    private int GetLatestManifestMajor()
    {
        // The manifest is designed so that the first product is always the latest major
        // (same assumption ChannelVersionResolver.GetLatestVersionForMajorOrMajorMinor relies on).
        return _releaseManifest.GetReleasesIndex().FirstOrDefault()?.LatestReleaseVersion?.Major ?? 0;
    }

    private ReleaseVersion? TryResolvePartialVersion(string partialVersion, string archivePrefix, string rid, string extension)
    {
        string akaMsUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture, AkaMsTemplate, partialVersion, archivePrefix, rid, extension);

        Uri finalUri;
        string? contentType;
        try
        {
            using var response = _httpClient.GetAsync(akaMsUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            finalUri = response.RequestMessage?.RequestUri
                ?? throw new DotnetInstallException(
                    DotnetInstallErrorCode.NetworkError,
                    $"Could not determine the redirect target for daily channel '{partialVersion}-daily'.");
            contentType = response.Content.Headers.ContentType?.MediaType;
        }
        catch (HttpRequestException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.NetworkError,
                $"Failed to resolve daily channel '{partialVersion}-daily' via {akaMsUrl}: {ex.Message}",
                ex);
        }

        if (IsAkaMsShortlinkNotFound(finalUri) || IsHtmlContent(contentType))
        {
            // Two ways aka.ms can tell us "no daily build available":
            //  * URL pattern: unknown shortlinks redirect to https://www.bing.com/?ref=aka&shorturl=...
            //  * Content type: the fallback page is text/html rather than a binary archive.
            // Either signal returns null so callers (like the bare 'daily' probe of
            // major+1) can fall back to the next candidate.
            return null;
        }

        var version = ExtractVersionFromUrl(finalUri);
        if (version == null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.NetworkError,
                $"Could not extract a version from daily channel redirect target '{finalUri}'.");
        }

        // UpdateChannel.Matches restricts daily channels to prerelease versions,
        // so returning a stable version here would create an installation that
        // the channel's own matcher disagrees with. Treat a stable redirect
        // target as "no daily build available for this scope" so the bare 'daily'
        // probe can fall back to the next candidate.
        if (string.IsNullOrEmpty(version.Prerelease))
        {
            return null;
        }

        return version;
    }

    /// <summary>
    /// Detects the aka.ms "shortlink not found" redirect: when an aka.ms URL
    /// has no registered target, the service redirects to a Bing landing page
    /// of the form <c>https://www.bing.com/?ref=aka&amp;shorturl=&lt;original-path&gt;</c>.
    /// The <c>ref=aka</c> query parameter is the unambiguous marker that the
    /// redirect originated from aka.ms's not-found fallback (rather than a
    /// legitimate redirect chain that happens to terminate on bing.com).
    /// </summary>
    public static bool IsAkaMsShortlinkNotFound(Uri uri)
    {
        if (!uri.Host.Equals("www.bing.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("bing.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = uri.Query;
        return query.Contains("ref=aka", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Treats <c>text/html</c> as "this isn't a daily-build archive". A real
    /// daily-build redirect terminates on a binary archive (typically served
    /// as <c>application/octet-stream</c>); HTML responses are the aka.ms
    /// not-found fallback or any future error page they switch to.
    /// </summary>
    public static bool IsHtmlContent(string? mediaType) =>
        mediaType is not null && mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts the .NET version from a daily-build redirect URL. The path
    /// segments include the version (e.g. <c>.../Sdk/10.0.100-preview.4.25216.37/dotnet-sdk-...</c>);
    /// we return the first segment that parses as a <see cref="ReleaseVersion"/>.
    /// </summary>
    public static ReleaseVersion? ExtractVersionFromUrl(Uri uri)
    {
        foreach (var segment in uri.Segments)
        {
            var trimmed = segment.Trim('/');
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (ReleaseVersion.TryParse(trimmed, out var version))
            {
                return version;
            }
        }

        return null;
    }

    /// <summary>
    /// HttpClient configured to follow redirects (so we can read the final
    /// <see cref="HttpResponseMessage.RequestMessage"/> URI after aka.ms
    /// resolves).
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = true,
            UseDefaultCredentials = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(2),
        };
    }

    public void Dispose()
    {
        if (_shouldDisposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
