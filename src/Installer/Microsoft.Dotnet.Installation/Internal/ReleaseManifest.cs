// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Handles downloading and parsing .NET release manifests to find the correct installer/archive for a given installation.
/// </summary>
internal class ReleaseManifest(HttpClient httpClient) : IDisposable
{
    /// <summary>
    /// Parses a version channel string into its components.
    /// </summary>
    /// <param name="channel">Channel string to parse (e.g., "9", "9.0", "9.0.1xx", "9.0.103")</param>
    /// <returns>Tuple containing (major, minor, featureBand, isFullySpecified)</returns>
    private (int Major, int Minor, string? FeatureBand, bool IsFullySpecified) ParseVersionChannel(UpdateChannel channel)
    {
        var parts = channel.Name.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : -1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : -1;

        // Check if we have a feature band (like 1xx) or a fully specified patch
        string? featureBand = null;
        bool isFullySpecified = false;

        if (parts.Length >= 3)
        {
            if (parts[2].EndsWith("xx"))
            {
                // Feature band pattern (e.g., "1xx")
                featureBand = parts[2].Substring(0, parts[2].Length - 2);
            }
            else if (int.TryParse(parts[2], out _))
            {
                // Fully specified version (e.g., "9.0.103")
                isFullySpecified = true;
            }
        }

        return (major, minor, featureBand, isFullySpecified);
    }

    public IEnumerable<string> GetSupportedChannels()
    {

        return ["latest", "preview", "lts", "sts",
            ..GetProductCollection()
                .Where(p => p.IsSupported)
                .OrderByDescending(p => p.LatestReleaseVersion)
                .SelectMany(GetChannelsForProduct)
        ];

        static IEnumerable<string> GetChannelsForProduct(Product product)
        {
            return [product.ProductVersion,
                ..product.GetReleasesAsync().GetAwaiter().GetResult()
                    .SelectMany(r => r.Sdks)
                    .Select(sdk => sdk.Version)
                    .OrderByDescending(v => v)
                    .Select(v => $"{v.Major}.{v.Minor}.{(v.Patch / 100)}xx")
                    .Distinct()
                    .ToList()
                ];                
        }

    }

    /// <summary>
    /// Finds the latest fully specified version for a given channel string (major, major.minor, or feature band).
    /// </summary>
    /// <param name="channel">Channel string (e.g., "9", "9.0", "9.0.1xx", "9.0.103", "lts", "sts", "preview")</param>
    /// <param name="mode">InstallMode.SDK or InstallMode.Runtime</param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public ReleaseVersion? GetLatestVersionForChannel(UpdateChannel channel, InstallComponent component)
    {
        if (string.Equals(channel.Name, "lts", StringComparison.OrdinalIgnoreCase) || string.Equals(channel.Name, "sts", StringComparison.OrdinalIgnoreCase))
        {
            var releaseType = string.Equals(channel.Name, "lts", StringComparison.OrdinalIgnoreCase) ? ReleaseType.LTS : ReleaseType.STS;
            var productIndex = GetProductCollection();
            return GetLatestVersionByReleaseType(productIndex, releaseType, component);
        }
        else if (string.Equals(channel.Name, "preview", StringComparison.OrdinalIgnoreCase))
        {
            var productIndex = GetProductCollection();
            return GetLatestPreviewVersion(productIndex, component);
        }
        else if (string.Equals(channel.Name, "latest", StringComparison.OrdinalIgnoreCase))
        {
            var productIndex = GetProductCollection();
            return GetLatestActiveVersion(productIndex, component);
        }

        var (major, minor, featureBand, isFullySpecified) = ParseVersionChannel(channel);

        // If major is invalid, return null
        if (major < 0)
        {
            return null;
        }

        // If the version is already fully specified, just return it as-is
        if (isFullySpecified)
        {
            return new ReleaseVersion(channel.Name);
        }

        // Load the index manifest
        var index = GetProductCollection();
        if (minor < 0)
        {
            return GetLatestVersionForMajorOrMajorMinor(index, major, component); // Major Only (e.g., "9")
        }
        else if (minor >= 0 && featureBand == null) // Major.Minor (e.g., "9.0")
        {
            return GetLatestVersionForMajorOrMajorMinor(index, major, component, minor);
        }
        else if (minor >= 0 && featureBand is not null) // Not Fully Qualified Feature band Version (e.g., "9.0.1xx")
        {
            return GetLatestVersionForFeatureBand(index, major, minor, featureBand, component);
        }

        return null;
    }

