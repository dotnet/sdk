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
    /// (e.g. <c>10.0</c> or <c>10.0.1xx</c>); the rest identifies the SDK
    /// archive for the current OS/architecture. We always use the SDK archive
    /// for version discovery because VMR-built daily versions share the same
    /// SDK version across components.
    /// </summary>
    private const string AkaMsTemplate = "https://aka.ms/dotnet/{0}/daily/dotnet-sdk-{1}{2}";

    /// <summary>
    /// Hosts that aka.ms is allowed to redirect to. The redirect target tells
    /// us where the daily build is actually published; if it points anywhere
    /// else (e.g. through DNS hijacking or a misconfigured redirect), we
    /// refuse to trust the URL for version extraction.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedRedirectHosts =
    [
        "ci.dot.net",
        "builds.dotnet.microsoft.com",
        "dotnetbuilds.azureedge.net",
    ];

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
    /// Returns <c>null</c> if no daily build is available.
    /// </summary>
    public ReleaseVersion? Resolve(UpdateChannel channel, InstallArchitecture architecture)
    {
        if (!channel.IsDaily)
        {
            throw new ArgumentException($"Channel '{channel.Name}' is not a daily channel.", nameof(channel));
        }

        string rid = DotnetupUtilities.GetRuntimeIdentifier(architecture);
        string extension = DotnetupUtilities.GetArchiveFileExtensionForPlatform();

        // Bare "daily": probe major+1 first so we pick up a new major as soon
        // as it starts producing daily builds, before its first GA release
        // updates the manifest.
        if (channel.Name.Equals(ChannelVersionResolver.DailyChannel, StringComparison.OrdinalIgnoreCase))
        {
            int latestMajor = GetLatestManifestMajor();

            ReleaseVersion? candidate = TryResolvePartialVersion($"{latestMajor + 1}.0", rid, extension);
            if (candidate != null)
            {
                return candidate;
            }

            return TryResolvePartialVersion($"{latestMajor}.0", rid, extension);
        }

        // "<M>-daily" → use "<M>.0" as the aka.ms partial version (aka.ms paths use major.minor).
        string partialVersion = NormalizePartialVersion(channel.BaseChannel);
        return TryResolvePartialVersion(partialVersion, rid, extension);
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
        var index = _releaseManifest.GetReleasesIndex();
        int latestMajor = 0;
        foreach (var product in index)
        {
            if (int.TryParse(product.ProductVersion.Split('.')[0], out var major) && major > latestMajor)
            {
                latestMajor = major;
            }
        }

        return latestMajor;
    }

    private ReleaseVersion? TryResolvePartialVersion(string partialVersion, string rid, string extension)
    {
        string akaMsUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture, AkaMsTemplate, partialVersion, rid, extension);

        Uri finalUri;
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
        }
        catch (HttpRequestException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.NetworkError,
                $"Failed to resolve daily channel '{partialVersion}-daily' via {akaMsUrl}: {ex.Message}",
                ex);
        }

        if (!IsAllowedRedirectTarget(finalUri))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.NetworkError,
                $"Daily channel '{partialVersion}-daily' redirected to disallowed host '{finalUri.Host}' (expected one of: {string.Join(", ", AllowedRedirectHosts)}).");
        }

        var version = ExtractVersionFromUrl(finalUri);
        if (version == null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.NetworkError,
                $"Could not extract a version from daily channel redirect target '{finalUri}'.");
        }

        return version;
    }

    /// <summary>
    /// Returns true when <paramref name="uri"/> uses HTTPS and points at a
    /// host on <see cref="AllowedRedirectHosts"/>.
    /// </summary>
    public static bool IsAllowedRedirectTarget(Uri uri)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AllowedRedirectHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

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