    private IEnumerable<Product> GetProductsInMajorOrMajorMinor(IEnumerable<Product> index, int major, int? minor = null)
    {
        var validProducts = index.Where(p => p.ProductVersion.StartsWith(minor is not null ? $"{major}.{minor}" : $"{major}."));
        return validProducts;
    }

    /// <summary>
    /// Gets the latest version for a major-only channel (e.g., "9").
    /// </summary>
    private ReleaseVersion? GetLatestVersionForMajorOrMajorMinor(IEnumerable<Product> index, int major, InstallComponent component, int? minor = null)
    {
        // Assumption: The manifest is designed so that the first product for a major version will always be latest.
        Product? latestProductWithMajor = GetProductsInMajorOrMajorMinor(index, major, minor).FirstOrDefault();
        return GetLatestReleaseVersionInProduct(latestProductWithMajor, component);
    }

    /// <summary>
    /// Gets the latest version based on support status (LTS or STS).
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="isLts">True for LTS (Long-Term Support), false for STS (Standard-Term Support)</param>
    /// <param name="mode">InstallComponent.SDK or InstallComponent.Runtime</param>
    /// <returns>Latest stable version string matching the support status, or null if none found</returns>
    private static ReleaseVersion? GetLatestVersionByReleaseType(IEnumerable<Product> index, ReleaseType releaseType, InstallComponent component)
    {
        var correctPhaseProducts = index?.Where(p => p.ReleaseType == releaseType) ?? Enumerable.Empty<Product>();
        return GetLatestActiveVersion(correctPhaseProducts, component);
    }

    /// <summary>
    /// Gets the latest preview version available.
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="mode">InstallComponent.SDK or InstallComponent.Runtime</param>
    /// <returns>Latest preview or GoLive version string, or null if none found</returns>
    private ReleaseVersion? GetLatestPreviewVersion(IEnumerable<Product> index, InstallComponent component)
    {
        ReleaseVersion? latestPreviewVersion = GetLatestVersionBySupportPhase(index, component, [SupportPhase.Preview, SupportPhase.GoLive]);
        if (latestPreviewVersion is not null)
        {
            return latestPreviewVersion;
        }

        return GetLatestVersionBySupportPhase(index, component, [SupportPhase.Active]);
    }

    /// <summary>
    /// Gets the latest version across all available products that matches the support phase.
    /// </summary>
    private static ReleaseVersion? GetLatestActiveVersion(IEnumerable<Product> index, InstallComponent component)
    {
        return GetLatestVersionBySupportPhase(index, component, [SupportPhase.Active]);
    }
    /// <summary>
    /// Gets the latest version across all available products that matches the support phase.
    /// </summary>
    private static ReleaseVersion? GetLatestVersionBySupportPhase(IEnumerable<Product> index, InstallComponent component, SupportPhase[] acceptedSupportPhases)
    {
        // A version in preview/ga/rtm support is considered Go Live and not Active.
        var activeSupportProducts = index?.Where(p => acceptedSupportPhases.Contains(p.SupportPhase));

        // The manifest is designed so that the first product will always be latest.
        Product? latestActiveSupportProduct = activeSupportProducts?.FirstOrDefault();

        return GetLatestReleaseVersionInProduct(latestActiveSupportProduct, component);
    }

    private static ReleaseVersion? GetLatestReleaseVersionInProduct(Product? product, InstallComponent component)
    {
        // Assumption: The latest runtime version will always be the same across runtime components.
        ReleaseVersion? latestVersion = component switch
        {
            InstallComponent.SDK => product?.LatestSdkVersion,
            _ => product?.LatestRuntimeVersion
        };

        return latestVersion;
    }

    /// <summary>
    ///  Replaces user input feature band strings into the full feature band.
    ///  This would convert '1xx' into '100'.
    ///  100 is not necessarily the latest but it is the feature band.
    ///  The other number in the band is the patch.
    /// </summary>
    /// <param name="band"></param>
    /// <returns></returns>
    private static int NormalizeFeatureBandInput(string band)
    {
        var bandString = band
            .Replace("X", "x")
            .Replace("x", "0")
            .PadRight(3, '0')
            .Substring(0, 3);
        return int.Parse(bandString);
    }


    /// <summary>
    /// Gets the latest version for a feature band channel (e.g., "9.0.1xx").
    /// </summary>
    private ReleaseVersion? GetLatestVersionForFeatureBand(ProductCollection index, int major, int minor, string featureBand, InstallComponent component)
    {
        if (component != InstallComponent.SDK)
        {
            return null;
        }

        var validProducts = GetProductsInMajorOrMajorMinor(index, major, minor);
        var latestProduct = validProducts.FirstOrDefault();
        var releases = latestProduct?.GetReleasesAsync().GetAwaiter().GetResult().ToList() ?? new List<ProductRelease>();
        var normalizedFeatureBand = NormalizeFeatureBandInput(featureBand);

        foreach (var release in releases)
        {
            foreach (var sdk in release.Sdks)
            {
                if (sdk.Version.SdkFeatureBand == normalizedFeatureBand)
                {
                    return sdk.Version;
                }
            }
        }

        return null;
    }

    private const int MaxRetryCount = 3;
    private const int RetryDelayMilliseconds = 1000;

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private ProductCollection? _productCollection;

    public ReleaseManifest()
        : this(CreateDefaultHttpClient())
    {
    }

    /// <summary>
    /// Creates an HttpClient with enhanced proxy support for enterprise environments.
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler()
        {
            UseProxy = true,
            UseDefaultCredentials = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Set user-agent to identify dnup in telemetry
        client.DefaultRequestHeaders.UserAgent.ParseAdd("dnup-dotnet-installer");

        return client;
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path with progress reporting.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download was successful, false otherwise</returns>
    protected async Task<bool> DownloadArchiveAsync(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        // Create temp file path in same directory for atomic move when complete
        string tempPath = $"{destinationPath}.download";

        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                // Try to get content length for progress reporting
                long? totalBytes = await GetContentLengthAsync(downloadUrl);

                // Make the actual download request
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Get the total bytes if we didn't get it before
                if (!totalBytes.HasValue && response.Content.Headers.ContentLength.HasValue)
                {
                    totalBytes = response.Content.Headers.ContentLength.Value;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920]; // 80KB buffer
                long bytesRead = 0;
                int read;

                var lastProgressReport = DateTime.UtcNow;

                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));

                    bytesRead += read;

                    // Report progress at most every 100ms to avoid UI thrashing
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressReport).TotalMilliseconds > 100)
                    {
                        lastProgressReport = now;
                        progress?.Report(new DownloadProgress(bytesRead, totalBytes));
                    }
                }

                // Final progress report
                progress?.Report(new DownloadProgress(bytesRead, totalBytes));

                // Ensure all data is written to disk
                await fileStream.FlushAsync();
                fileStream.Close();

                // Atomic move to final destination
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);

                return true;
            }
            catch (Exception)
            {
                // Delete the partial download if it exists
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                if (attempt < MaxRetryCount)
                {
                    await Task.Delay(RetryDelayMilliseconds * attempt); // Exponential backoff
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the content length of a resource.
    /// </summary>
    private async Task<long?> GetContentLengthAsync(string url)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(headRequest);
            return headResponse.Content.Headers.ContentLength;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path (synchronous version).
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download was successful, false otherwise</returns>
    protected bool DownloadArchive(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        return DownloadArchiveAsync(downloadUrl, destinationPath, progress).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads the archive for the specified installation and verifies its hash.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download and verification were successful, false otherwise</returns>
    public bool DownloadArchiveWithVerification(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        var targetFile = FindReleaseFile(installRequest, resolvedVersion);
        string? downloadUrl = targetFile?.Address.ToString();
        string? expectedHash = targetFile?.Hash.ToString();

        if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(downloadUrl))
        {
            return false;
        }

        if (!DownloadArchive(downloadUrl, destinationPath, progress))
        {
            return false;
        }

        return VerifyFileHash(destinationPath, expectedHash);
    }

    /// <summary>
    /// Finds the appropriate release file for the given installation.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <returns>The matching ReleaseFile, throws if none are available.</returns>
    private ReleaseFile? FindReleaseFile(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
    {
        try
        {
            var productCollection = GetProductCollection();
            var product = FindProduct(productCollection, resolvedVersion) ?? throw new InvalidOperationException($"No product found for version {resolvedVersion}");
            var release = FindRelease(product, resolvedVersion, installRequest.Component) ?? throw new InvalidOperationException($"No release found for version {resolvedVersion}");
            return FindMatchingFile(release, installRequest, resolvedVersion);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to find an available release for install {installRequest} : ${ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets or loads the ProductCollection with caching.
    /// </summary>
    private ProductCollection GetProductCollection()
    {
        if (_productCollection is not null)
        {
            return _productCollection;
        }

        _productCollection = ProductCollection.GetAsync().GetAwaiter().GetResult();
        return _productCollection;
    }

    /// <summary>
    /// Finds the product for the given version.
    /// </summary>
    private static Product? FindProduct(ProductCollection productCollection, ReleaseVersion releaseVersion)
    {
        var majorMinor = $"{releaseVersion.Major}.{releaseVersion.Minor}";
        return productCollection.FirstOrDefault(p => p.ProductVersion == majorMinor);
    }

    /// <summary>
    /// Finds the specific release for the given version.
    /// </summary>
    private static ReleaseComponent? FindRelease(Product product, ReleaseVersion resolvedVersion, InstallComponent component)
    {
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult().ToList();

        foreach (var release in releases)
        {
            if (component == InstallComponent.SDK)
            {
                foreach (var sdk in release.Sdks)
                {
                    if (sdk.Version.Equals(resolvedVersion))
                    {
                        return sdk;
                    }
                }
            }
            else
            {
                var runtimesQuery = component switch
                {
                    InstallComponent.ASPNETCore => release.Runtimes
                        .Where(r => r.Name.Contains("ASP", StringComparison.OrdinalIgnoreCase)),
                    InstallComponent.WindowsDesktop => release.Runtimes
                        .Where(r => r.Name.Contains("Desktop", StringComparison.OrdinalIgnoreCase)),
                    _ => release.Runtimes
                        .Where(r => r.Name.Contains(".NET Runtime", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Contains(".NET Core Runtime", StringComparison.OrdinalIgnoreCase)),
                };
                foreach (var runtime in runtimesQuery)
                {
                    if (runtime.Version.Equals(resolvedVersion))
                    {
                        return runtime;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the matching file in the release for the given installation requirements.
    /// </summary>
    private static ReleaseFile? FindMatchingFile(ReleaseComponent release, DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
    {
        var rid = DnupUtilities.GetRuntimeIdentifier(installRequest.InstallRoot.Architecture);
        var fileExtension = DnupUtilities.GetArchiveFileExtensionForPlatform();

        var matchingFiles = release.Files
             .Where(f => f.Rid == rid) // TODO: Do we support musl here?
             .Where(f => f.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
             .ToList();

        if (matchingFiles.Count == 0)
        {
            return null;
        }

        return matchingFiles.First();
    }

    /// <summary>
    /// Computes the SHA512 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file to hash</param>
    /// <returns>The hash as a lowercase hex string</returns>
    public static string ComputeFileHash(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        // TODO: Older runtime versions use a different SHA algorithm.
        // Eventually the manifest should indicate which algorithm to use.
        using var sha512 = SHA512.Create();
        byte[] hashBytes = sha512.ComputeHash(fileStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a downloaded file matches the expected hash.
    /// </summary>
    /// <param name="filePath">Path to the file to verify</param>
    /// <param name="expectedHash">Expected hash value</param>
    /// <returns>True if the hash matches, false otherwise</returns>
    public static bool VerifyFileHash(string filePath, string expectedHash)
    {
        if (string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        string actualHash = ComputeFileHash(filePath);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
